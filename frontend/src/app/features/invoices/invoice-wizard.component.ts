import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
  untracked,
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { Subscription, concatMap } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { SelectModule } from 'primeng/select';
import { SelectButtonModule } from 'primeng/selectbutton';
import { StepperModule } from 'primeng/stepper';
import { TableModule } from 'primeng/table';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { MessageService } from 'primeng/api';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { InvoicesApiService } from '../../core/api/invoices-api.service';
import {
  GibTaxpayerDto,
  InvoiceDraftRequest,
  InvoiceDto,
  InvoiceListItemDto,
  InvoicePreviewDto,
} from '../../core/api/invoice-api.models';
import { FinanceApiService } from '../../core/api/finance-api.service';
import { CompanyDto } from '../../core/api/finance-api.models';
import { PatientsApiService } from '../../core/api/patients-api.service';
import { PatientDto } from '../../core/api/api.models';
import { TreatmentsApiService } from '../../core/api/treatments-api.service';
import {
  TreatmentRecordDto,
  TreatmentRecordStatus,
} from '../../core/api/treatment-api.models';
import { HasPermissionDirective } from '../../core/auth/has-permission.directive';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import {
  PatientOption,
  PatientSearchSelectComponent,
} from '../../shared/components/patient-search-select/patient-search-select.component';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { TrDatePipe } from '../../shared/pipes/tr-date.pipe';
import { injectTranslationSignal } from '../../shared/utils/transloco-signal';
import {
  InvoiceKindTagComponent,
  InvoiceScenarioTagComponent,
} from './invoice-tags.component';

type BuyerMode = 'patient' | 'company';

/** Gonderim zincirinin hangi adiminda oldugumuz — hata mesajinda kullanilir. */
type ChainStep = 'create' | 'ubl' | 'send' | null;

/**
 * Yeni e-Belge sihirbazi (/app/invoices/new):
 * 1) alici, 2) kalemler + karar motoru onizlemesi, 3) ozet ve gonderim.
 * Belge tipini daima arka uctaki karar motoru belirler; kullanici degistiremez.
 */
@Component({
  selector: 'app-invoice-wizard',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    ButtonModule,
    CheckboxModule,
    SelectModule,
    SelectButtonModule,
    StepperModule,
    TableModule,
    ToggleSwitchModule,
    TranslocoPipe,
    HasPermissionDirective,
    PageHeaderComponent,
    PatientSearchSelectComponent,
    InvoiceKindTagComponent,
    InvoiceScenarioTagComponent,
    MoneyPipe,
    TrDatePipe,
  ],
  templateUrl: './invoice-wizard.component.html',
})
export class InvoiceWizardComponent {
  private readonly api = inject(InvoicesApiService);
  private readonly financeApi = inject(FinanceApiService);
  private readonly patientsApi = inject(PatientsApiService);
  private readonly treatmentsApi = inject(TreatmentsApiService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly messageService = inject(MessageService);
  private readonly transloco = inject(TranslocoService);
  private readonly translation = injectTranslationSignal();

  protected readonly step = signal(1);

  // --- Adim 1: alici ---------------------------------------------------------
  protected readonly buyerMode = signal<BuyerMode>('patient');
  protected readonly patient = signal<PatientDto | null>(null);
  protected readonly patientOption = signal<PatientOption | null>(null);
  protected readonly companies = signal<CompanyDto[]>([]);
  protected readonly companyId = signal<number | null>(null);
  protected readonly gibTaxpayer = signal<GibTaxpayerDto | null>(null);
  protected readonly gibLoading = signal(false);

  // --- Adim 2: kalemler + senaryo -------------------------------------------
  protected readonly treatments = signal<TreatmentRecordDto[]>([]);
  protected readonly treatmentsLoading = signal(false);
  protected readonly selectedIds = signal<number[]>([]);
  protected readonly isForeignPatient = signal(false);
  protected readonly isGovernmentBuyer = signal(false);
  protected readonly isRefund = signal(false);
  protected readonly sourceInvoiceId = signal<number | null>(null);
  protected readonly sourceInvoices = signal<InvoiceListItemDto[]>([]);

  protected readonly preview = signal<InvoicePreviewDto | null>(null);
  protected readonly previewLoading = signal(false);
  private previewSub?: Subscription;

  // --- Adim 3: gonderim ------------------------------------------------------
  protected readonly saving = signal(false);
  protected readonly chainStep = signal<ChainStep>(null);

  protected readonly buyerModeOptions = computed(() => {
    this.translation();
    return [
      { label: this.transloco.translate('invoices.wizard.buyerPatient'), value: 'patient' },
      { label: this.transloco.translate('invoices.wizard.buyerCompany'), value: 'company' },
    ];
  });

  protected readonly companyOptions = computed(() =>
    this.companies().map((c) => ({
      label: c.vkn ? `${c.name} · ${c.vkn}` : c.name,
      value: c.id,
    })),
  );

  protected readonly sourceInvoiceOptions = computed(() =>
    this.sourceInvoices().map((i) => ({
      label: `${i.invoiceNumber ?? '#' + i.id} · ${i.buyerName}`,
      value: i.id,
    })),
  );

  protected readonly selectedCompany = computed(
    () => this.companies().find((c) => c.id === this.companyId()) ?? null,
  );

  /** Yabanci hasta anahtari yalniz uyrugu TUR olmayan hastalarda gorunur. */
  protected readonly isForeignEligible = computed(() => {
    const nationality = this.patient()?.nationalityCode;
    return !!nationality && nationality.toUpperCase() !== 'TUR';
  });

  protected readonly selectedTreatments = computed(() => {
    const ids = new Set(this.selectedIds());
    return this.treatments().filter((t) => ids.has(t.id));
  });

  protected readonly canLeaveStep1 = computed(() => {
    if (this.buyerMode() === 'company') {
      return this.companyId() != null && this.patient() != null;
    }
    return this.patient() != null;
  });

  protected readonly previewErrors = computed(() => this.preview()?.errors ?? []);
  protected readonly previewWarnings = computed(() => this.preview()?.warnings ?? []);

  protected readonly canLeaveStep2 = computed(
    () =>
      this.selectedIds().length > 0 &&
      !this.previewLoading() &&
      !!this.preview() &&
      this.preview()!.canCreate &&
      this.previewErrors().length === 0,
  );

  constructor() {
    this.financeApi.companies({ page: 1, pageSize: 200 }).subscribe({
      next: (result) => this.companies.set(result.items),
      error: () => this.companies.set([]),
    });

    // Odeme sekmesindeki "Fatura Kes" butonu ?patientId= ile gelir.
    effect(() => {
      const patientId = this.route.snapshot.queryParamMap.get('patientId');
      untracked(() => {
        if (patientId) {
          this.loadPatient(Number(patientId));
        }
      });
    });

    // Secim/senaryo degistikce karar motorunu yeniden sor (izlenen sinyaller asagida).
    effect(() => {
      this.selectedIds();
      this.isForeignPatient();
      this.isGovernmentBuyer();
      this.isRefund();
      this.sourceInvoiceId();
      this.buyerMode();
      this.companyId();
      this.patient();
      untracked(() => this.refreshPreview());
    });
  }

  // --- Adim 1 ---------------------------------------------------------------

  protected onPatientSelected(option: PatientOption | null): void {
    this.patientOption.set(option);
    this.preview.set(null);
    this.selectedIds.set([]);
    this.treatments.set([]);
    if (!option) {
      this.patient.set(null);
      return;
    }
    this.loadPatient(option.id);
  }

  private loadPatient(id: number): void {
    this.patientsApi.get(id).subscribe({
      next: (patient) => {
        this.patient.set(patient);
        this.patientOption.set({
          id: patient.id,
          label: `${patient.firstName} ${patient.lastName}`,
          fileNo: patient.fileNo,
          phone: patient.phone,
        });
        if (patient.nationalityCode?.toUpperCase() === 'TUR') {
          this.isForeignPatient.set(false);
        }
        this.loadTreatments(patient.id);
      },
      error: () => this.patient.set(null),
    });
  }

  protected onCompanyChange(id: number | null): void {
    this.companyId.set(id);
    this.gibTaxpayer.set(null);
    const company = this.companies().find((c) => c.id === id);
    if (!company?.vkn) {
      return;
    }
    this.gibLoading.set(true);
    this.api.gibTaxpayer(company.vkn).subscribe({
      next: (taxpayer) => {
        this.gibTaxpayer.set(taxpayer);
        this.gibLoading.set(false);
      },
      error: () => {
        this.gibTaxpayer.set(null);
        this.gibLoading.set(false);
      },
    });
  }

  // --- Adim 2 ---------------------------------------------------------------

  private loadTreatments(patientId: number): void {
    this.treatmentsLoading.set(true);
    this.treatmentsApi.list(patientId, TreatmentRecordStatus.Done).subscribe({
      next: (records) => {
        this.treatments.set(records);
        this.treatmentsLoading.set(false);
      },
      error: () => {
        this.treatments.set([]);
        this.treatmentsLoading.set(false);
      },
    });
  }

  protected isSelected(record: TreatmentRecordDto): boolean {
    return this.selectedIds().includes(record.id);
  }

  protected toggleTreatment(record: TreatmentRecordDto): void {
    this.selectedIds.update((ids) =>
      ids.includes(record.id) ? ids.filter((id) => id !== record.id) : [...ids, record.id],
    );
  }

  protected toggleAll(): void {
    const all = this.treatments().map((t) => t.id);
    this.selectedIds.update((ids) => (ids.length === all.length ? [] : all));
  }

  protected onRefundChange(value: boolean): void {
    this.isRefund.set(value);
    if (!value) {
      this.sourceInvoiceId.set(null);
      return;
    }
    if (this.sourceInvoices().length === 0) {
      this.api.list({ page: 1, pageSize: 100 }).subscribe({
        next: (result) => this.sourceInvoices.set(result.items),
        error: () => this.sourceInvoices.set([]),
      });
    }
  }

  private buildRequest(): InvoiceDraftRequest | null {
    const ids = this.selectedIds();
    if (ids.length === 0) {
      return null;
    }
    const company = this.buyerMode() === 'company' ? this.companyId() : null;
    const patient = this.patient();
    if (!company && !patient) {
      return null;
    }
    return {
      patientId: company ? null : (patient?.id ?? null),
      companyId: company,
      treatmentRecordIds: ids,
      isForeignPatient: this.isForeignPatient(),
      isGovernmentBuyer: this.isGovernmentBuyer(),
      isRefund: this.isRefund(),
      sourceInvoiceId: this.isRefund() ? this.sourceInvoiceId() : null,
    };
  }

  private refreshPreview(): void {
    const request = this.buildRequest();
    this.previewSub?.unsubscribe();
    if (!request) {
      this.preview.set(null);
      return;
    }
    this.previewLoading.set(true);
    this.previewSub = this.api.preview(request).subscribe({
      next: (preview) => {
        this.preview.set(preview);
        this.previewLoading.set(false);
      },
      error: () => {
        this.preview.set(null);
        this.previewLoading.set(false);
      },
    });
  }

  // --- Adim gecisleri -------------------------------------------------------

  protected goTo(step: number): void {
    this.step.set(step);
  }

  protected next(): void {
    this.step.update((s) => Math.min(3, s + 1));
  }

  protected previous(): void {
    this.step.update((s) => Math.max(1, s - 1));
  }

  protected cancelWizard(): void {
    void this.router.navigate(['/app/invoices']);
  }

  // --- Adim 3: kaydet / gonder ----------------------------------------------

  protected saveDraft(): void {
    const request = this.buildRequest();
    if (!request) {
      return;
    }
    this.saving.set(true);
    this.chainStep.set('create');
    this.api.create(request).subscribe({
      next: (invoice) => {
        this.saving.set(false);
        this.chainStep.set(null);
        this.messageService.add({
          severity: 'success',
          summary: this.transloco.translate('invoices.toast.draftSaved'),
          life: 4000,
        });
        void this.router.navigate(['/app/invoices', invoice.id]);
      },
      error: () => this.failChain('create'),
    });
  }

  /** create -> generate-ubl -> send zinciri; her adim ayri toast uretir. */
  protected saveAndSend(): void {
    const request = this.buildRequest();
    if (!request) {
      return;
    }
    this.saving.set(true);
    this.chainStep.set('create');
    let created: InvoiceDto | null = null;

    this.api
      .create(request)
      .pipe(
        concatMap((invoice) => {
          created = invoice;
          this.toastStep('invoices.toast.draftSaved');
          this.chainStep.set('ubl');
          return this.api.generateUbl(invoice.id);
        }),
        concatMap((invoice) => {
          created = invoice;
          this.toastStep('invoices.toast.ublGenerated', invoice.invoiceNumber ?? undefined);
          this.chainStep.set('send');
          return this.api.send(invoice.id);
        }),
      )
      .subscribe({
        next: (invoice) => {
          this.saving.set(false);
          this.chainStep.set(null);
          this.messageService.add({
            severity: invoice.errorMessage ? 'warn' : 'success',
            summary: this.transloco.translate('invoices.toast.sent'),
            detail: invoice.errorMessage ?? undefined,
            life: invoice.errorMessage ? 8000 : 4000,
          });
          void this.router.navigate(['/app/invoices', invoice.id]);
        },
        error: () => {
          const failedAt = this.chainStep();
          this.failChain(failedAt);
          // Belge olustuysa kullanicinin kaybolmamasi icin detaya goturuyoruz.
          if (created) {
            void this.router.navigate(['/app/invoices', (created as InvoiceDto).id]);
          }
        },
      });
  }

  private toastStep(key: string, detail?: string): void {
    this.messageService.add({
      severity: 'success',
      summary: this.transloco.translate(key),
      detail,
      life: 3000,
    });
  }

  private failChain(step: ChainStep): void {
    this.saving.set(false);
    this.chainStep.set(null);
    this.messageService.add({
      severity: 'error',
      summary: this.transloco.translate('invoices.toast.chainFailed'),
      detail: this.transloco.translate(
        'invoices.chainStep.' + (step ?? 'create'),
      ),
      life: 8000,
    });
  }
}
