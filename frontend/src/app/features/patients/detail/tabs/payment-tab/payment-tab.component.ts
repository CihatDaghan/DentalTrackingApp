import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
  untracked,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { toSignal } from '@angular/core/rxjs-interop';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { DatePickerModule } from 'primeng/datepicker';
import { InputNumberModule } from 'primeng/inputnumber';
import { SelectButtonModule } from 'primeng/selectbutton';
import { TableModule } from 'primeng/table';
import { TextareaModule } from 'primeng/textarea';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmationService, MessageService } from 'primeng/api';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { FinanceApiService } from '../../../../../core/api/finance-api.service';
import { PaymentLinksApiService } from '../../../../../core/api/payment-links-api.service';
import { PaymentLinkDto } from '../../../../../core/api/messaging-api.models';
import {
  InstallmentDto,
  InstallmentStatus,
  LedgerEntryType,
  LedgerLineDto,
  LedgerStatementDto,
  PaymentMethod,
  PaymentPlanDto,
} from '../../../../../core/api/finance-api.models';
import { toDateOnly } from '../../../../../core/api/api.models';
import { HasPermissionDirective } from '../../../../../core/auth/has-permission.directive';
import { StatusTagComponent } from '../../../../../shared/components/status-tag/status-tag.component';
import { MoneyPipe } from '../../../../../shared/pipes/money.pipe';
import { TrDatePipe } from '../../../../../shared/pipes/tr-date.pipe';
import { PatientDetailStore } from '../../patient-detail.store';

interface MethodOption {
  labelKey: string;
  value: PaymentMethod;
  disabled?: boolean;
}

interface PlanPreviewRow {
  seqNo: number;
  dueDate: Date;
  amount: number;
}

/**
 * Odeme sekmesi: ozet kartlar + cari ekstre + tahsilat/indirim dialoglari + taksit planlari.
 * Her islem sonrasi ekstre ve basliktaki bakiye (PatientDetailStore.refresh) yenilenir.
 */
@Component({
  selector: 'app-payment-tab',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    ReactiveFormsModule,
    ButtonModule,
    DialogModule,
    DatePickerModule,
    InputNumberModule,
    SelectButtonModule,
    TableModule,
    TextareaModule,
    TooltipModule,
    TranslocoPipe,
    HasPermissionDirective,
    StatusTagComponent,
    MoneyPipe,
    TrDatePipe,
  ],
  templateUrl: './payment-tab.component.html',
})
export class PaymentTabComponent {
  private readonly store = inject(PatientDetailStore);
  private readonly financeApi = inject(FinanceApiService);
  private readonly paymentLinksApi = inject(PaymentLinksApiService);
  private readonly messageService = inject(MessageService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly transloco = inject(TranslocoService);
  private readonly fb = inject(FormBuilder);

  /** "Fatura Kes" butonunun sihirbaza tasidigi hasta kimligi. */
  protected readonly patientId = computed(() => this.store.patient()?.id ?? null);

  protected readonly LedgerEntryType = LedgerEntryType;
  protected readonly PaymentMethod = PaymentMethod;

  protected readonly statement = signal<LedgerStatementDto | null>(null);
  protected readonly loading = signal(false);
  protected readonly plans = signal<PaymentPlanDto[]>([]);

  protected readonly collectVisible = signal(false);
  protected readonly collectSaving = signal(false);
  protected readonly discountVisible = signal(false);
  protected readonly discountSaving = signal(false);
  protected readonly planVisible = signal(false);
  protected readonly planSaving = signal(false);

  /** Uretilen odeme linki onay kutusu (link metni + kopyala + SMS durumu). */
  protected readonly linkVisible = signal(false);
  protected readonly paymentLink = signal<PaymentLinkDto | null>(null);
  protected readonly linkCopied = signal(false);

  /** Odeme Linki secilirse tahsilat degil, POST /payment-links cagrilir (G asamasi). */
  protected readonly methodOptions: MethodOption[] = [
    { labelKey: 'paymentMethod.cash', value: PaymentMethod.Cash },
    { labelKey: 'paymentMethod.creditCardPos', value: PaymentMethod.CreditCardPos },
    { labelKey: 'paymentMethod.bankTransfer', value: PaymentMethod.BankTransfer },
    { labelKey: 'paymentMethod.check', value: PaymentMethod.Check },
    { labelKey: 'paymentMethod.onlineLink', value: PaymentMethod.OnlineLink },
  ];

  protected readonly collectForm = this.fb.group({
    amount: this.fb.control<number | null>(null, [Validators.required, Validators.min(0.01)]),
    method: this.fb.nonNullable.control<PaymentMethod>(PaymentMethod.Cash),
    installmentCount: this.fb.control<number | null>(null),
    note: this.fb.control<string | null>(null),
  });

  protected readonly discountForm = this.fb.group({
    amount: this.fb.control<number | null>(null, [Validators.required, Validators.min(0.01)]),
    description: this.fb.control<string | null>(null),
  });

  protected readonly planForm = this.fb.group({
    totalAmount: this.fb.control<number | null>(null, [Validators.required, Validators.min(0.01)]),
    installmentCount: this.fb.control<number | null>(3, [
      Validators.required,
      Validators.min(1),
      Validators.max(36),
    ]),
    startDate: this.fb.control<Date | null>(null, [Validators.required]),
    note: this.fb.control<string | null>(null),
  });

  private readonly collectMethod = toSignal(this.collectForm.controls.method.valueChanges, {
    initialValue: this.collectForm.controls.method.value,
  });
  /** Taksit sayisi alani yalniz kredi kartinda gosterilir. */
  protected readonly showInstallments = computed(
    () => this.collectMethod() === PaymentMethod.CreditCardPos,
  );

  private readonly planFormValue = toSignal(this.planForm.valueChanges, {
    initialValue: this.planForm.getRawValue(),
  });

  /** Esit bolusum onizlemesi: kurus farki son taksite yazilir. */
  protected readonly planPreview = computed<PlanPreviewRow[]>(() => {
    const v = this.planFormValue();
    const total = v?.totalAmount ?? 0;
    const count = v?.installmentCount ?? 0;
    const start = v?.startDate;
    if (!total || !count || count < 1 || count > 36 || !start) {
      return [];
    }
    const base = Math.floor((total / count) * 100) / 100;
    const rows: PlanPreviewRow[] = [];
    for (let i = 0; i < count; i++) {
      const due = new Date(start);
      due.setMonth(due.getMonth() + i);
      rows.push({
        seqNo: i + 1,
        dueDate: due,
        amount: i === count - 1 ? Math.round((total - base * (count - 1)) * 100) / 100 : base,
      });
    }
    return rows;
  });

  constructor() {
    effect(() => {
      const patient = this.store.patient();
      if (patient) {
        untracked(() => this.reload(patient.id));
      }
    });
  }

  protected reload(patientId?: number): void {
    const id = patientId ?? this.store.patient()?.id;
    if (!id) {
      return;
    }
    this.loading.set(true);
    this.financeApi.patientLedger(id).subscribe({
      next: (statement) => {
        this.statement.set(statement);
        this.loading.set(false);
      },
      error: () => {
        this.statement.set(null);
        this.loading.set(false);
      },
    });
    this.financeApi.patientPaymentPlans(id).subscribe({
      next: (plans) => this.plans.set(plans),
      error: () => this.plans.set([]),
    });
  }

  /** Islem sonrasi: ekstre + planlar + basliktaki bakiye. */
  private afterMutation(successKey: string): void {
    this.messageService.add({
      severity: 'success',
      summary: this.transloco.translate(successKey),
      life: 3000,
    });
    this.reload();
    this.store.refresh();
  }

  // --- Tahsilat Al ----------------------------------------------------------

  protected openCollect(): void {
    this.collectForm.reset({ amount: null, method: PaymentMethod.Cash, installmentCount: null, note: null });
    this.collectVisible.set(true);
  }

  protected saveCollect(): void {
    const patientId = this.store.patient()?.id;
    if (!patientId || this.collectForm.invalid) {
      this.collectForm.markAllAsTouched();
      return;
    }
    const v = this.collectForm.getRawValue();
    this.collectSaving.set(true);

    // Odeme Linki: tahsilat degil, hastaya SMS/WhatsApp ile gonderilen bir link uretilir.
    if (v.method === PaymentMethod.OnlineLink) {
      this.paymentLinksApi
        .create({
          patientId,
          amount: v.amount as number,
          description: v.note,
        })
        .subscribe({
          next: (link) => {
            this.collectSaving.set(false);
            this.collectVisible.set(false);
            this.paymentLink.set(link);
            this.linkCopied.set(false);
            this.linkVisible.set(true);
          },
          error: () => this.collectSaving.set(false),
        });
      return;
    }

    this.financeApi
      .createPayment({
        patientId,
        amount: v.amount as number,
        method: v.method,
        installmentCount: v.method === PaymentMethod.CreditCardPos ? v.installmentCount : null,
        note: v.note,
      })
      .subscribe({
        next: () => {
          this.collectSaving.set(false);
          this.collectVisible.set(false);
          this.afterMutation('payment.collectSuccess');
        },
        error: () => this.collectSaving.set(false),
      });
  }

  /** Link metnini panoya kopyalar (clipboard yoksa sessizce gecer). */
  protected copyLink(): void {
    const url = this.paymentLink()?.linkUrl;
    if (!url) {
      return;
    }
    void navigator.clipboard?.writeText(url).then(
      () => {
        this.linkCopied.set(true);
        this.messageService.add({
          severity: 'success',
          summary: this.transloco.translate('payment.link.copied'),
          life: 2000,
        });
      },
      () => undefined,
    );
  }

  // --- Indirim --------------------------------------------------------------

  protected openDiscount(): void {
    this.discountForm.reset({ amount: null, description: null });
    this.discountVisible.set(true);
  }

  protected saveDiscount(): void {
    const patientId = this.store.patient()?.id;
    if (!patientId || this.discountForm.invalid) {
      this.discountForm.markAllAsTouched();
      return;
    }
    const v = this.discountForm.getRawValue();
    this.discountSaving.set(true);
    this.financeApi
      .applyDiscount({ patientId, amount: v.amount as number, description: v.description })
      .subscribe({
        next: () => {
          this.discountSaving.set(false);
          this.discountVisible.set(false);
          this.afterMutation('payment.discountSuccess');
        },
        error: () => this.discountSaving.set(false),
      });
  }

  // --- Taksit plani ---------------------------------------------------------

  protected openPlan(): void {
    const start = new Date();
    start.setMonth(start.getMonth() + 1);
    this.planForm.reset({ totalAmount: null, installmentCount: 3, startDate: start, note: null });
    this.planVisible.set(true);
  }

  protected savePlan(): void {
    const patientId = this.store.patient()?.id;
    if (!patientId || this.planForm.invalid) {
      this.planForm.markAllAsTouched();
      return;
    }
    const v = this.planForm.getRawValue();
    this.planSaving.set(true);
    this.financeApi
      .createPaymentPlan({
        patientId,
        totalAmount: v.totalAmount as number,
        installmentCount: v.installmentCount as number,
        startDate: toDateOnly(v.startDate as Date),
        note: v.note,
      })
      .subscribe({
        next: () => {
          this.planSaving.set(false);
          this.planVisible.set(false);
          this.afterMutation('payment.planSuccess');
        },
        error: () => this.planSaving.set(false),
      });
  }

  protected deletePlan(plan: PaymentPlanDto): void {
    this.confirmation.confirm({
      header: this.transloco.translate('payment.deletePlanTitle'),
      message: this.transloco.translate('payment.deletePlanMessage'),
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
        this.financeApi.deletePaymentPlan(plan.id).subscribe({
          next: () => this.afterMutation('payment.deletePlanSuccess'),
        });
      },
    });
  }

  // --- Tahsilat silme -------------------------------------------------------

  /** Yalniz tahsilat satirlari (refType=Payment) silinebilir. */
  protected canDeleteLine(line: LedgerLineDto): boolean {
    return (
      line.entryType === LedgerEntryType.PaymentIn &&
      line.refType === 'Payment' &&
      line.refId != null
    );
  }

  protected deletePayment(line: LedgerLineDto): void {
    if (!this.canDeleteLine(line)) {
      return;
    }
    this.confirmation.confirm({
      header: this.transloco.translate('payment.deletePaymentTitle'),
      message: this.transloco.translate('payment.deletePaymentMessage'),
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
        this.financeApi.deletePayment(line.refId as number).subscribe({
          next: () => this.afterMutation('payment.deletePaymentSuccess'),
        });
      },
    });
  }

  /** Vadesi gecmis Bekliyor/Kismi taksitler Gecikmis olarak sunulur. */
  protected effectiveInstallmentStatus(inst: InstallmentDto): number {
    if (
      (inst.status === InstallmentStatus.Pending || inst.status === InstallmentStatus.Partial) &&
      inst.dueDate < toDateOnly(new Date())
    ) {
      return InstallmentStatus.Overdue;
    }
    return inst.status;
  }
}
