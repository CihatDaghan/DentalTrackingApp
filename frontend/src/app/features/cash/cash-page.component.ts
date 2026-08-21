import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
  untracked,
} from '@angular/core';
import { Router } from '@angular/router';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { DatePickerModule } from 'primeng/datepicker';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TabsModule } from 'primeng/tabs';
import { TableModule } from 'primeng/table';
import { TextareaModule } from 'primeng/textarea';
import { TooltipModule } from 'primeng/tooltip';
import { CheckboxModule } from 'primeng/checkbox';
import { ConfirmationService, MessageService } from 'primeng/api';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { FinanceApiService } from '../../core/api/finance-api.service';
import {
  CashMovementKind,
  CashRegisterDailySummaryDto,
  CompanyDto,
  ExpenseCategoryDto,
  ExpenseDto,
  LedgerStatementDto,
  PaymentDto,
  PaymentMethod,
} from '../../core/api/finance-api.models';
import { toDateOnly } from '../../core/api/api.models';
import { HasPermissionDirective } from '../../core/auth/has-permission.directive';
import { StatusTagComponent } from '../../shared/components/status-tag/status-tag.component';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { TrDatePipe } from '../../shared/pipes/tr-date.pipe';

/**
 * Kasa sayfasi (/app/cash): gun secici + ozet kartlari (yontem bazli tahsilat,
 * gider, net) + hareket tablosu; Giderler ve Kurumlar alt sekmeleri.
 */
@Component({
  selector: 'app-cash-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    ReactiveFormsModule,
    ButtonModule,
    DialogModule,
    DatePickerModule,
    InputNumberModule,
    InputTextModule,
    SelectModule,
    TabsModule,
    TableModule,
    TextareaModule,
    TooltipModule,
    CheckboxModule,
    TranslocoPipe,
    HasPermissionDirective,
    StatusTagComponent,
    MoneyPipe,
    TrDatePipe,
  ],
  templateUrl: './cash-page.component.html',
})
export class CashPageComponent {
  private readonly financeApi = inject(FinanceApiService);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly transloco = inject(TranslocoService);
  private readonly fb = inject(FormBuilder);

  protected readonly PaymentMethod = PaymentMethod;

  protected readonly selectedDate = signal<Date>(new Date());
  protected readonly summary = signal<CashRegisterDailySummaryDto | null>(null);
  protected readonly loading = signal(false);
  /** Gunun tahsilatlari — hareketlerdeki hasta adini hasta kartina baglamak icin. */
  protected readonly dayPayments = signal<PaymentDto[]>([]);

  // Giderler
  protected readonly expenses = signal<ExpenseDto[]>([]);
  protected readonly expensesLoading = signal(false);
  protected readonly categories = signal<ExpenseCategoryDto[]>([]);
  protected readonly expenseVisible = signal(false);
  protected readonly expenseSaving = signal(false);

  // Kurumlar
  protected readonly companies = signal<CompanyDto[]>([]);
  protected readonly companiesLoading = signal(false);
  protected readonly companyVisible = signal(false);
  protected readonly companySaving = signal(false);
  protected readonly editingCompany = signal<CompanyDto | null>(null);
  protected readonly companyLedgerTarget = signal<CompanyDto | null>(null);
  protected readonly companyLedger = signal<LedgerStatementDto | null>(null);

  protected readonly methodOptions = [
    { labelKey: 'paymentMethod.cash', value: PaymentMethod.Cash },
    { labelKey: 'paymentMethod.creditCardPos', value: PaymentMethod.CreditCardPos },
    { labelKey: 'paymentMethod.bankTransfer', value: PaymentMethod.BankTransfer },
    { labelKey: 'paymentMethod.check', value: PaymentMethod.Check },
  ];

  protected readonly expenseForm = this.fb.group({
    categoryId: this.fb.control<number | null>(null, [Validators.required]),
    amount: this.fb.control<number | null>(null, [Validators.required, Validators.min(0.01)]),
    expenseDate: this.fb.control<Date | null>(new Date(), [Validators.required]),
    method: this.fb.nonNullable.control<PaymentMethod>(PaymentMethod.Cash),
    description: this.fb.control<string | null>(null),
  });

  protected readonly companyForm = this.fb.group({
    name: this.fb.control<string>('', [Validators.required]),
    vkn: this.fb.control<string | null>(null),
    taxOffice: this.fb.control<string | null>(null),
    phone: this.fb.control<string | null>(null),
    email: this.fb.control<string | null>(null),
    address: this.fb.control<string | null>(null),
    isEInvoiceUser: this.fb.nonNullable.control(false),
  });

  /** Yontem bazli toplamlar (0 olanlar da kart olarak gorunur). */
  protected readonly methodTotals = computed(() => {
    const summary = this.summary();
    const totals = new Map<number, number>();
    for (const t of summary?.collectionsByMethod ?? []) {
      totals.set(t.method, t.total);
    }
    return [
      { labelKey: 'paymentMethod.cash', value: totals.get(PaymentMethod.Cash) ?? 0 },
      { labelKey: 'paymentMethod.creditCardPos', value: totals.get(PaymentMethod.CreditCardPos) ?? 0 },
      { labelKey: 'paymentMethod.bankTransfer', value: totals.get(PaymentMethod.BankTransfer) ?? 0 },
      { labelKey: 'paymentMethod.check', value: totals.get(PaymentMethod.Check) ?? 0 },
    ];
  });

  constructor() {
    effect(() => {
      const date = this.selectedDate();
      untracked(() => this.loadDay(date));
    });
    this.financeApi.expenseCategories().subscribe({
      next: (categories) => this.categories.set(categories),
      error: () => this.categories.set([]),
    });
    this.loadCompanies();
  }

  protected onDateChange(date: Date | null): void {
    if (date) {
      this.selectedDate.set(date);
    }
  }

  private loadDay(date: Date): void {
    const day = toDateOnly(date);
    this.loading.set(true);
    this.financeApi.cashRegister(day).subscribe({
      next: (summary) => {
        this.summary.set(summary);
        this.loading.set(false);
      },
      error: () => {
        this.summary.set(null);
        this.loading.set(false);
      },
    });
    this.financeApi.payments({ from: day, to: day, pageSize: 200 }).subscribe({
      next: (result) => this.dayPayments.set(result.items),
      error: () => this.dayPayments.set([]),
    });
    this.loadExpenses(day);
  }

  private loadExpenses(day: string): void {
    this.expensesLoading.set(true);
    this.financeApi.expenses({ from: day, to: day, pageSize: 100 }).subscribe({
      next: (result) => {
        this.expenses.set(result.items);
        this.expensesLoading.set(false);
      },
      error: () => {
        this.expenses.set([]);
        this.expensesLoading.set(false);
      },
    });
  }

  protected reloadDay(): void {
    this.loadDay(this.selectedDate());
  }

  /** Hareketteki hesap adini gunun tahsilatlarindan hasta id'sine cozer. */
  protected patientIdFor(accountName: string | null): number | null {
    if (!accountName) {
      return null;
    }
    const payment = this.dayPayments().find((p) => p.patientName === accountName);
    return payment?.patientId ?? null;
  }

  protected openAccount(accountName: string | null): void {
    const patientId = this.patientIdFor(accountName);
    if (patientId != null) {
      void this.router.navigate(['/app/patients', patientId, 'payment']);
    }
  }

  // --- Gider Ekle -----------------------------------------------------------

  protected openExpense(): void {
    this.expenseForm.reset({
      categoryId: this.categories()[0]?.id ?? null,
      amount: null,
      expenseDate: this.selectedDate(),
      method: PaymentMethod.Cash,
      description: null,
    });
    this.expenseVisible.set(true);
  }

  protected saveExpense(): void {
    if (this.expenseForm.invalid) {
      this.expenseForm.markAllAsTouched();
      return;
    }
    const v = this.expenseForm.getRawValue();
    this.expenseSaving.set(true);
    this.financeApi
      .createExpense({
        categoryId: v.categoryId as number,
        amount: v.amount as number,
        expenseDate: toDateOnly(v.expenseDate as Date),
        method: v.method,
        description: v.description,
      })
      .subscribe({
        next: () => {
          this.expenseSaving.set(false);
          this.expenseVisible.set(false);
          this.messageService.add({
            severity: 'success',
            summary: this.transloco.translate('cash.expenseSuccess'),
            life: 3000,
          });
          this.reloadDay();
        },
        error: () => this.expenseSaving.set(false),
      });
  }

  protected deleteExpense(expense: ExpenseDto): void {
    this.confirmation.confirm({
      header: this.transloco.translate('cash.deleteExpenseTitle'),
      message: this.transloco.translate('cash.deleteExpenseMessage'),
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
        this.financeApi.deleteExpense(expense.id).subscribe({ next: () => this.reloadDay() });
      },
    });
  }

  protected isPayment(kind: CashMovementKind): boolean {
    return kind === CashMovementKind.Payment;
  }

  protected methodLabelKey(method: PaymentMethod): string {
    const map: Record<number, string> = {
      [PaymentMethod.Cash]: 'paymentMethod.cash',
      [PaymentMethod.CreditCardPos]: 'paymentMethod.creditCardPos',
      [PaymentMethod.BankTransfer]: 'paymentMethod.bankTransfer',
      [PaymentMethod.OnlineLink]: 'paymentMethod.onlineLink',
      [PaymentMethod.Check]: 'paymentMethod.check',
    };
    return map[method] ?? 'paymentMethod.cash';
  }

  // --- Kurumlar -------------------------------------------------------------

  private loadCompanies(): void {
    this.companiesLoading.set(true);
    this.financeApi.companies({ pageSize: 100 }).subscribe({
      next: (result) => {
        this.companies.set(result.items);
        this.companiesLoading.set(false);
      },
      error: () => {
        this.companies.set([]);
        this.companiesLoading.set(false);
      },
    });
  }

  protected openCompanyNew(): void {
    this.editingCompany.set(null);
    this.companyForm.reset({
      name: '',
      vkn: null,
      taxOffice: null,
      phone: null,
      email: null,
      address: null,
      isEInvoiceUser: false,
    });
    this.companyVisible.set(true);
  }

  protected openCompanyEdit(company: CompanyDto): void {
    this.editingCompany.set(company);
    this.companyForm.reset({
      name: company.name,
      vkn: company.vkn,
      taxOffice: company.taxOffice,
      phone: company.phone,
      email: company.email,
      address: company.address,
      isEInvoiceUser: company.isEInvoiceUser,
    });
    this.companyVisible.set(true);
  }

  protected saveCompany(): void {
    if (this.companyForm.invalid) {
      this.companyForm.markAllAsTouched();
      return;
    }
    const v = this.companyForm.getRawValue();
    const request = {
      name: v.name ?? '',
      vkn: v.vkn,
      taxOffice: v.taxOffice,
      phone: v.phone,
      email: v.email,
      address: v.address,
      isEInvoiceUser: v.isEInvoiceUser,
    };
    this.companySaving.set(true);
    const editing = this.editingCompany();
    const call = editing
      ? this.financeApi.updateCompany(editing.id, request)
      : this.financeApi.createCompany(request);
    call.subscribe({
      next: () => {
        this.companySaving.set(false);
        this.companyVisible.set(false);
        this.messageService.add({
          severity: 'success',
          summary: this.transloco.translate('cash.companySaved'),
          life: 3000,
        });
        this.loadCompanies();
      },
      error: () => this.companySaving.set(false),
    });
  }

  protected deleteCompany(company: CompanyDto): void {
    this.confirmation.confirm({
      header: this.transloco.translate('cash.deleteCompanyTitle'),
      message: this.transloco.translate('cash.deleteCompanyMessage'),
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
        this.financeApi.deleteCompany(company.id).subscribe({ next: () => this.loadCompanies() });
      },
    });
  }

  protected openCompanyLedger(company: CompanyDto): void {
    this.companyLedgerTarget.set(company);
    this.companyLedger.set(null);
    this.financeApi.companyLedger(company.id).subscribe({
      next: (statement) => this.companyLedger.set(statement),
      error: () => this.companyLedger.set(null),
    });
  }
}
