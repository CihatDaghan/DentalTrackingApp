import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { DatePickerModule } from 'primeng/datepicker';
import { SelectModule } from 'primeng/select';
import { SelectButtonModule } from 'primeng/selectbutton';
import { TextareaModule } from 'primeng/textarea';
import { InputTextModule } from 'primeng/inputtext';
import { ColorPickerModule } from 'primeng/colorpicker';
import { MessageService } from 'primeng/api';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AppointmentsApiService } from '../../core/api/appointments-api.service';
import { ClinicContext } from '../../core/auth/clinic-context';
import {
  AppointmentDto,
  AppointmentType,
  AppointmentUpsertRequest,
  DoctorDto,
  parseUtc,
  toUtcIso,
} from '../../core/api/api.models';
import {
  PatientOption,
  PatientSearchSelectComponent,
} from '../../shared/components/patient-search-select/patient-search-select.component';
import { APPOINTMENT_DURATIONS } from './appointment-utils';

/** Dialog acilis baglami: bos slottan olusturma ya da mevcut randevuyu duzenleme. */
export interface AppointmentDialogState {
  mode: 'create' | 'edit';
  appointment?: AppointmentDto;
  start?: Date;
  end?: Date;
  doctorUserId?: number | null;
  patient?: PatientOption | null;
  type?: AppointmentType;
}

/** Randevu olusturma/duzenleme dialogu. */
@Component({
  selector: 'app-appointment-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    ButtonModule,
    DialogModule,
    DatePickerModule,
    SelectModule,
    SelectButtonModule,
    TextareaModule,
    InputTextModule,
    ColorPickerModule,
    TranslocoPipe,
    PatientSearchSelectComponent,
  ],
  templateUrl: './appointment-dialog.component.html',
})
export class AppointmentDialogComponent {
  private readonly appointmentsApi = inject(AppointmentsApiService);
  private readonly clinicContext = inject(ClinicContext);
  private readonly messageService = inject(MessageService);
  private readonly transloco = inject(TranslocoService);
  private readonly fb = inject(FormBuilder);

  /** null -> kapali. */
  readonly state = input.required<AppointmentDialogState | null>();
  readonly doctors = input.required<DoctorDto[]>();
  readonly saved = output<AppointmentDto>();
  readonly closed = output<void>();

  protected readonly AppointmentType = AppointmentType;
  protected readonly saving = signal(false);

  protected readonly typeOptions = [
    { labelKey: 'calendar.type.normal', value: AppointmentType.Normal },
    { labelKey: 'calendar.type.control', value: AppointmentType.Control },
    { labelKey: 'calendar.type.emptySlot', value: AppointmentType.EmptySlot },
    { labelKey: 'calendar.type.blocked', value: AppointmentType.Blocked },
  ];

  protected readonly durationOptions = APPOINTMENT_DURATIONS.map((m) => ({
    label: `${m} dk`,
    value: m,
  }));

  protected readonly form = this.fb.group({
    type: this.fb.nonNullable.control<AppointmentType>(AppointmentType.Normal),
    patient: this.fb.control<PatientOption | null>(null),
    doctorUserId: this.fb.control<number | null>(null, [Validators.required]),
    start: this.fb.control<Date | null>(null, [Validators.required]),
    durationMinutes: this.fb.nonNullable.control(30),
    title: this.fb.control<string | null>(null),
    note: this.fb.control<string | null>(null),
    color: this.fb.control<string | null>(null),
  });

  protected readonly typeValue = toSignal(this.form.controls.type.valueChanges, {
    initialValue: this.form.controls.type.value,
  });

  /** Bos slot/blokajda hasta gerekmez. */
  protected readonly patientRequired = computed(
    () =>
      this.typeValue() !== AppointmentType.EmptySlot &&
      this.typeValue() !== AppointmentType.Blocked,
  );

  protected readonly visible = computed(() => this.state() !== null);
  protected readonly isEdit = computed(() => this.state()?.mode === 'edit');

  constructor() {
    effect(() => {
      const state = this.state();
      if (state) {
        untracked(() => this.initFromState(state));
      }
    });
    this.form.controls.type.valueChanges.subscribe((type) => {
      const patientControl = this.form.controls.patient;
      if (type === AppointmentType.EmptySlot || type === AppointmentType.Blocked) {
        patientControl.clearValidators();
      } else {
        patientControl.setValidators([Validators.required]);
      }
      patientControl.updateValueAndValidity({ emitEvent: false });
    });
  }

  private initFromState(state: AppointmentDialogState): void {
    const appointment = state.appointment;
    if (state.mode === 'edit' && appointment) {
      const start = parseUtc(appointment.startUtc);
      const end = parseUtc(appointment.endUtc);
      this.form.reset({
        type: appointment.type,
        patient:
          appointment.patientId != null
            ? {
                id: appointment.patientId,
                label: appointment.patientName ?? `#${appointment.patientId}`,
                fileNo: '',
                phone: null,
              }
            : null,
        doctorUserId: appointment.doctorUserId,
        start,
        durationMinutes: Math.max(15, Math.round((end.getTime() - start.getTime()) / 60_000)),
        title: appointment.title,
        note: appointment.note,
        color: appointment.color,
      });
    } else {
      const start = state.start ?? new Date();
      const end = state.end;
      const duration = end
        ? Math.max(15, Math.round((end.getTime() - start.getTime()) / 60_000))
        : 30;
      const doctorId = state.doctorUserId ?? this.doctors()[0]?.id ?? null;
      this.form.reset({
        type: state.type ?? AppointmentType.Normal,
        patient: state.patient ?? null,
        doctorUserId: doctorId,
        start,
        durationMinutes: duration,
        title: null,
        note: null,
        color: null,
      });
    }
    this.form.controls.type.updateValueAndValidity();
  }

  protected doctorColor(id: number | null): string {
    return this.doctors().find((d) => d.id === id)?.color ?? '#3b82f6';
  }

  protected save(): void {
    const state = this.state();
    const clinicId = this.clinicContext.clinicId();
    if (!state) {
      return;
    }
    // Klinik baglami yoksa dugme sessizce hicbir sey yapiyordu (inceleme bulgusu).
    if (clinicId == null) {
      this.messageService.add({
        severity: 'warn',
        summary: this.transloco.translate('calendar.noClinicContext'),
        life: 5000,
      });
      return;
    }
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      this.messageService.add({
        severity: 'warn',
        summary: this.transloco.translate('validation.formInvalid'),
        life: 4000,
      });
      return;
    }
    const v = this.form.getRawValue();
    const start = v.start as Date;
    const end = new Date(start.getTime() + v.durationMinutes * 60_000);
    const request: AppointmentUpsertRequest = {
      clinicId,
      doctorUserId: v.doctorUserId as number,
      startUtc: toUtcIso(start),
      endUtc: toUtcIso(end),
      type: v.type,
      patientId: this.patientRequired() ? (v.patient?.id ?? null) : null,
      title: v.title,
      note: v.note,
      color: v.color ? this.normalizeColor(v.color) : null,
      sourceTreatmentRecordId: state.appointment?.sourceTreatmentRecordId ?? null,
      rowVersion: state.appointment?.rowVersion ?? null,
    };
    this.saving.set(true);
    const call =
      state.mode === 'edit' && state.appointment
        ? this.appointmentsApi.update(state.appointment.id, request)
        : this.appointmentsApi.create(request);
    call.subscribe({
      next: (appointment) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'success',
          summary: this.transloco.translate(
            state.mode === 'edit' ? 'calendar.updateSuccess' : 'calendar.createSuccess',
          ),
          life: 3000,
        });
        this.saved.emit(appointment);
      },
      error: () => this.saving.set(false),
    });
  }

  /** p-colorpicker '#' onekini atlayabilir; API ^#hex6 bekler. */
  private normalizeColor(color: string): string {
    return color.startsWith('#') ? color : `#${color}`;
  }
}
