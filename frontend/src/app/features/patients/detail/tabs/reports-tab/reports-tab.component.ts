import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
  untracked,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TextareaModule } from 'primeng/textarea';
import { CheckboxModule } from 'primeng/checkbox';
import { MessageService } from 'primeng/api';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { PatientDetailStore } from '../../patient-detail.store';
import {
  PatientReportsApiService,
  PatientTreatmentReportDto,
  ProformaDto,
} from '../../../../../core/api/patient-reports-api.service';
import { TreatmentsApiService } from '../../../../../core/api/treatments-api.service';
import {
  TreatmentRecordDto,
  TreatmentRecordStatus,
} from '../../../../../core/api/treatment-api.models';
import { ClinicalApiService } from '../../../../../core/api/clinical-api.service';
import { MediaCategory, MediaFileDto } from '../../../../../core/api/clinical-api.models';
import { toDateOnly } from '../../../../../core/api/api.models';
import { MoneyPipe } from '../../../../../shared/pipes/money.pipe';
import { TrDatePipe } from '../../../../../shared/pipes/tr-date.pipe';
import { StatusTagComponent } from '../../../../../shared/components/status-tag/status-tag.component';
import { downloadBlob, openBlobInNewTab } from '../../../../../shared/utils/file-download';
import { injectTranslationSignal } from '../../../../../shared/utils/transloco-signal';

/** Arsiv filtresi: onam PDF'leri + uretilmis rapor PDF'leri. */
const ARCHIVE_CATEGORIES = [
  { key: 'all', value: null as MediaCategory | null },
  { key: 'patientReportPdf', value: MediaCategory.PatientReportPdf },
  { key: 'consentPdf', value: MediaCategory.ConsentPdf },
];

/**
 * Hasta karti "Rapor" sekmesi: tedavi raporu, durum bildirir rapor, proforma
 * (fiyat teklifi) ve uretilmis belge arsivi.
 */
@Component({
  selector: 'app-reports-tab',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    ButtonModule,
    DatePickerModule,
    SelectModule,
    TableModule,
    TextareaModule,
    CheckboxModule,
    TranslocoPipe,
    MoneyPipe,
    TrDatePipe,
    StatusTagComponent,
  ],
  templateUrl: './reports-tab.component.html',
})
export class ReportsTabComponent {
  private readonly store = inject(PatientDetailStore);
  private readonly api = inject(PatientReportsApiService);
  private readonly treatmentsApi = inject(TreatmentsApiService);
  private readonly clinicalApi = inject(ClinicalApiService);
  private readonly messageService = inject(MessageService);
  private readonly transloco = inject(TranslocoService);
  private readonly translation = injectTranslationSignal();

  protected readonly patientId = computed(() => this.store.patient()?.id ?? 0);

  // --- Tedavi raporu --------------------------------------------------------
  protected readonly from = signal<Date | null>(null);
  protected readonly to = signal<Date | null>(null);
  protected readonly treatmentReport = signal<PatientTreatmentReportDto | null>(null);
  protected readonly treatmentLoading = signal(false);
  protected readonly treatmentPdfLoading = signal(false);

  // --- Durum raporu ---------------------------------------------------------
  protected readonly statusPdfLoading = signal(false);

  // --- Proforma -------------------------------------------------------------
  protected readonly planned = signal<TreatmentRecordDto[]>([]);
  protected readonly selectedIds = signal<number[]>([]);
  protected readonly proformaNote = signal('');
  protected readonly proformaValidUntil = signal<Date | null>(null);
  protected readonly proformaPreview = signal<ProformaDto | null>(null);
  protected readonly proformaLoading = signal(false);

  protected readonly selectedTotal = computed(() => {
    const ids = new Set(this.selectedIds());
    return this.planned()
      .filter((t) => ids.has(t.id))
      .reduce((sum, t) => sum + (t.price - t.discountAmount), 0);
  });

  // --- Arsiv ----------------------------------------------------------------
  protected readonly archive = signal<MediaFileDto[]>([]);
  protected readonly archiveCategory = signal<MediaCategory | null>(null);
  protected readonly archiveLoading = signal(false);

  protected readonly archiveOptions = computed(() => {
    this.translation();
    return ARCHIVE_CATEGORIES.map((c) => ({
      label:
        c.key === 'all'
          ? this.transloco.translate('common.all')
          : this.transloco.translate('patientReports.archive.' + c.key),
      value: c.value,
    }));
  });

  constructor() {
    const today = new Date();
    this.to.set(today);
    this.from.set(new Date(today.getFullYear(), today.getMonth() - 6, 1));
    // Hasta kaydi asenkron yuklendigi icin id gelince tetiklenir.
    effect(() => {
      const id = this.store.patient()?.id;
      if (!id) {
        return;
      }
      untracked(() => {
        this.loadTreatmentReport();
        this.loadPlanned();
        this.loadArchive();
      });
    });
  }

  // --- Tedavi raporu --------------------------------------------------------

  protected loadTreatmentReport(): void {
    const id = this.patientId();
    if (!id) {
      return;
    }
    this.treatmentLoading.set(true);
    this.api.treatmentReport(id, this.dateOnly(this.from()), this.dateOnly(this.to())).subscribe({
      next: (report) => {
        this.treatmentReport.set(report);
        this.treatmentLoading.set(false);
      },
      error: () => {
        this.treatmentReport.set(null);
        this.treatmentLoading.set(false);
      },
    });
  }

  protected downloadTreatmentPdf(): void {
    const id = this.patientId();
    this.treatmentPdfLoading.set(true);
    this.api.treatmentReportPdf(id, this.dateOnly(this.from()), this.dateOnly(this.to())).subscribe({
      next: (blob) => {
        downloadBlob(blob, `tedavi-raporu-${id}.pdf`);
        this.treatmentPdfLoading.set(false);
        this.loadArchive();
      },
      error: () => this.treatmentPdfLoading.set(false),
    });
  }

  // --- Durum raporu ---------------------------------------------------------

  protected downloadStatusPdf(): void {
    const id = this.patientId();
    this.statusPdfLoading.set(true);
    this.api.statusReportPdf(id).subscribe({
      next: (blob) => {
        downloadBlob(blob, `durum-raporu-${id}.pdf`);
        this.statusPdfLoading.set(false);
        this.loadArchive();
      },
      error: () => this.statusPdfLoading.set(false),
    });
  }

  // --- Proforma -------------------------------------------------------------

  protected toggleTreatment(id: number, checked: boolean): void {
    this.selectedIds.update((ids) =>
      checked ? [...new Set([...ids, id])] : ids.filter((i) => i !== id),
    );
    this.proformaPreview.set(null);
  }

  protected isSelected(id: number): boolean {
    return this.selectedIds().includes(id);
  }

  protected toggleAll(checked: boolean): void {
    this.selectedIds.set(checked ? this.planned().map((t) => t.id) : []);
    this.proformaPreview.set(null);
  }

  protected previewProforma(): void {
    if (this.selectedIds().length === 0) {
      return;
    }
    this.proformaLoading.set(true);
    this.api
      .proforma(this.patientId(), {
        treatmentRecordIds: this.selectedIds(),
        validUntil: this.dateOnly(this.proformaValidUntil()),
        note: this.proformaNote() || null,
      })
      .subscribe({
        next: (dto) => {
          this.proformaPreview.set(dto);
          this.proformaLoading.set(false);
        },
        error: () => this.proformaLoading.set(false),
      });
  }

  protected downloadProformaPdf(): void {
    if (this.selectedIds().length === 0) {
      this.messageService.add({
        severity: 'warn',
        summary: this.transloco.translate('patientReports.proforma.selectFirst'),
        life: 4000,
      });
      return;
    }
    const id = this.patientId();
    this.proformaLoading.set(true);
    this.api
      .proformaPdf(id, {
        treatmentRecordIds: this.selectedIds(),
        validUntil: this.dateOnly(this.proformaValidUntil()),
        note: this.proformaNote() || null,
      })
      .subscribe({
        next: (blob) => {
          downloadBlob(blob, `proforma-${id}.pdf`);
          this.proformaLoading.set(false);
          this.loadArchive();
        },
        error: () => this.proformaLoading.set(false),
      });
  }

  // --- Arsiv ----------------------------------------------------------------

  protected onArchiveCategoryChange(category: MediaCategory | null): void {
    this.archiveCategory.set(category);
    this.loadArchive();
  }

  protected openArchiveFile(file: MediaFileDto): void {
    this.clinicalApi.downloadBlob(file.id).subscribe({
      next: (blob) => openBlobInNewTab(blob, file.fileName),
      error: () => undefined,
    });
  }

  protected downloadArchiveFile(file: MediaFileDto): void {
    this.clinicalApi.downloadBlob(file.id).subscribe({
      next: (blob) => downloadBlob(blob, file.fileName),
      error: () => undefined,
    });
  }

  protected categoryKey(category: MediaCategory): string {
    return category === MediaCategory.ConsentPdf
      ? 'patientReports.archive.consentPdf'
      : category === MediaCategory.PatientReportPdf
        ? 'patientReports.archive.patientReportPdf'
        : 'mediaCategory.document';
  }

  private loadPlanned(): void {
    const id = this.patientId();
    if (!id) {
      return;
    }
    this.treatmentsApi.list(id, TreatmentRecordStatus.Planned).subscribe({
      next: (rows) => this.planned.set(rows),
      error: () => this.planned.set([]),
    });
  }

  private loadArchive(): void {
    const id = this.patientId();
    if (!id) {
      return;
    }
    this.archiveLoading.set(true);
    const category = this.archiveCategory();
    if (category != null) {
      this.clinicalApi.media(id, category).subscribe({
        next: (files) => {
          this.archive.set(files);
          this.archiveLoading.set(false);
        },
        error: () => {
          this.archive.set([]);
          this.archiveLoading.set(false);
        },
      });
      return;
    }
    // "Tumu" = onam PDF'leri + uretilmis rapor PDF'leri
    this.clinicalApi.media(id, MediaCategory.PatientReportPdf).subscribe({
      next: (reports) => {
        this.clinicalApi.media(id, MediaCategory.ConsentPdf).subscribe({
          next: (consents) => {
            this.archive.set(
              [...reports, ...consents].sort((a, b) =>
                b.createdAtUtc.localeCompare(a.createdAtUtc),
              ),
            );
            this.archiveLoading.set(false);
          },
          error: () => {
            this.archive.set(reports);
            this.archiveLoading.set(false);
          },
        });
      },
      error: () => {
        this.archive.set([]);
        this.archiveLoading.set(false);
      },
    });
  }

  private dateOnly(value: Date | null): string | null {
    return value ? toDateOnly(value) : null;
  }
}
