import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
  untracked,
} from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { DatePickerModule } from 'primeng/datepicker';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { InputTextModule } from 'primeng/inputtext';
import { FileUploadModule } from 'primeng/fileupload';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmationService, MessageService } from 'primeng/api';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { forkJoin } from 'rxjs';
import { ClinicalApiService } from '../../../../../core/api/clinical-api.service';
import { MediaCategory, MediaFileDto } from '../../../../../core/api/clinical-api.models';
import { toDateOnly } from '../../../../../core/api/api.models';
import { HasPermissionDirective } from '../../../../../core/auth/has-permission.directive';
import { MediaImageComponent } from '../../../../../shared/components/media-image/media-image.component';
import { TrDatePipe } from '../../../../../shared/pipes/tr-date.pipe';
import { PatientDetailStore } from '../../patient-detail.store';

interface CategoryChip {
  labelKey: string;
  value: MediaCategory | null;
}

/**
 * Goruntu arsivi sekmesi: kategori cipleri + thumbnail grid + coklu upload +
 * lightbox (onceki/sonraki, indir, sil) + iki gorsel yan yana karsilastirma modu.
 */
@Component({
  selector: 'app-images-tab',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    ReactiveFormsModule,
    ButtonModule,
    DialogModule,
    DatePickerModule,
    SelectModule,
    TextareaModule,
    InputTextModule,
    FileUploadModule,
    TooltipModule,
    TranslocoPipe,
    HasPermissionDirective,
    MediaImageComponent,
    TrDatePipe,
  ],
  templateUrl: './images-tab.component.html',
})
export class ImagesTabComponent {
  private readonly store = inject(PatientDetailStore);
  private readonly api = inject(ClinicalApiService);
  private readonly messageService = inject(MessageService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly transloco = inject(TranslocoService);
  private readonly fb = inject(FormBuilder);

  protected readonly MediaCategory = MediaCategory;

  protected readonly chips: CategoryChip[] = [
    { labelKey: 'images.categories.all', value: null },
    { labelKey: 'mediaCategory.xray', value: MediaCategory.Xray },
    { labelKey: 'mediaCategory.intraoralPhoto', value: MediaCategory.IntraoralPhoto },
    { labelKey: 'mediaCategory.document', value: MediaCategory.Document },
    { labelKey: 'mediaCategory.consentPdf', value: MediaCategory.ConsentPdf },
  ];

  protected readonly uploadCategoryOptions = [
    { labelKey: 'mediaCategory.xray', value: MediaCategory.Xray },
    { labelKey: 'mediaCategory.intraoralPhoto', value: MediaCategory.IntraoralPhoto },
    { labelKey: 'mediaCategory.document', value: MediaCategory.Document },
  ];

  protected readonly items = signal<MediaFileDto[]>([]);
  protected readonly loading = signal(false);
  protected readonly activeCategory = signal<MediaCategory | null>(null);

  // Upload
  protected readonly uploadVisible = signal(false);
  protected readonly uploading = signal(false);
  protected readonly pendingFiles = signal<File[]>([]);
  protected readonly uploadForm = this.fb.group({
    category: this.fb.nonNullable.control<MediaCategory>(MediaCategory.Xray),
    takenAt: this.fb.control<Date | null>(new Date()),
    toothNumber: this.fb.control<string | null>(null),
    description: this.fb.control<string | null>(null),
  });

  // Lightbox
  protected readonly lightboxIndex = signal<number | null>(null);
  protected readonly lightboxItem = computed<MediaFileDto | null>(() => {
    const idx = this.lightboxIndex();
    return idx == null ? null : (this.viewables()[idx] ?? null);
  });

  // Karsilastirma
  protected readonly compareMode = signal(false);
  protected readonly compareSelection = signal<MediaFileDto[]>([]);
  protected readonly compareVisible = signal(false);

  /** Lightbox'ta gezinilebilir (goruntulenebilir) ogeler — PDF'ler de meta ile gosterilir. */
  protected readonly viewables = computed(() => this.items());

  constructor() {
    effect(() => {
      const patient = this.store.patient();
      const category = this.activeCategory();
      if (patient) {
        untracked(() => this.load(patient.id, category));
      }
    });
  }

  private load(patientId: number, category: MediaCategory | null): void {
    this.loading.set(true);
    this.api.media(patientId, category ?? undefined).subscribe({
      next: (items) => {
        // Onam imza PNG'leri dahili artefakttir (imzali PDF'in icinde zaten yer alir);
        // "Tumu" galerisinde ayri kart olarak gosterilmez.
        this.items.set(
          category == null
            ? items.filter((i) => i.category !== MediaCategory.SignatureImage)
            : items,
        );
        this.loading.set(false);
      },
      error: () => {
        this.items.set([]);
        this.loading.set(false);
      },
    });
  }

  protected reload(): void {
    const patient = this.store.patient();
    if (patient) {
      this.load(patient.id, this.activeCategory());
    }
  }

  protected isPdf(item: MediaFileDto): boolean {
    return item.contentType.includes('pdf');
  }

  protected isImage(item: MediaFileDto): boolean {
    return item.contentType.startsWith('image/');
  }

  // --- Upload ---------------------------------------------------------------

  protected openUpload(): void {
    this.pendingFiles.set([]);
    this.uploadForm.reset({
      category: MediaCategory.Xray,
      takenAt: new Date(),
      toothNumber: null,
      description: null,
    });
    this.uploadVisible.set(true);
  }

  /** p-fileupload onSelect: secilen dosyalar birikir (coklu secim + drag-drop). */
  protected onFilesSelected(event: { currentFiles?: File[]; files?: File[] }): void {
    const files = event.currentFiles ?? event.files ?? [];
    this.pendingFiles.set([...files]);
  }

  protected onFileRemoved(event: { file?: File }): void {
    if (event.file) {
      this.pendingFiles.set(this.pendingFiles().filter((f) => f !== event.file));
    }
  }

  protected upload(): void {
    const patientId = this.store.patient()?.id;
    const files = this.pendingFiles();
    if (!patientId || files.length === 0) {
      return;
    }
    const v = this.uploadForm.getRawValue();
    this.uploading.set(true);
    forkJoin(
      files.map((file) =>
        this.api.uploadMedia(patientId, file, {
          category: v.category,
          description: v.description,
          toothNumber: v.toothNumber,
          takenAt: v.takenAt ? toDateOnly(v.takenAt) : null,
        }),
      ),
    ).subscribe({
      next: () => {
        this.uploading.set(false);
        this.uploadVisible.set(false);
        this.messageService.add({
          severity: 'success',
          summary: this.transloco.translate('images.uploadSuccess', { count: files.length }),
          life: 3000,
        });
        this.reload();
      },
      error: () => {
        this.uploading.set(false);
        this.reload();
      },
    });
  }

  // --- Lightbox -------------------------------------------------------------

  protected openItem(item: MediaFileDto): void {
    if (this.compareMode()) {
      this.toggleCompare(item);
      return;
    }
    const idx = this.viewables().findIndex((i) => i.id === item.id);
    if (idx >= 0) {
      this.lightboxIndex.set(idx);
    }
  }

  protected lightboxPrev(): void {
    const idx = this.lightboxIndex();
    if (idx != null && idx > 0) {
      this.lightboxIndex.set(idx - 1);
    }
  }

  protected lightboxNext(): void {
    const idx = this.lightboxIndex();
    if (idx != null && idx < this.viewables().length - 1) {
      this.lightboxIndex.set(idx + 1);
    }
  }

  protected download(item: MediaFileDto): void {
    this.api.downloadBlob(item.id).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = item.fileName;
        a.click();
        URL.revokeObjectURL(url);
      },
    });
  }

  protected remove(item: MediaFileDto): void {
    this.confirmation.confirm({
      header: this.transloco.translate('images.deleteTitle'),
      message: this.transloco.translate('images.deleteMessage'),
      icon: 'pi pi-exclamation-triangle',
      acceptButtonProps: {
        label: this.transloco.translate('common.delete'),
        severity: 'danger',
      },
      rejectButtonProps: {
        label: this.transloco.translate('common.cancel'),
        severity: 'secondary',
        outlined: true,
      },
      accept: () => {
        this.api.deleteMedia(item.id).subscribe({
          next: () => {
            this.lightboxIndex.set(null);
            this.messageService.add({
              severity: 'success',
              summary: this.transloco.translate('images.deleteSuccess'),
              life: 3000,
            });
            this.reload();
          },
        });
      },
    });
  }

  // --- Karsilastirma --------------------------------------------------------

  protected toggleCompareMode(): void {
    this.compareMode.update((v) => !v);
    this.compareSelection.set([]);
  }

  protected isSelectedForCompare(item: MediaFileDto): boolean {
    return this.compareSelection().some((i) => i.id === item.id);
  }

  private toggleCompare(item: MediaFileDto): void {
    if (!this.isImage(item)) {
      return;
    }
    const current = this.compareSelection();
    if (current.some((i) => i.id === item.id)) {
      this.compareSelection.set(current.filter((i) => i.id !== item.id));
      return;
    }
    const next = [...current, item].slice(-2);
    this.compareSelection.set(next);
    if (next.length === 2) {
      this.compareVisible.set(true);
    }
  }

  protected closeCompare(): void {
    this.compareVisible.set(false);
    this.compareMode.set(false);
    this.compareSelection.set([]);
  }

  protected formatSize(bytes: number): string {
    if (bytes < 1024) {
      return `${bytes} B`;
    }
    if (bytes < 1024 * 1024) {
      return `${(bytes / 1024).toFixed(0)} KB`;
    }
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  protected categoryLabelKey(category: MediaCategory): string {
    const map: Record<number, string> = {
      [MediaCategory.Xray]: 'mediaCategory.xray',
      [MediaCategory.IntraoralPhoto]: 'mediaCategory.intraoralPhoto',
      [MediaCategory.Document]: 'mediaCategory.document',
      [MediaCategory.ConsentPdf]: 'mediaCategory.consentPdf',
      [MediaCategory.LabAttachment]: 'mediaCategory.labAttachment',
      [MediaCategory.SignatureImage]: 'mediaCategory.signatureImage',
    };
    return map[category] ?? 'mediaCategory.other';
  }
}
