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
import { AutoCompleteCompleteEvent, AutoCompleteModule } from 'primeng/autocomplete';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { PrescriptionsApiService } from '../../../../../core/api/prescriptions-api.service';
import {
  DrugDto,
  FREQUENCY_PRESETS,
  PrescriptionDto,
  PrescriptionItemRequest,
  PrescriptionTemplateDto,
  USAGE_PRESET_KEYS,
} from '../../../../../core/api/prescription-api.models';
import { AppointmentsApiService } from '../../../../../core/api/appointments-api.service';
import { DoctorDto } from '../../../../../core/api/api.models';
import { AuthStore } from '../../../../../core/auth/auth.store';
import { UserType } from '../../../../../core/api/auth-api.models';
import { HasPermissionDirective } from '../../../../../core/auth/has-permission.directive';
import { StatusTagComponent } from '../../../../../shared/components/status-tag/status-tag.component';
import { TrDatePipe } from '../../../../../shared/pipes/tr-date.pipe';
import { downloadBlob, openBlobInNewTab } from '../../../../../shared/utils/file-download';
import { injectTranslationSignal } from '../../../../../shared/utils/transloco-signal';
import { PatientDetailStore } from '../../patient-detail.store';

/** Dialogdaki duzenlenebilir recete satiri (ilac + posoloji). */
interface PrescriptionLine {
  drug: DrugDto;
  boxCount: number;
  dose: string;
  frequency: string;
  duration: string;
  usageNote: string;
}

/**
 * Recete sekmesi: gecmis receteler + genis "Yeni Recete" dialogu
 * (solda ilac arama/satirlar, sagda sablonlarim).
 */
@Component({
  selector: 'app-prescriptions-tab',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    AutoCompleteModule,
    ButtonModule,
    CheckboxModule,
    DialogModule,
    InputNumberModule,
    InputTextModule,
    SelectModule,
    TableModule,
    TooltipModule,
    TranslocoPipe,
    HasPermissionDirective,
    StatusTagComponent,
    TrDatePipe,
  ],
  templateUrl: './prescriptions-tab.component.html',
})
export class PrescriptionsTabComponent {
  private readonly store = inject(PatientDetailStore);
  private readonly api = inject(PrescriptionsApiService);
  private readonly appointmentsApi = inject(AppointmentsApiService);
  private readonly authStore = inject(AuthStore);
  private readonly messageService = inject(MessageService);
  private readonly transloco = inject(TranslocoService);

  /** Ceviriler yuklendiginde/dil degistiginde secenek listeleri yeniden hesaplansin. */
  private readonly translation = injectTranslationSignal();

  protected readonly frequencyPresets = FREQUENCY_PRESETS;

  protected readonly prescriptions = signal<PrescriptionDto[]>([]);
  protected readonly loading = signal(false);

  protected readonly dialogVisible = signal(false);
  protected readonly saving = signal(false);
  protected readonly lines = signal<PrescriptionLine[]>([]);
  protected readonly templates = signal<PrescriptionTemplateDto[]>([]);
  protected readonly doctors = signal<DoctorDto[]>([]);
  protected readonly doctorId = signal<number | null>(null);
  protected readonly drugSuggestions = signal<DrugDto[]>([]);
  protected readonly drugQuery = signal<DrugDto | string | null>(null);
  protected readonly appliedTemplateId = signal<number | null>(null);

  protected readonly saveAsTemplate = signal(false);
  protected readonly templateName = signal('');
  protected readonly openPdfAfterSave = signal(true);

  /** Hekim rolundeki kullanici kendi adina yazar — secici kilitlenir. */
  protected readonly doctorLocked = computed(
    () => this.authStore.user()?.userType === UserType.Dentist,
  );

  /** Kontrole tabi ilac varsa formda kalici uyari bandi gorunur. */
  protected readonly hasControlled = computed(() => this.lines().some((l) => l.drug.isControlled));

  protected readonly usageOptions = computed(() => {
    this.translation();
    return USAGE_PRESET_KEYS.map((key) => this.transloco.translate('prescriptions.usage.' + key));
  });

  protected readonly doctorOptions = computed(() =>
    this.doctors().map((d) => ({ label: `${d.firstName} ${d.lastName}`, value: d.id })),
  );

  protected readonly canSave = computed(() => this.lines().length > 0 && this.doctorId() != null);

  constructor() {
    effect(() => {
      const patient = this.store.patient();
      if (patient) {
        untracked(() => this.load(patient.id));
      }
    });

    this.appointmentsApi.doctors().subscribe({
      next: (doctors) => {
        this.doctors.set(doctors);
        this.applyDefaultDoctor(doctors);
      },
      error: () => this.doctors.set([]),
    });
  }

  private applyDefaultDoctor(doctors: DoctorDto[]): void {
    const user = this.authStore.user();
    const own = user ? doctors.find((d) => d.id === user.id) : undefined;
    this.doctorId.set(own?.id ?? doctors[0]?.id ?? null);
  }

  private load(patientId: number): void {
    this.loading.set(true);
    this.api.list(patientId).subscribe({
      next: (items) => {
        this.prescriptions.set(
          [...items].sort((a, b) => b.createdAtUtc.localeCompare(a.createdAtUtc)),
        );
        this.loading.set(false);
      },
      error: () => {
        this.prescriptions.set([]);
        this.loading.set(false);
      },
    });
  }

  // --- Dialog ---------------------------------------------------------------

  protected openNew(): void {
    this.lines.set([]);
    this.drugQuery.set(null);
    this.drugSuggestions.set([]);
    this.appliedTemplateId.set(null);
    this.saveAsTemplate.set(false);
    this.templateName.set('');
    this.openPdfAfterSave.set(true);
    this.applyDefaultDoctor(this.doctors());
    this.dialogVisible.set(true);
    this.api.templates().subscribe({
      next: (templates) => this.templates.set(templates),
      error: () => this.templates.set([]),
    });
  }

  protected searchDrugs(event: AutoCompleteCompleteEvent): void {
    const query = event.query.trim();
    if (query.length < 2) {
      this.drugSuggestions.set([]);
      return;
    }
    this.api.drugs(query).subscribe({
      next: (drugs) => this.drugSuggestions.set(drugs),
      error: () => this.drugSuggestions.set([]),
    });
  }

  /** Autocomplete secimi: satir olarak ekle ve arama kutusunu bosalt. */
  protected onDrugSelected(value: DrugDto | string | null): void {
    if (!value || typeof value !== 'object') {
      return;
    }
    this.addDrug(value);
    // Model'i ayni tick icinde temizlemek p-autocomplete'i sasirtiyor.
    queueMicrotask(() => this.drugQuery.set(null));
  }

  private addDrug(drug: DrugDto): void {
    if (this.lines().some((l) => l.drug.id === drug.id)) {
      this.messageService.add({
        severity: 'warn',
        summary: this.transloco.translate('prescriptions.duplicateDrug'),
        life: 3000,
      });
      return;
    }
    this.lines.update((lines) => [
      ...lines,
      {
        drug,
        boxCount: 1,
        dose: drug.defaultDose ?? '',
        frequency: '',
        duration: '',
        usageNote: drug.defaultUsage ?? '',
      },
    ]);
  }

  protected removeLine(index: number): void {
    this.lines.update((lines) => lines.filter((_, i) => i !== index));
  }

  protected patchLine(index: number, patch: Partial<PrescriptionLine>): void {
    this.lines.update((lines) => lines.map((l, i) => (i === index ? { ...l, ...patch } : l)));
  }

  /** Sablona tikla -> satirlar forma dolar (mevcut satirlarin yerine gecer). */
  protected applyTemplate(template: PrescriptionTemplateDto): void {
    this.lines.set(
      template.items.map((item) => ({
        drug: {
          id: item.drugId,
          tenantId: null,
          barcode: null,
          name: item.drugName,
          atcCode: null,
          form: item.drugForm,
          defaultDose: item.dose,
          defaultUsage: item.usageNote,
          isControlled: item.isControlled,
        },
        boxCount: item.boxCount,
        dose: item.dose ?? '',
        frequency: item.frequency ?? '',
        duration: item.duration ?? '',
        usageNote: item.usageNote ?? '',
      })),
    );
    this.appliedTemplateId.set(template.id);
  }

  protected save(): void {
    const patientId = this.store.patient()?.id;
    const doctorUserId = this.doctorId();
    if (!patientId || doctorUserId == null || this.lines().length === 0) {
      return;
    }
    const items: PrescriptionItemRequest[] = this.lines().map((l) => ({
      drugId: l.drug.id,
      boxCount: l.boxCount || 1,
      dose: l.dose || null,
      frequency: l.frequency || null,
      duration: l.duration || null,
      usageNote: l.usageNote || null,
    }));
    this.saving.set(true);
    this.api
      .create(patientId, { doctorUserId, templateId: this.appliedTemplateId(), items })
      .subscribe({
        next: (prescription) => {
          this.saving.set(false);
          this.dialogVisible.set(false);
          this.messageService.add({
            severity: 'success',
            summary: this.transloco.translate('prescriptions.saveSuccess'),
            life: 3000,
          });
          this.load(patientId);
          if (this.saveAsTemplate() && this.templateName().trim()) {
            this.api
              .saveAsTemplate(prescription.id, { name: this.templateName().trim() })
              .subscribe({
                next: () =>
                  this.messageService.add({
                    severity: 'success',
                    summary: this.transloco.translate('prescriptions.templateSaved'),
                    life: 3000,
                  }),
              });
          }
          if (this.openPdfAfterSave()) {
            this.openPdf(prescription);
          }
        },
        error: () => this.saving.set(false),
      });
  }

  // --- PDF ------------------------------------------------------------------

  protected downloadPdf(prescription: PrescriptionDto): void {
    this.api.pdfBlob(prescription.id).subscribe({
      next: (blob) => downloadBlob(blob, this.pdfFileName(prescription)),
    });
  }

  protected openPdf(prescription: PrescriptionDto): void {
    this.api.pdfBlob(prescription.id).subscribe({
      next: (blob) => openBlobInNewTab(blob, this.pdfFileName(prescription)),
    });
  }

  private pdfFileName(prescription: PrescriptionDto): string {
    return `recete-${prescription.prescriptionNo || prescription.id}.pdf`;
  }
}
