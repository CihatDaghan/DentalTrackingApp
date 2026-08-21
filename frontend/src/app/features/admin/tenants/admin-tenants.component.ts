import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { ConfirmationService, MessageService } from 'primeng/api';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import {
  AdminApiService,
  PlanDto,
  TenantDetailDto,
  TenantListItemDto,
} from '../../../core/api/admin-api.service';
import { TenantLegalType, TenantStatus } from '../../../core/api/settings-api.models';
import { AuthStore } from '../../../core/auth/auth.store';
import { TrDatePipe } from '../../../shared/pipes/tr-date.pipe';
import { injectTranslationSignal } from '../../../shared/utils/transloco-signal';

interface WizardForm {
  clinicName: string;
  legalType: TenantLegalType;
  adminFirstName: string;
  adminLastName: string;
  adminEmail: string;
  adminPassword: string;
  taxNumber: string | null;
  phone: string | null;
}

function emptyWizard(): WizardForm {
  return {
    clinicName: '',
    legalType: TenantLegalType.SoleProprietor,
    adminFirstName: '',
    adminLastName: '',
    adminEmail: '',
    adminPassword: '',
    taxNumber: null,
    phone: null,
  };
}

/** Rastgele, guclu gecici sifre (kiraci acma sihirbazinda gosterilir). */
function generatePassword(): string {
  const upper = 'ABCDEFGHJKLMNPQRSTUVWXYZ';
  const lower = 'abcdefghijkmnopqrstuvwxyz';
  const digits = '23456789';
  const symbols = '!?*-_';
  const all = upper + lower + digits + symbols;
  const pick = (set: string) => set[Math.floor(Math.random() * set.length)];
  const chars = [pick(upper), pick(lower), pick(digits), pick(symbols)];
  while (chars.length < 12) {
    chars.push(pick(all));
  }
  return chars.sort(() => Math.random() - 0.5).join('');
}

/** Kiracilar: liste/arama, acma sihirbazi, detay (plan/durum/deneme), impersonation. */
@Component({
  selector: 'app-admin-tenants',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    ButtonModule,
    DatePickerModule,
    DialogModule,
    InputTextModule,
    SelectModule,
    TableModule,
    TranslocoPipe,
    TrDatePipe,
  ],
  templateUrl: './admin-tenants.component.html',
})
export class AdminTenantsComponent implements OnInit {
  private readonly api = inject(AdminApiService);
  private readonly authStore = inject(AuthStore);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly transloco = inject(TranslocoService);
  private readonly translation = injectTranslationSignal();

  protected readonly TenantStatus = TenantStatus;
  protected readonly pageSize = 25;

  protected readonly rows = signal<TenantListItemDto[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly first = signal(0);
  protected readonly loading = signal(true);
  protected readonly search = signal('');
  protected readonly statusFilter = signal<TenantStatus | null>(null);
  protected readonly plans = signal<PlanDto[]>([]);

  // --- Sihirbaz -------------------------------------------------------------
  protected readonly wizardVisible = signal(false);
  protected readonly wizard = signal<WizardForm>(emptyWizard());
  protected readonly creating = signal(false);
  protected readonly createdPassword = signal<string | null>(null);
  protected readonly createdEmail = signal('');
  protected readonly passwordCopied = signal(false);

  // --- Detay ----------------------------------------------------------------
  protected readonly detailVisible = signal(false);
  protected readonly detail = signal<TenantDetailDto | null>(null);
  protected readonly detailSaving = signal(false);
  protected readonly detailPlanCode = signal<string | null>(null);
  protected readonly detailStatus = signal<TenantStatus | null>(null);
  protected readonly detailTrialEnds = signal<Date | null>(null);

  protected readonly statusOptions = computed(() => {
    this.translation();
    return [
      { label: this.transloco.translate('common.all'), value: null as TenantStatus | null },
      { label: this.transloco.translate('admin.tenants.status.trial'), value: TenantStatus.Trial },
      { label: this.transloco.translate('admin.tenants.status.active'), value: TenantStatus.Active },
      {
        label: this.transloco.translate('admin.tenants.status.suspended'),
        value: TenantStatus.Suspended,
      },
    ];
  });

  protected readonly detailStatusOptions = computed(() =>
    this.statusOptions().filter((o) => o.value !== null),
  );

  protected readonly legalTypeOptions = computed(() => {
    this.translation();
    return [
      {
        label: this.transloco.translate('settings.clinic.legalTypeSole'),
        value: TenantLegalType.SoleProprietor,
      },
      {
        label: this.transloco.translate('settings.clinic.legalTypeCompany'),
        value: TenantLegalType.Company,
      },
    ];
  });

  protected readonly planOptions = computed(() =>
    this.plans().map((p) => ({ label: `${p.name} (${p.code})`, value: p.code })),
  );

  ngOnInit(): void {
    this.load(1);
    this.api.plans().subscribe({
      next: (plans) => this.plans.set(plans),
      error: () => this.plans.set([]),
    });
  }

  protected statusKey(status: TenantStatus): string {
    return status === TenantStatus.Active
      ? 'admin.tenants.status.active'
      : status === TenantStatus.Suspended
        ? 'admin.tenants.status.suspended'
        : 'admin.tenants.status.trial';
  }

  protected statusClass(status: TenantStatus): string {
    return status === TenantStatus.Active
      ? 'bg-emerald-100 text-emerald-700'
      : status === TenantStatus.Suspended
        ? 'bg-rose-100 text-rose-700'
        : 'bg-blue-100 text-blue-700';
  }

  protected onLazyLoad(event: TableLazyLoadEvent): void {
    const page = Math.floor((event.first ?? 0) / this.pageSize) + 1;
    this.first.set(event.first ?? 0);
    this.load(page);
  }

  protected applyFilters(): void {
    this.first.set(0);
    this.load(1);
  }

  // --- Sihirbaz -------------------------------------------------------------

  protected openWizard(): void {
    this.wizard.set({ ...emptyWizard(), adminPassword: generatePassword() });
    this.createdPassword.set(null);
    this.passwordCopied.set(false);
    this.wizardVisible.set(true);
  }

  protected patchWizard(patch: Partial<WizardForm>): void {
    this.wizard.update((w) => ({ ...w, ...patch }));
  }

  protected createTenant(): void {
    const w = this.wizard();
    if (!w.clinicName || !w.adminEmail || !w.adminFirstName || !w.adminLastName) {
      this.messageService.add({
        severity: 'warn',
        summary: this.transloco.translate('validation.formInvalid'),
        life: 4000,
      });
      return;
    }
    this.creating.set(true);
    this.api
      .createTenant({
        clinicName: w.clinicName,
        legalType: w.legalType,
        adminEmail: w.adminEmail,
        adminFirstName: w.adminFirstName,
        adminLastName: w.adminLastName,
        adminPassword: w.adminPassword,
        taxNumber: w.taxNumber,
        phone: w.phone,
      })
      .subscribe({
        next: () => {
          this.creating.set(false);
          this.createdPassword.set(w.adminPassword);
          this.createdEmail.set(w.adminEmail);
          this.load(1);
        },
        error: () => this.creating.set(false),
      });
  }

  protected copyPassword(): void {
    const password = this.createdPassword();
    if (password) {
      void navigator.clipboard?.writeText(password).then(() => this.passwordCopied.set(true));
    }
  }

  protected closeWizard(): void {
    this.wizardVisible.set(false);
    this.createdPassword.set(null);
  }

  // --- Detay ----------------------------------------------------------------

  protected openDetail(row: TenantListItemDto): void {
    this.detail.set(null);
    this.detailVisible.set(true);
    this.api.tenant(row.id).subscribe({
      next: (dto) => {
        this.detail.set(dto);
        this.detailPlanCode.set(dto.planCode);
        this.detailStatus.set(dto.status);
        this.detailTrialEnds.set(dto.trialEndsAtUtc ? new Date(dto.trialEndsAtUtc) : null);
      },
      error: () => this.detailVisible.set(false),
    });
  }

  protected saveDetail(): void {
    const detail = this.detail();
    if (!detail) {
      return;
    }
    this.detailSaving.set(true);
    this.api
      .updateTenant(detail.id, {
        planCode: this.detailPlanCode(),
        status: this.detailStatus(),
        trialEndsAtUtc: this.detailTrialEnds()?.toISOString() ?? null,
      })
      .subscribe({
        next: (dto) => {
          this.detail.set(dto);
          this.detailSaving.set(false);
          this.load(Math.floor(this.first() / this.pageSize) + 1);
          this.messageService.add({
            severity: 'success',
            summary: this.transloco.translate('settings.saved'),
            life: 3000,
          });
        },
        error: () => this.detailSaving.set(false),
      });
  }

  /** Impersonation: 15 dk'lik token'la /app'e gecis; uyari bandi kalici gorunur. */
  protected impersonate(row: TenantListItemDto | TenantDetailDto): void {
    this.confirmationService.confirm({
      header: this.transloco.translate('admin.impersonation.confirmTitle'),
      message: this.transloco.translate('admin.impersonation.confirmMessage', { tenant: row.name }),
      acceptLabel: this.transloco.translate('common.yes'),
      rejectLabel: this.transloco.translate('common.no'),
      acceptButtonStyleClass: 'p-button-warn p-button-sm',
      rejectButtonStyleClass: 'p-button-text p-button-sm',
      accept: () => {
        this.api.impersonate(row.id).subscribe({
          next: (response) => {
            if (this.authStore.startImpersonation(response)) {
              this.detailVisible.set(false);
              void this.router.navigate(['/app/dashboard']);
            }
          },
          error: () => undefined,
        });
      },
    });
  }

  private load(page: number): void {
    this.loading.set(true);
    this.api
      .tenants({
        search: this.search() || null,
        status: this.statusFilter(),
        page,
        pageSize: this.pageSize,
      })
      .subscribe({
        next: (result) => {
          this.rows.set(result.items);
          this.totalCount.set(result.totalCount);
          this.loading.set(false);
        },
        error: () => {
          this.rows.set([]);
          this.totalCount.set(0);
          this.loading.set(false);
        },
      });
  }
}
