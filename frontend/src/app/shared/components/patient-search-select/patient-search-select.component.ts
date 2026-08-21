import {
  ChangeDetectionStrategy,
  Component,
  forwardRef,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import {
  ControlValueAccessor,
  FormBuilder,
  NG_VALUE_ACCESSOR,
  ReactiveFormsModule,
  FormsModule,
  Validators,
} from '@angular/forms';
import { AutoCompleteCompleteEvent, AutoCompleteModule } from 'primeng/autocomplete';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { TranslocoPipe } from '@jsverse/transloco';
import { PatientsApiService } from '../../../core/api/patients-api.service';
import { PhoneInputComponent } from '../phone-input/phone-input.component';

/** Secim degeri: arama listesinden ya da hizli kayittan gelen ozet. */
export interface PatientOption {
  id: number;
  label: string;
  fileNo: string;
  phone: string | null;
}

/**
 * Hasta arama-sec bileseni (p-autocomplete):
 * GET /patients?search=&pageSize=10, min 2 karakter, 300 ms debounce.
 * `[allowQuickCreate]` ile alt kisimdan "yeni hasta" hizli kaydi (ad+soyad+telefon).
 */
@Component({
  selector: 'app-patient-search-select',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    AutoCompleteModule,
    ButtonModule,
    DialogModule,
    InputTextModule,
    FormsModule,
    ReactiveFormsModule,
    TranslocoPipe,
    PhoneInputComponent,
  ],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => PatientSearchSelectComponent),
      multi: true,
    },
  ],
  templateUrl: './patient-search-select.component.html',
})
export class PatientSearchSelectComponent implements ControlValueAccessor {
  private readonly patientsApi = inject(PatientsApiService);
  private readonly fb = inject(FormBuilder);

  readonly allowQuickCreate = input(false);
  readonly inputId = input<string>('');
  readonly placeholderKey = input('patientSearch.placeholder');
  readonly selected = output<PatientOption | null>();

  protected readonly value = signal<PatientOption | null>(null);
  protected readonly disabled = signal(false);
  protected readonly suggestions = signal<PatientOption[]>([]);
  protected readonly quickCreateVisible = signal(false);
  protected readonly quickCreateSaving = signal(false);

  protected readonly quickForm = this.fb.nonNullable.group({
    firstName: ['', [Validators.required, Validators.maxLength(100)]],
    lastName: ['', [Validators.required, Validators.maxLength(100)]],
    phone: this.fb.control<string | null>(null),
  });

  private onChange: (value: PatientOption | null) => void = () => undefined;
  protected onTouched: () => void = () => undefined;

  writeValue(value: PatientOption | null): void {
    this.value.set(value);
  }

  registerOnChange(fn: (value: PatientOption | null) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
  }

  protected search(event: AutoCompleteCompleteEvent): void {
    const query = event.query.trim();
    if (query.length < 2) {
      this.suggestions.set([]);
      return;
    }
    this.patientsApi.list({ page: 1, pageSize: 10, search: query }).subscribe({
      next: (result) =>
        this.suggestions.set(
          result.items.map((p) => ({
            id: p.id,
            label: `${p.firstName} ${p.lastName}`,
            fileNo: p.fileNo,
            phone: p.phone,
          })),
        ),
      error: () => this.suggestions.set([]),
    });
  }

  protected onModelChange(value: PatientOption | string | null): void {
    // forceSelection'a ragmen serbest metin gecici olarak gelebilir; yalniz nesneyi kabul et.
    const option = value && typeof value === 'object' ? value : null;
    this.value.set(option);
    this.onChange(option);
    this.selected.emit(option);
  }

  protected openQuickCreate(): void {
    this.quickForm.reset({ firstName: '', lastName: '', phone: null });
    this.quickCreateVisible.set(true);
  }

  protected saveQuickCreate(): void {
    if (this.quickForm.invalid) {
      this.quickForm.markAllAsTouched();
      return;
    }
    const { firstName, lastName, phone } = this.quickForm.getRawValue();
    this.quickCreateSaving.set(true);
    this.patientsApi.create({ firstName, lastName, phone }).subscribe({
      next: (patient) => {
        const option: PatientOption = {
          id: patient.id,
          label: `${patient.firstName} ${patient.lastName}`,
          fileNo: patient.fileNo,
          phone: patient.phone,
        };
        this.value.set(option);
        this.onChange(option);
        this.selected.emit(option);
        this.quickCreateSaving.set(false);
        this.quickCreateVisible.set(false);
      },
      error: () => this.quickCreateSaving.set(false),
    });
  }
}
