import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { TableModule } from 'primeng/table';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { ConfirmationService, MessageService } from 'primeng/api';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AdminApiService, PlanDto, PlanUpsertRequest } from '../../../core/api/admin-api.service';
import { MoneyPipe } from '../../../shared/pipes/money.pipe';

interface PlanForm extends PlanUpsertRequest {
  id: number | null;
}

function emptyPlan(): PlanForm {
  return {
    id: null,
    code: '',
    name: '',
    maxUsers: 5,
    maxPatients: 1000,
    monthlySmsQuota: 250,
    storageGb: 5,
    priceMonthly: 0,
    isActive: true,
    sortOrder: 0,
  };
}

/** Abonelik planlari CRUD (kod, ad, limitler, kota, depolama, ucret, aktiflik). */
@Component({
  selector: 'app-admin-plans',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    ButtonModule,
    DialogModule,
    InputNumberModule,
    InputTextModule,
    TableModule,
    ToggleSwitchModule,
    TranslocoPipe,
    MoneyPipe,
  ],
  templateUrl: './admin-plans.component.html',
})
export class AdminPlansComponent implements OnInit {
  private readonly api = inject(AdminApiService);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly transloco = inject(TranslocoService);

  protected readonly plans = signal<PlanDto[]>([]);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly dialogVisible = signal(false);
  protected readonly form = signal<PlanForm>(emptyPlan());

  ngOnInit(): void {
    this.load();
  }

  protected openCreate(): void {
    this.form.set(emptyPlan());
    this.dialogVisible.set(true);
  }

  protected openEdit(row: PlanDto): void {
    this.form.set({
      id: row.id,
      code: row.code,
      name: row.name,
      maxUsers: row.maxUsers,
      maxPatients: row.maxPatients,
      monthlySmsQuota: row.monthlySmsQuota,
      storageGb: row.storageGb,
      priceMonthly: row.priceMonthly,
      isActive: row.isActive,
      sortOrder: row.sortOrder,
    });
    this.dialogVisible.set(true);
  }

  protected patch(patch: Partial<PlanForm>): void {
    this.form.update((f) => ({ ...f, ...patch }));
  }

  protected save(): void {
    const f = this.form();
    if (!f.code || !f.name) {
      this.messageService.add({
        severity: 'warn',
        summary: this.transloco.translate('validation.formInvalid'),
        life: 4000,
      });
      return;
    }
    const request: PlanUpsertRequest = {
      code: f.code,
      name: f.name,
      maxUsers: f.maxUsers,
      maxPatients: f.maxPatients,
      monthlySmsQuota: f.monthlySmsQuota,
      storageGb: f.storageGb,
      priceMonthly: f.priceMonthly,
      isActive: f.isActive,
      sortOrder: f.sortOrder,
    };
    this.saving.set(true);
    const call = f.id ? this.api.updatePlan(f.id, request) : this.api.createPlan(request);
    call.subscribe({
      next: () => {
        this.saving.set(false);
        this.dialogVisible.set(false);
        this.load();
        this.toastSaved();
      },
      error: () => this.saving.set(false),
    });
  }

  protected remove(row: PlanDto): void {
    this.confirmationService.confirm({
      header: this.transloco.translate('admin.plans.deleteTitle'),
      message: this.transloco.translate('admin.plans.deleteMessage', { name: row.name }),
      acceptLabel: this.transloco.translate('common.yes'),
      rejectLabel: this.transloco.translate('common.no'),
      acceptButtonStyleClass: 'p-button-danger p-button-sm',
      rejectButtonStyleClass: 'p-button-text p-button-sm',
      accept: () => {
        this.api.deletePlan(row.id).subscribe({
          next: () => {
            this.load();
            this.toastSaved();
          },
          error: () => undefined,
        });
      },
    });
  }

  private load(): void {
    this.loading.set(true);
    this.api.plans().subscribe({
      next: (plans) => {
        this.plans.set(plans);
        this.loading.set(false);
      },
      error: () => {
        this.plans.set([]);
        this.loading.set(false);
      },
    });
  }

  private toastSaved(): void {
    this.messageService.add({
      severity: 'success',
      summary: this.transloco.translate('settings.saved'),
      life: 3000,
    });
  }
}
