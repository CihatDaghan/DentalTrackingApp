import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
  untracked,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { DatePickerModule } from 'primeng/datepicker';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { TableModule } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmationService, MessageService } from 'primeng/api';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { RecallsApiService } from '../../../../core/api/recalls-api.service';
import { AppointmentsApiService } from '../../../../core/api/appointments-api.service';
import { ClinicContext } from '../../../../core/auth/clinic-context';
import {
  DoctorDto,
  RecallDto,
  RecallStatus,
  toDateOnly,
  toUtcIso,
} from '../../../../core/api/api.models';
import { StatusTagComponent } from '../../../../shared/components/status-tag/status-tag.component';
import { TrDatePipe } from '../../../../shared/pipes/tr-date.pipe';
import { PatientDetailStore } from '../patient-detail.store';

const DURATIONS = [15, 30, 45, 60, 90, 120];

/** Kontrol (recall) sekmesi: liste + "Kontrol planla" + "Randevuya cevir". */
@Component({
  selector: 'app-controls-tab',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    ButtonModule,
    DialogModule,
    DatePickerModule,
    SelectModule,
    TextareaModule,
    TableModule,
    TooltipModule,
    TranslocoPipe,
    StatusTagComponent,
    TrDatePipe,
  ],
  templateUrl: './controls-tab.component.html',
})
export class ControlsTabComponent {
  private readonly store = inject(PatientDetailStore);
  private readonly recallsApi = inject(RecallsApiService);
  private readonly appointmentsApi = inject(AppointmentsApiService);
  private readonly clinicContext = inject(ClinicContext);
  private readonly messageService = inject(MessageService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly transloco = inject(TranslocoService);
  private readonly fb = inject(FormBuilder);

  protected readonly RecallStatus = RecallStatus;
  protected readonly minDate = new Date();

  protected readonly recalls = signal<RecallDto[]>([]);
  protected readonly loading = signal(false);
  protected readonly doctors = signal<DoctorDto[]>([]);

  protected readonly planVisible = signal(false);
  protected readonly planSaving = signal(false);
  protected readonly convertTarget = signal<RecallDto | null>(null);
  protected readonly convertSaving = signal(false);

  protected readonly durationOptions = DURATIONS.map((m) => ({
    label: `${m} dk`,
    value: m,
  }));

  protected readonly planForm = this.fb.group({
    suggestedDate: this.fb.control<Date | null>(null, [Validators.required]),
    reason: this.fb.control<string | null>(null),
    doctorUserId: this.fb.control<number | null>(null),
  });

  protected readonly convertForm = this.fb.group({
    start: this.fb.control<Date | null>(null, [Validators.required]),
    durationMinutes: this.fb.nonNullable.control(30),
    doctorUserId: this.fb.control<number | null>(null, [Validators.required]),
    note: this.fb.control<string | null>(null),
  });

  protected readonly patientId = computed(() => this.store.patient()?.id ?? null);

  constructor() {
    // Hasta async yuklenir; hazir oldugunda recall listesi cekilir.
    effect(() => {
      const patient = this.store.patient();
      if (patient) {
        untracked(() => this.loadRecalls());
      }
    });
    this.appointmentsApi.doctors().subscribe({
      next: (doctors) => this.doctors.set(doctors),
      error: () => this.doctors.set([]),
    });
  }

  protected doctorName(id: number | null): string {
    const doctor = this.doctors().find((d) => d.id === id);
    return doctor ? `${doctor.firstName} ${doctor.lastName}` : '—';
  }

  protected loadRecalls(): void {
    const patientId = this.store.patient()?.id;
    if (!patientId) {
      return;
    }
    this.loading.set(true);
    // Sunucu tarafi patientId filtresi (istemci suzmesi 100 kayit sinirinda kontrol kaciriyordu).
    this.recallsApi.list({ patientId, page: 1, pageSize: 100 }).subscribe({
      next: (result) => {
        this.recalls.set(result.items);
        this.loading.set(false);
      },
      error: () => {
        this.recalls.set([]);
        this.loading.set(false);
      },
    });
  }

  // --- Kontrol planla -------------------------------------------------------

  protected openPlan(): void {
    const suggested = new Date();
    suggested.setMonth(suggested.getMonth() + 6);
    this.planForm.reset({ suggestedDate: suggested, reason: null, doctorUserId: null });
    this.planVisible.set(true);
  }

  protected savePlan(): void {
    const patientId = this.patientId();
    if (!patientId || this.planForm.invalid) {
      this.planForm.markAllAsTouched();
      return;
    }
    const v = this.planForm.getRawValue();
    this.planSaving.set(true);
    this.recallsApi
      .create({
        patientId,
        suggestedDate: toDateOnly(v.suggestedDate as Date),
        reason: v.reason,
        doctorUserId: v.doctorUserId,
      })
      .subscribe({
        next: () => {
          this.planSaving.set(false);
          this.planVisible.set(false);
          this.messageService.add({
            severity: 'success',
            summary: this.transloco.translate('controls.planSuccess'),
            life: 3000,
          });
          this.loadRecalls();
        },
        error: () => this.planSaving.set(false),
      });
  }

  // --- Randevuya cevir ------------------------------------------------------

  protected openConvert(recall: RecallDto): void {
    const [y, m, d] = recall.suggestedDate.split('-').map(Number);
    const start = new Date(y, m - 1, d, 9, 0);
    this.convertForm.reset({
      start,
      durationMinutes: 30,
      doctorUserId: recall.doctorUserId ?? this.doctors()[0]?.id ?? null,
      note: recall.reason,
    });
    this.convertTarget.set(recall);
  }

  protected saveConvert(): void {
    const recall = this.convertTarget();
    const clinicId = this.clinicContext.clinicId();
    if (!recall || this.convertForm.invalid) {
      this.convertForm.markAllAsTouched();
      return;
    }
    // Klinik baglami yoksa kullaniciya sebebini soyle (inceleme bulgusu: sessiz no-op).
    if (!clinicId) {
      this.messageService.add({
        severity: 'warn',
        summary: this.transloco.translate('calendar.noClinicContext'),
        life: 5000,
      });
      return;
    }
    const v = this.convertForm.getRawValue();
    const start = v.start as Date;
    const end = new Date(start.getTime() + v.durationMinutes * 60_000);
    this.convertSaving.set(true);
    this.recallsApi
      .convert(recall.id, {
        clinicId,
        doctorUserId: v.doctorUserId as number,
        startUtc: toUtcIso(start),
        endUtc: toUtcIso(end),
        note: v.note,
      })
      .subscribe({
        next: () => {
          this.convertSaving.set(false);
          this.convertTarget.set(null);
          this.messageService.add({
            severity: 'success',
            summary: this.transloco.translate('controls.convertSuccess'),
            life: 3000,
          });
          this.loadRecalls();
        },
        error: () => this.convertSaving.set(false),
      });
  }

  // --- Vazgecti -------------------------------------------------------------

  protected abandon(recall: RecallDto): void {
    this.confirmation.confirm({
      header: this.transloco.translate('controls.abandonTitle'),
      message: this.transloco.translate('controls.abandonMessage'),
      icon: 'pi pi-exclamation-triangle',
      acceptButtonProps: {
        label: this.transloco.translate('common.yes'),
        severity: 'danger',
      },
      rejectButtonProps: {
        label: this.transloco.translate('common.no'),
        severity: 'secondary',
        outlined: true,
      },
      accept: () => {
        this.recallsApi
          .updateStatus(recall.id, { status: RecallStatus.Abandoned })
          .subscribe({ next: () => this.loadRecalls() });
      },
    });
  }
}
