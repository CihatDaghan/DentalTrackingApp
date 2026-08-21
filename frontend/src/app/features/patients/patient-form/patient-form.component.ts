import { ChangeDetectionStrategy, Component, inject, input, OnInit } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { SelectModule } from 'primeng/select';
import { DatePickerModule } from 'primeng/datepicker';
import { SelectButtonModule } from 'primeng/selectbutton';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import {
  ConsentType,
  ConsentUpsertDto,
  fromDateOnly,
  Gender,
  IdentityType,
  PatientDto,
  PatientUpsertRequest,
  toDateOnly,
} from '../../../core/api/api.models';
import { tcknValidator } from '../../../shared/utils/tckn';
import { PhoneInputComponent } from '../../../shared/components/phone-input/phone-input.component';
import { TcknInputComponent } from '../../../shared/components/tckn-input/tckn-input.component';

/** Uyruk secenekleri (ISO 3166-1 alpha-3) — editable select, serbest kod da girilebilir. */
const NATIONALITIES = [
  'TUR', 'AZE', 'DEU', 'GBR', 'USA', 'RUS', 'UKR', 'IRQ', 'IRN', 'SAU',
  'KAZ', 'FRA', 'NLD', 'BGR', 'SYR', 'GEO', 'TKM', 'UZB', 'KWT', 'QAT',
];

/**
 * Hasta demografi formu — hem "Yeni Hasta" dialogu hem profil sekmesi kullanir.
 * Kimlik kurali: TCKN secilirse uyruk TUR'a kilitlenir; Pasaport secilirse
 * pasaport no + uyruk + son giris tarihi zorunlu olur (fatura istisna kodu 334).
 */
@Component({
  selector: 'app-patient-form',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    InputTextModule,
    TextareaModule,
    SelectModule,
    DatePickerModule,
    SelectButtonModule,
    ToggleSwitchModule,
    TranslocoPipe,
    PhoneInputComponent,
    TcknInputComponent,
  ],
  templateUrl: './patient-form.component.html',
})
export class PatientFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly transloco = inject(TranslocoService);

  /** Duzenleme modunda mevcut hasta; create'te null birakin. */
  readonly patient = input<PatientDto | null>(null);

  readonly form = this.fb.group({
    firstName: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(100)]),
    lastName: this.fb.nonNullable.control('', [Validators.required, Validators.maxLength(100)]),
    identityType: this.fb.nonNullable.control<IdentityType>(IdentityType.Tckn),
    tckn: this.fb.control<string | null>(null, [tcknValidator()]),
    passportNo: this.fb.control<string | null>(null),
    nationalityCode: this.fb.nonNullable.control('TUR', [
      Validators.required,
      Validators.minLength(3),
      Validators.maxLength(3),
    ]),
    lastEntryDate: this.fb.control<Date | null>(null),
    birthDate: this.fb.control<Date | null>(null),
    gender: this.fb.nonNullable.control<Gender>(Gender.Unknown),
    phone: this.fb.control<string | null>(null),
    phone2: this.fb.control<string | null>(null),
    email: this.fb.control<string | null>(null, [Validators.email]),
    address: this.fb.control<string | null>(null),
    city: this.fb.control<string | null>(null),
    district: this.fb.control<string | null>(null),
    referralSource: this.fb.control<string | null>(null),
    profession: this.fb.control<string | null>(null),
    generalNote: this.fb.control<string | null>(null),
    consentKvkk: this.fb.nonNullable.control(false),
    consentSmsInfo: this.fb.nonNullable.control(false),
    consentSmsMarketing: this.fb.nonNullable.control(false),
    consentWhatsApp: this.fb.nonNullable.control(false),
    consentEmail: this.fb.nonNullable.control(false),
  });

  protected readonly identityTypeValue = toSignal(this.form.controls.identityType.valueChanges, {
    initialValue: this.form.controls.identityType.value,
  });

  protected readonly IdentityType = IdentityType;
  protected readonly maxDate = new Date();

  protected readonly identityOptions = [
    { labelKey: 'patients.form.identityTckn', value: IdentityType.Tckn },
    { labelKey: 'patients.form.identityPassport', value: IdentityType.Passport },
  ];

  protected readonly genderOptions = [
    { labelKey: 'gender.unknown', value: Gender.Unknown },
    { labelKey: 'gender.male', value: Gender.Male },
    { labelKey: 'gender.female', value: Gender.Female },
  ];

  protected readonly nationalityOptions = NATIONALITIES.map((code) => ({ code }));

  protected readonly consentRows = [
    { control: 'consentSmsInfo' as const, labelKey: 'patients.consents.smsInfo' },
    { control: 'consentSmsMarketing' as const, labelKey: 'patients.consents.smsMarketing' },
    { control: 'consentWhatsApp' as const, labelKey: 'patients.consents.whatsApp' },
    { control: 'consentEmail' as const, labelKey: 'patients.consents.email' },
    { control: 'consentKvkk' as const, labelKey: 'patients.consents.kvkk' },
  ];

  ngOnInit(): void {
    this.form.controls.identityType.valueChanges.subscribe((type) =>
      this.applyIdentityRules(type),
    );
    const existing = this.patient();
    if (existing) {
      this.patchFrom(existing);
    }
    this.applyIdentityRules(this.form.controls.identityType.value);
  }

  hasPendingChanges(): boolean {
    return this.form.dirty;
  }

  patchFrom(patient: PatientDto): void {
    const consent = (type: ConsentType) =>
      patient.consents.find((c) => c.consentType === type)?.isGranted ?? false;
    this.form.reset({
      firstName: patient.firstName,
      lastName: patient.lastName,
      identityType: patient.identityType,
      tckn: patient.tckn,
      passportNo: patient.passportNo,
      nationalityCode: patient.nationalityCode || 'TUR',
      lastEntryDate: fromDateOnly(patient.lastEntryDate),
      birthDate: fromDateOnly(patient.birthDate),
      gender: patient.gender,
      phone: patient.phone,
      phone2: patient.phone2,
      email: patient.email,
      address: patient.address,
      city: patient.city,
      district: patient.district,
      referralSource: patient.referralSource,
      profession: patient.profession,
      generalNote: patient.generalNote,
      consentKvkk: consent(ConsentType.KvkkProcessing),
      consentSmsInfo: consent(ConsentType.SmsInfo),
      consentSmsMarketing: consent(ConsentType.SmsMarketing),
      consentWhatsApp: consent(ConsentType.WhatsApp),
      consentEmail: consent(ConsentType.Email),
    });
  }

  toRequest(): PatientUpsertRequest {
    const v = this.form.getRawValue();
    return {
      firstName: v.firstName.trim(),
      lastName: v.lastName.trim(),
      identityType: v.identityType,
      tckn: v.identityType === IdentityType.Tckn ? v.tckn : null,
      passportNo: v.identityType === IdentityType.Passport ? v.passportNo : null,
      nationalityCode:
        v.identityType === IdentityType.Tckn ? 'TUR' : v.nationalityCode.toUpperCase(),
      lastEntryDate:
        v.identityType === IdentityType.Passport && v.lastEntryDate
          ? toDateOnly(v.lastEntryDate)
          : null,
      birthDate: v.birthDate ? toDateOnly(v.birthDate) : null,
      gender: v.gender,
      phone: v.phone,
      phone2: v.phone2,
      email: v.email || null,
      address: v.address,
      city: v.city,
      district: v.district,
      referralSource: v.referralSource,
      profession: v.profession,
      generalNote: v.generalNote,
      consents: [
        { consentType: ConsentType.KvkkProcessing, isGranted: v.consentKvkk, source: 1 },
        { consentType: ConsentType.SmsInfo, isGranted: v.consentSmsInfo, source: 1 },
        { consentType: ConsentType.SmsMarketing, isGranted: v.consentSmsMarketing, source: 1 },
        { consentType: ConsentType.WhatsApp, isGranted: v.consentWhatsApp, source: 1 },
        { consentType: ConsentType.Email, isGranted: v.consentEmail, source: 1 },
      ] satisfies ConsentUpsertDto[],
    };
  }

  validationSummary(): string | null {
    if (this.form.valid) {
      return null;
    }
    this.form.markAllAsTouched();
    return this.transloco.translate('validation.formInvalid');
  }

  /** Kimlik tipine gore dinamik zorunluluklar. */
  private applyIdentityRules(type: IdentityType): void {
    const { passportNo, lastEntryDate, nationalityCode } = this.form.controls;
    if (type === IdentityType.Passport) {
      passportNo.setValidators([Validators.required, Validators.maxLength(20)]);
      lastEntryDate.setValidators([Validators.required]);
      if (nationalityCode.value === 'TUR') {
        nationalityCode.setValue('', { emitEvent: false });
      }
      nationalityCode.enable({ emitEvent: false });
    } else {
      passportNo.clearValidators();
      lastEntryDate.clearValidators();
      nationalityCode.setValue('TUR', { emitEvent: false });
      nationalityCode.disable({ emitEvent: false });
    }
    passportNo.updateValueAndValidity({ emitEvent: false });
    lastEntryDate.updateValueAndValidity({ emitEvent: false });
    nationalityCode.updateValueAndValidity({ emitEvent: false });
  }
}
