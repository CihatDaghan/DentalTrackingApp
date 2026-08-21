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
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TextareaModule } from 'primeng/textarea';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { EpicrisisApiService } from '../../../../../core/api/epicrisis-api.service';
import {
  EpicrisisDiagnosis,
  EpicrisisDto,
  IcdCodeDto,
} from '../../../../../core/api/epicrisis-api.models';
import { TreatmentsApiService } from '../../../../../core/api/treatments-api.service';
import {
  TreatmentRecordDto,
  TreatmentRecordStatus,
} from '../../../../../core/api/treatment-api.models';
import { AppointmentsApiService } from '../../../../../core/api/appointments-api.service';
import { DoctorDto } from '../../../../../core/api/api.models';
import { AuthStore } from '../../../../../core/auth/auth.store';
import { UserType } from '../../../../../core/api/auth-api.models';
import { HasPermissionDirective } from '../../../../../core/auth/has-permission.directive';
import { TrDatePipe } from '../../../../../shared/pipes/tr-date.pipe';
import { downloadBlob, openBlobInNewTab } from '../../../../../shared/utils/file-download';
import { PatientDetailStore } from '../../patient-detail.store';

/** ICD autocomplete secim ogesi (chip etiketi hazir gelsin diye label tasir). */
interface DiagnosisOption extends EpicrisisDiagnosis {
  label: string;
}

/** Epikriz sekmesi: gecmis epikrizler + tani/tedavi secimli olusturma dialogu. */
@Component({
  selector: 'app-epicrisis-tab',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    AutoCompleteModule,
    ButtonModule,
    CheckboxModule,
    DialogModule,
    InputTextModule,
    SelectModule,
    TableModule,
    TextareaModule,
    TooltipModule,
    TranslocoPipe,
    HasPermissionDirective,
    TrDatePipe,
  ],
  templateUrl: './epicrisis-tab.component.html',
})
export class EpicrisisTabComponent {
  private readonly store = inject(PatientDetailStore);
  private readonly api = inject(EpicrisisApiService);
  private readonly treatmentsApi = inject(TreatmentsApiService);
  private readonly appointmentsApi = inject(AppointmentsApiService);
  private readonly authStore = inject(AuthStore);
  private readonly messageService = inject(MessageService);
  private readonly transloco = inject(TranslocoService);

  protected readonly items = signal<EpicrisisDto[]>([]);
  protected readonly loading = signal(false);

  protected readonly dialogVisible = signal(false);
  protected readonly saving = signal(false);
  protected readonly title = signal('');
  protected readonly bodyText = signal('');
  protected readonly diagnoses = signal<DiagnosisOption[]>([]);
  protected readonly icdSuggestions = signal<DiagnosisOption[]>([]);
  protected readonly doneTreatments = signal<TreatmentRecordDto[]>([]);
  protected readonly selectedTreatmentIds = signal<number[]>([]);
  protected readonly doctors = signal<DoctorDto[]>([]);
  protected readonly doctorId = signal<number | null>(null);
  protected readonly openPdfAfterSave = signal(true);

  /** Hekim rolundeki kullanici kendi adina duzenler — secici kilitlenir. */
  protected readonly doctorLocked = computed(
    () => this.authStore.user()?.userType === UserType.Dentist,
  );

  protected readonly doctorOptions = computed(() =>
    this.doctors().map((d) => ({ label: `${d.firstName} ${d.lastName}`, value: d.id })),
  );

  protected readonly canSave = computed(
    () => this.title().trim().length > 0 && this.doctorId() != null,
  );

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
        this.items.set([...items].sort((a, b) => b.createdAtUtc.localeCompare(a.createdAtUtc)));
        this.loading.set(false);
      },
      error: () => {
        this.items.set([]);
        this.loading.set(false);
      },
    });
  }

  // --- Dialog ---------------------------------------------------------------

  protected openNew(): void {
    const patientId = this.store.patient()?.id;
    this.title.set('');
    this.bodyText.set('');
    this.diagnoses.set([]);
    this.icdSuggestions.set([]);
    this.selectedTreatmentIds.set([]);
    this.openPdfAfterSave.set(true);
    this.applyDefaultDoctor(this.doctors());
    this.dialogVisible.set(true);
    if (patientId) {
      this.treatmentsApi.list(patientId, TreatmentRecordStatus.Done).subscribe({
        next: (records) =>
          this.doneTreatments.set(
            [...records].sort((a, b) =>
              (b.performedAtUtc ?? b.createdAtUtc).localeCompare(
                a.performedAtUtc ?? a.createdAtUtc,
              ),
            ),
          ),
        error: () => this.doneTreatments.set([]),
      });
    }
  }

  protected searchIcd(event: AutoCompleteCompleteEvent): void {
    const query = event.query.trim();
    if (query.length < 2) {
      this.icdSuggestions.set([]);
      return;
    }
    this.api.icdCodes(query).subscribe({
      next: (codes) => this.icdSuggestions.set(codes.map((c) => this.toOption(c))),
      error: () => this.icdSuggestions.set([]),
    });
  }

  private toOption(code: IcdCodeDto): DiagnosisOption {
    return { code: code.code, name: code.name, label: `${code.code} — ${code.name}` };
  }

  /** p-autocomplete multiple modunda serbest metin de gelebilir; yalniz nesneleri sakla. */
  protected onDiagnosesChange(value: (DiagnosisOption | string)[]): void {
    this.diagnoses.set(value.filter((v): v is DiagnosisOption => typeof v === 'object'));
  }

  protected isTreatmentSelected(id: number): boolean {
    return this.selectedTreatmentIds().includes(id);
  }

  protected toggleTreatment(id: number, checked: boolean): void {
    this.selectedTreatmentIds.update((ids) =>
      checked ? [...new Set([...ids, id])] : ids.filter((x) => x !== id),
    );
  }

  protected toggleAllTreatments(checked: boolean): void {
    this.selectedTreatmentIds.set(checked ? this.doneTreatments().map((t) => t.id) : []);
  }

  protected save(): void {
    const patientId = this.store.patient()?.id;
    const doctorUserId = this.doctorId();
    if (!patientId || doctorUserId == null || !this.title().trim()) {
      return;
    }
    this.saving.set(true);
    this.api
      .create(patientId, {
        doctorUserId,
        title: this.title().trim(),
        diagnoses: this.diagnoses().map((d) => ({ code: d.code, name: d.name })),
        treatmentRecordIds: this.selectedTreatmentIds(),
        bodyText: this.bodyText().trim() || null,
      })
      .subscribe({
        next: (epicrisis) => {
          this.saving.set(false);
          this.dialogVisible.set(false);
          this.messageService.add({
            severity: 'success',
            summary: this.transloco.translate('epicrisis.saveSuccess'),
            life: 3000,
          });
          this.load(patientId);
          if (this.openPdfAfterSave()) {
            this.openPdf(epicrisis);
          }
        },
        error: () => this.saving.set(false),
      });
  }

  // --- PDF ------------------------------------------------------------------

  protected downloadPdf(epicrisis: EpicrisisDto): void {
    this.api.pdfBlob(epicrisis.id).subscribe({
      next: (blob) => downloadBlob(blob, `epikriz-${epicrisis.id}.pdf`),
    });
  }

  protected openPdf(epicrisis: EpicrisisDto): void {
    this.api.pdfBlob(epicrisis.id).subscribe({
      next: (blob) => openBlobInNewTab(blob, `epikriz-${epicrisis.id}.pdf`),
    });
  }
}
