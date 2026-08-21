import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin, map, mergeMap, of, toArray } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { SelectModule } from 'primeng/select';
import { SelectButtonModule } from 'primeng/selectbutton';
import { StepperModule } from 'primeng/stepper';
import { TextareaModule } from 'primeng/textarea';
import { MessageService } from 'primeng/api';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { MessagingApiService } from '../../../core/api/messaging-api.service';
import { PatientsApiService } from '../../../core/api/patients-api.service';
import { AppointmentsApiService } from '../../../core/api/appointments-api.service';
import {
  BulkMessageResult,
  MessageChannel,
  MessageKind,
  MessageTemplateDto,
} from '../../../core/api/messaging-api.models';
import {
  AppointmentStatus,
  ConsentType,
  DoctorDto,
  PatientDto,
  PatientListItemDto,
  toDateOnly,
} from '../../../core/api/api.models';
import { injectTranslationSignal } from '../../../shared/utils/transloco-signal';
import { toUtcNaive } from '../../calendar/appointment-utils';
import { CHANNEL_LABEL_KEYS, MESSAGE_CHANNELS } from '../messaging-options';
import { smsParts } from '../sms-parts';

/** Hasta/izin onbellegi tek seferde bu kadar kayda kadar cekilir. */
const AUDIENCE_FETCH_LIMIT = 500;
const PATIENT_PAGE_SIZE = 100;
const CONSENT_CONCURRENCY = 6;

/** Randevu filtresi icin tarih verilmediginde taranan pencere. */
const WIDE_WINDOW_YEARS_BACK = 5;
const WIDE_WINDOW_YEARS_FORWARD = 1;

interface DebtOption {
  labelKey: string;
  value: boolean | null;
}

/**
 * Toplu gonderim sihirbazi (3 adim):
 * 1) hedef kitle filtresi + canli hedef sayisi ve izinsiz uyarisi,
 * 2) sablon secimi + ornek hasta ile onizleme,
 * 3) zamanlama + onay -> hedeflenen/kuyruga alinan/atlanan sonucu.
 *
 * Hedef sayisi arka uctaki `ResolveAudienceAsync` ile ayni kurallarla istemcide hesaplanir
 * (arka uc onizleme ucu sunmuyor); gercek sonuc gonderim yanitindan okunur.
 */
@Component({
  selector: 'app-bulk-send',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    ButtonModule,
    DatePickerModule,
    SelectModule,
    SelectButtonModule,
    StepperModule,
    TextareaModule,
    TranslocoPipe,
  ],
  templateUrl: './bulk-send.component.html',
})
export class BulkSendComponent implements OnInit {
  private readonly api = inject(MessagingApiService);
  private readonly patientsApi = inject(PatientsApiService);
  private readonly appointmentsApi = inject(AppointmentsApiService);
  private readonly transloco = inject(TranslocoService);
  private readonly messageService = inject(MessageService);
  private readonly translation = injectTranslationSignal();

  protected readonly MessageKind = MessageKind;

  protected readonly step = signal(1);

  // --- Adim 1: hedef kitle --------------------------------------------------
  protected readonly lastVisitFrom = signal<Date | null>(null);
  protected readonly lastVisitTo = signal<Date | null>(null);
  protected readonly doctorUserId = signal<number | null>(null);
  protected readonly hasDebt = signal<boolean | null>(null);
  protected readonly birthMonth = signal<number | null>(null);

  private readonly patients = signal<PatientListItemDto[]>([]);
  private readonly consents = signal<Record<number, boolean>>({});
  /** Randevu filtresine uyan hasta kimlikleri; filtre yoksa null (kisit uygulanmaz). */
  private readonly visitMatchedIds = signal<Set<number> | null>(null);
  protected readonly audienceLoading = signal(false);
  protected readonly consentsLoaded = signal(false);
  protected readonly doctors = signal<DoctorDto[]>([]);

  // --- Adim 2: sablon -------------------------------------------------------
  protected readonly templates = signal<MessageTemplateDto[]>([]);
  protected readonly templateKey = signal<string | null>(null);
  protected readonly channel = signal<MessageChannel>(MessageChannel.Sms);
  protected readonly bodyOverride = signal<string>('');

  // --- Adim 3: zamanlama ----------------------------------------------------
  protected readonly scheduleMode = signal<'now' | 'later'>('now');
  protected readonly scheduledAt = signal<Date | null>(null);
  protected readonly sending = signal(false);
  protected readonly result = signal<BulkMessageResult | null>(null);

  protected readonly debtOptions = computed<DebtOption[]>(() => {
    this.translation();
    return [
      { labelKey: this.transloco.translate('common.all'), value: null },
      { labelKey: this.transloco.translate('messaging.bulk.debtYes'), value: true },
      { labelKey: this.transloco.translate('messaging.bulk.debtNo'), value: false },
    ];
  });

  protected readonly monthOptions = computed(() => {
    this.translation();
    return Array.from({ length: 12 }, (_, i) => ({
      label: this.transloco.translate('messaging.bulk.month.' + (i + 1)),
      value: i + 1,
    }));
  });

  protected readonly doctorOptions = computed(() =>
    this.doctors().map((d) => ({ label: `${d.firstName} ${d.lastName}`, value: d.id })),
  );

  protected readonly channelOptions = computed(() => {
    this.translation();
    return MESSAGE_CHANNELS.map((value) => ({
      label: this.transloco.translate(CHANNEL_LABEL_KEYS[value]),
      value,
    }));
  });

  /** Secilebilir sablon anahtarlari (kanal + aktiflik suzgeciyle). */
  protected readonly templateOptions = computed(() => {
    const keys = new Map<string, MessageTemplateDto>();
    for (const t of this.templates()) {
      if (!t.isActive) {
        continue;
      }
      if (!keys.has(t.templateKey) || t.locale === 'tr') {
        keys.set(t.templateKey, t);
      }
    }
    return [...keys.entries()].map(([key, t]) => ({ label: key, value: key, template: t }));
  });

  protected readonly selectedTemplate = computed(() => {
    const key = this.templateKey();
    if (!key) {
      return null;
    }
    const forChannel = this.templates().filter(
      (t) => t.templateKey === key && t.channel === this.channel(),
    );
    const pool = forChannel.length ? forChannel : this.templates().filter((t) => t.templateKey === key);
    return pool.find((t) => t.locale === 'tr') ?? pool[0] ?? null;
  });

  protected readonly selectedKind = computed(
    () => this.selectedTemplate()?.kind ?? MessageKind.Commercial,
  );

  /** Arka uctaki ResolveAudienceAsync kurallarinin istemci karsiligi. */
  protected readonly targetPatients = computed<PatientListItemDto[]>(() => {
    const debt = this.hasDebt();
    const month = this.birthMonth();
    const matched = this.visitMatchedIds();
    return this.patients().filter((p) => {
      if (debt === true && !(p.balance > 0)) {
        return false;
      }
      if (debt === false && p.balance > 0) {
        return false;
      }
      if (month != null) {
        if (!p.birthDate) {
          return false;
        }
        if (Number(p.birthDate.slice(5, 7)) !== month) {
          return false;
        }
      }
      if (matched && !matched.has(p.id)) {
        return false;
      }
      return true;
    });
  });

  protected readonly targetCount = computed(() => this.targetPatients().length);

  /** Ticari gonderimde izni olmadigi icin atlanacak hasta sayisi. */
  protected readonly noConsentCount = computed(() => {
    if (!this.consentsLoaded()) {
      return null;
    }
    const map = this.consents();
    return this.targetPatients().filter((p) => !map[p.id]).length;
  });

  /** Telefonu olmadigi icin atlanacak hasta sayisi. */
  protected readonly noPhoneCount = computed(
    () => this.targetPatients().filter((p) => !p.phone).length,
  );

  /** Ornek hasta verisiyle render edilmis onizleme. */
  protected readonly preview = computed(() => {
    const body = this.effectiveBody();
    const sample = this.targetPatients()[0];
    const name = sample ? `${sample.firstName} ${sample.lastName}` : 'Ayşe Yılmaz';
    const balance = sample
      ? new Intl.NumberFormat('tr-TR', { minimumFractionDigits: 2 }).format(sample.balance)
      : '1.250,00';
    const now = new Date();
    const replacements: Record<string, string> = {
      '{hasta_adi}': name,
      '{randevu_tarihi}': toDateOnly(now).split('-').reverse().join('.'),
      '{randevu_saati}': '14:30',
      '{klinik_adi}': this.transloco.translate('app.name'),
      '{bakiye}': `${balance} ₺`,
      '{odeme_linki}': 'https://ode.me/abc123',
      '{onam_linki}': 'https://onam.me/xyz789',
      '{hekim_adi}': this.doctors()[0]
        ? `${this.doctors()[0].firstName} ${this.doctors()[0].lastName}`
        : 'Dr. Mehmet Demir',
    };
    return Object.entries(replacements).reduce(
      (text, [token, value]) => text.split(token).join(value),
      body,
    );
  });

  protected readonly previewParts = computed(() => smsParts(this.preview()));

  protected readonly canGoStep2 = computed(() => this.targetCount() > 0);
  protected readonly canGoStep3 = computed(() => !!this.templateKey());

  ngOnInit(): void {
    this.loadPatients();
    this.api.templates(false).subscribe({
      next: (items) => this.templates.set(items),
      error: () => this.templates.set([]),
    });
    this.appointmentsApi.doctors().subscribe({
      next: (items) => this.doctors.set(items),
      error: () => this.doctors.set([]),
    });
  }

  protected effectiveBody(): string {
    const override = this.bodyOverride().trim();
    return override || this.selectedTemplate()?.body || '';
  }

  // --- Hedef kitle onbellegi ------------------------------------------------

  private loadPatients(): void {
    this.audienceLoading.set(true);
    this.patientsApi.list({ page: 1, pageSize: PATIENT_PAGE_SIZE }).subscribe({
      next: (first) => {
        const pageCount = Math.min(
          Math.ceil(first.totalCount / PATIENT_PAGE_SIZE) || 1,
          Math.ceil(AUDIENCE_FETCH_LIMIT / PATIENT_PAGE_SIZE),
        );
        if (pageCount <= 1) {
          this.patients.set(first.items);
          this.audienceLoading.set(false);
          this.loadConsents(first.items);
          return;
        }
        const rest = Array.from({ length: pageCount - 1 }, (_, i) =>
          this.patientsApi.list({ page: i + 2, pageSize: PATIENT_PAGE_SIZE }),
        );
        forkJoin(rest).subscribe({
          next: (pages) => {
            const all = [first, ...pages].flatMap((p) => p.items);
            this.patients.set(all);
            this.audienceLoading.set(false);
            this.loadConsents(all);
          },
          error: () => {
            this.patients.set(first.items);
            this.audienceLoading.set(false);
          },
        });
      },
      error: () => this.audienceLoading.set(false),
    });
  }

  /** Ticari izin (IYS) durumu hasta detayindan okunur; liste ucunda izin alani yok. */
  private loadConsents(patients: PatientListItemDto[]): void {
    if (patients.length === 0) {
      this.consentsLoaded.set(true);
      return;
    }
    of(...patients.map((p) => p.id))
      .pipe(
        mergeMap(
          (id) =>
            this.patientsApi.get(id).pipe(
              map((detail: PatientDto) => ({ id, granted: this.hasMarketingConsent(detail) })),
            ),
          CONSENT_CONCURRENCY,
        ),
        toArray(),
      )
      .subscribe({
        next: (entries) => {
          this.consents.set(
            entries.reduce<Record<number, boolean>>((acc, e) => {
              acc[e.id] = e.granted;
              return acc;
            }, {}),
          );
          this.consentsLoaded.set(true);
        },
        error: () => this.consentsLoaded.set(false),
      });
  }

  private hasMarketingConsent(patient: PatientDto): boolean {
    const type =
      this.channel() === MessageChannel.WhatsApp
        ? ConsentType.WhatsApp
        : this.channel() === MessageChannel.Email
          ? ConsentType.Email
          : ConsentType.SmsMarketing;
    return patient.consents.some((c) => c.consentType === type && c.isGranted);
  }

  /** Randevu (son ziyaret / hekim) filtresi degisince eslesen hasta kimlikleri yenilenir. */
  protected refreshVisitFilter(): void {
    const from = this.lastVisitFrom();
    const to = this.lastVisitTo();
    const doctorId = this.doctorUserId();
    if (!from && !to && doctorId == null) {
      this.visitMatchedIds.set(null);
      return;
    }
    const start = from ?? new Date(new Date().getFullYear() - WIDE_WINDOW_YEARS_BACK, 0, 1);
    const endBase = to ?? new Date(new Date().getFullYear() + WIDE_WINDOW_YEARS_FORWARD, 11, 31);
    const end = new Date(endBase);
    end.setHours(23, 59, 59, 0);

    this.audienceLoading.set(true);
    this.appointmentsApi
      .list({
        from: toUtcNaive(start),
        to: toUtcNaive(end),
        doctorIds: doctorId != null ? [doctorId] : null,
      })
      .subscribe({
        next: (appointments) => {
          const ids = new Set<number>();
          for (const a of appointments) {
            if (a.patientId != null && a.status !== AppointmentStatus.Cancelled) {
              ids.add(a.patientId);
            }
          }
          this.visitMatchedIds.set(ids);
          this.audienceLoading.set(false);
        },
        error: () => {
          this.visitMatchedIds.set(new Set());
          this.audienceLoading.set(false);
        },
      });
  }

  protected onChannelChange(channel: MessageChannel): void {
    this.channel.set(channel);
    // Izin turu kanala gore degisir: onbellek yeniden kurulur.
    this.consentsLoaded.set(false);
    this.loadConsents(this.patients());
  }

  protected clearAudience(): void {
    this.lastVisitFrom.set(null);
    this.lastVisitTo.set(null);
    this.doctorUserId.set(null);
    this.hasDebt.set(null);
    this.birthMonth.set(null);
    this.visitMatchedIds.set(null);
  }

  // --- Adim gecisleri -------------------------------------------------------

  protected goTo(step: number): void {
    this.step.set(step);
  }

  protected useTemplateBody(): void {
    this.bodyOverride.set('');
  }

  protected send(): void {
    const key = this.templateKey();
    if (!key) {
      return;
    }
    const scheduledAt =
      this.scheduleMode() === 'later' && this.scheduledAt()
        ? (this.scheduledAt() as Date).toISOString()
        : null;
    const override = this.bodyOverride().trim();

    this.sending.set(true);
    this.api
      .bulk({
        templateKey: key,
        channel: this.channel(),
        kind: this.selectedKind(),
        bodyOverride: override || null,
        scheduledAtUtc: scheduledAt,
        filter: {
          lastVisitFrom: this.lastVisitFrom() ? toDateOnly(this.lastVisitFrom() as Date) : null,
          lastVisitTo: this.lastVisitTo() ? toDateOnly(this.lastVisitTo() as Date) : null,
          doctorUserId: this.doctorUserId(),
          hasDebt: this.hasDebt(),
          birthMonth: this.birthMonth(),
        },
      })
      .subscribe({
        next: (result) => {
          this.sending.set(false);
          this.result.set(result);
          this.messageService.add({
            severity: 'success',
            summary: this.transloco.translate('messaging.bulk.queued'),
            life: 4000,
          });
        },
        error: () => this.sending.set(false),
      });
  }

  protected restart(): void {
    this.result.set(null);
    this.step.set(1);
    this.templateKey.set(null);
    this.bodyOverride.set('');
    this.scheduleMode.set('now');
    this.scheduledAt.set(null);
  }

  protected channelLabel(channel: MessageChannel): string {
    return this.transloco.translate(CHANNEL_LABEL_KEYS[channel] ?? 'messaging.channel.sms');
  }
}
