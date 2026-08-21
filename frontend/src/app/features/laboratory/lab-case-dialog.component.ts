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
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { MessageService } from 'primeng/api';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { LaboratoryApiService } from '../../core/api/laboratory-api.service';
import {
  LAB_MATERIAL_KEYS,
  LAB_WORK_TYPE_KEYS,
  LabCaseDto,
  LaboratoryDto,
  VITA_SHADES,
} from '../../core/api/laboratory-api.models';
import { AppointmentsApiService } from '../../core/api/appointments-api.service';
import { DoctorDto, fromDateOnly, toDateOnly } from '../../core/api/api.models';
import { AuthStore } from '../../core/auth/auth.store';
import { UserType } from '../../core/api/auth-api.models';
import { ClinicContext } from '../../core/auth/clinic-context';
import {
  PatientOption,
  PatientSearchSelectComponent,
} from '../../shared/components/patient-search-select/patient-search-select.component';
import { injectTranslationSignal } from '../../shared/utils/transloco-signal';

/**
 * Lab isi olustur/duzenle dialogu — hem hasta kartindaki Laboratuvar sekmesi
 * hem de klinik geneli /app/laboratory sayfasi tarafindan kullanilir.
 * `patientId` verilirse hasta secici gizlenir (hasta kartindan aciliş).
 */
@Component({
  selector: 'app-lab-case-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    ButtonModule,
    DatePickerModule,
    DialogModule,
    InputNumberModule,
    InputTextModule,
    SelectModule,
    TextareaModule,
    TranslocoPipe,
    PatientSearchSelectComponent,
  ],
  templateUrl: './lab-case-dialog.component.html',
})
export class LabCaseDialogComponent {
  private readonly api = inject(LaboratoryApiService);
  private readonly appointmentsApi = inject(AppointmentsApiService);
  private readonly authStore = inject(AuthStore);
  private readonly clinicContext = inject(ClinicContext);
  private readonly messageService = inject(MessageService);
  private readonly transloco = inject(TranslocoService);

  readonly visible = input(false);
  readonly visibleChange = output<boolean>();
  /** Hasta kartindan acildiginda sabit hasta; null ise dialogda hasta secilir. */
  readonly patientId = input<number | null>(null);
  readonly editing = input<LabCaseDto | null>(null);
  readonly saved = output<LabCaseDto>();

  /** Ceviriler yuklendiginde/dil degistiginde secenek listeleri yeniden hesaplansin. */
  private readonly translation = injectTranslationSignal();

  protected readonly shades: string[] = [...VITA_SHADES];
  protected readonly saving = signal(false);
  protected readonly laboratories = signal<LaboratoryDto[]>([]);
  protected readonly doctors = signal<DoctorDto[]>([]);

  protected readonly patient = signal<PatientOption | null>(null);
  protected readonly laboratoryId = signal<number | null>(null);
  protected readonly doctorId = signal<number | null>(null);
  protected readonly workType = signal('');
  protected readonly teethText = signal('');
  protected readonly shade = signal<string | null>(null);
  protected readonly material = signal<string | null>(null);
  protected readonly sentDate = signal<Date | null>(new Date());
  protected readonly dueDate = signal<Date | null>(null);
  protected readonly price = signal<number | null>(null);
  protected readonly note = signal('');

  protected readonly doctorLocked = computed(
    () => this.authStore.user()?.userType === UserType.Dentist,
  );

  protected readonly workTypeOptions = computed(() => {
    this.translation();
    return LAB_WORK_TYPE_KEYS.map((key) => this.transloco.translate('laboratory.workType.' + key));
  });

  protected readonly materialOptions = computed(() => {
    this.translation();
    return LAB_MATERIAL_KEYS.map((key) => this.transloco.translate('laboratory.material.' + key));
  });

  protected readonly laboratoryOptions = computed(() =>
    this.laboratories().map((l) => ({ label: l.name, value: l.id })),
  );

  protected readonly doctorOptions = computed(() =>
    this.doctors().map((d) => ({ label: `${d.firstName} ${d.lastName}`, value: d.id })),
  );

  /** "11, 12,21" -> ["11","12","21"] (onizleme chip'leri + gonderilecek CSV). */
  protected readonly teeth = computed(() =>
    this.teethText()
      .split(/[,\s]+/)
      .map((t) => t.trim())
      .filter((t) => t.length > 0),
  );

  protected readonly canSave = computed(
    () =>
      this.laboratoryId() != null &&
      this.doctorId() != null &&
      this.workType().trim().length > 0 &&
      (this.patientId() != null || this.patient() != null),
  );

  constructor() {
    // Dialog her acilista formu tazeler (duzenlemede mevcut vakayi doldurur).
    effect(() => {
      if (this.visible()) {
        untracked(() => this.reset());
      }
    });

    this.api.laboratories().subscribe({
      next: (labs) => {
        this.laboratories.set(labs);
        if (this.laboratoryId() == null) {
          this.laboratoryId.set(labs[0]?.id ?? null);
        }
      },
      error: () => this.laboratories.set([]),
    });

    this.appointmentsApi.doctors().subscribe({
      next: (doctors) => {
        this.doctors.set(doctors);
        if (this.doctorId() == null) {
          const user = this.authStore.user();
          const own = user ? doctors.find((d) => d.id === user.id) : undefined;
          this.doctorId.set(own?.id ?? doctors[0]?.id ?? null);
        }
      },
      error: () => this.doctors.set([]),
    });
  }

  /** Yeniden yuklenmesi gerekebilecek lab firmasi listesini disaridan tazelemek icin. */
  reloadLaboratories(): void {
    this.api.laboratories().subscribe({ next: (labs) => this.laboratories.set(labs) });
  }

  private reset(): void {
    const editing = this.editing();
    if (editing) {
      this.patient.set({
        id: editing.patientId,
        label: editing.patientName,
        fileNo: '',
        phone: null,
      });
      this.laboratoryId.set(editing.laboratoryId);
      this.doctorId.set(editing.doctorUserId);
      this.workType.set(editing.workType ?? '');
      this.teethText.set(editing.teethCsv ?? '');
      this.shade.set(editing.shade);
      this.material.set(editing.material);
      this.sentDate.set(fromDateOnly(editing.sentDate));
      this.dueDate.set(fromDateOnly(editing.dueDate));
      this.price.set(editing.price);
      this.note.set(editing.note ?? '');
      return;
    }
    this.patient.set(null);
    this.laboratoryId.set(this.laboratories()[0]?.id ?? null);
    const user = this.authStore.user();
    const own = user ? this.doctors().find((d) => d.id === user.id) : undefined;
    this.doctorId.set(own?.id ?? this.doctors()[0]?.id ?? null);
    this.workType.set('');
    this.teethText.set('');
    this.shade.set(null);
    this.material.set(null);
    this.sentDate.set(new Date());
    const due = new Date();
    due.setDate(due.getDate() + 7);
    this.dueDate.set(due);
    this.price.set(null);
    this.note.set('');
  }

  protected close(): void {
    this.visibleChange.emit(false);
  }

  protected save(): void {
    const patientId = this.patientId() ?? this.patient()?.id;
    const laboratoryId = this.laboratoryId();
    const doctorUserId = this.doctorId();
    if (patientId == null || laboratoryId == null || doctorUserId == null) {
      return;
    }
    const request = {
      patientId,
      doctorUserId,
      laboratoryId,
      workType: this.workType().trim(),
      teethCsv: this.teeth().join(',') || null,
      shade: this.shade() || null,
      material: this.material() || null,
      sentDate: this.sentDate() ? toDateOnly(this.sentDate()!) : null,
      dueDate: this.dueDate() ? toDateOnly(this.dueDate()!) : null,
      price: this.price(),
      note: this.note().trim() || null,
      clinicId: this.clinicContext.clinicId(),
    };
    this.saving.set(true);
    const editing = this.editing();
    const call = editing ? this.api.updateCase(editing.id, request) : this.api.createCase(request);
    call.subscribe({
      next: (labCase) => {
        this.saving.set(false);
        this.messageService.add({
          severity: 'success',
          summary: this.transloco.translate('laboratory.saveSuccess'),
          life: 3000,
        });
        this.saved.emit(labCase);
        this.visibleChange.emit(false);
      },
      error: () => this.saving.set(false),
    });
  }
}
