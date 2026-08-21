import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TextareaModule } from 'primeng/textarea';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { ConfirmationService, MessageService } from 'primeng/api';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import {
  AdminApiService,
  AnnouncementDto,
  TenantListItemDto,
} from '../../../core/api/admin-api.service';
import { AnnouncementSeverity } from '../../../core/api/notifications-api.service';
import { TrDatePipe } from '../../../shared/pipes/tr-date.pipe';
import { injectTranslationSignal } from '../../../shared/utils/transloco-signal';

interface AnnouncementForm {
  id: number | null;
  title: string;
  body: string;
  severity: AnnouncementSeverity;
  startsAt: Date | null;
  endsAt: Date | null;
  isActive: boolean;
  targetTenantId: number | null;
}

function emptyForm(): AnnouncementForm {
  return {
    id: null,
    title: '',
    body: '',
    severity: AnnouncementSeverity.Info,
    startsAt: new Date(),
    endsAt: null,
    isActive: true,
    targetTenantId: null,
  };
}

/** Platform duyurulari CRUD — hedef kiraci bos ise tum kiracilara gorunur. */
@Component({
  selector: 'app-admin-announcements',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    ButtonModule,
    DatePickerModule,
    DialogModule,
    InputTextModule,
    SelectModule,
    TableModule,
    TextareaModule,
    ToggleSwitchModule,
    TranslocoPipe,
    TrDatePipe,
  ],
  templateUrl: './admin-announcements.component.html',
})
export class AdminAnnouncementsComponent implements OnInit {
  private readonly api = inject(AdminApiService);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly transloco = inject(TranslocoService);
  private readonly translation = injectTranslationSignal();

  protected readonly AnnouncementSeverity = AnnouncementSeverity;

  protected readonly items = signal<AnnouncementDto[]>([]);
  protected readonly tenants = signal<TenantListItemDto[]>([]);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly dialogVisible = signal(false);
  protected readonly form = signal<AnnouncementForm>(emptyForm());

  protected readonly severityOptions = computed(() => {
    this.translation();
    return [
      {
        label: this.transloco.translate('admin.announcements.severity.info'),
        value: AnnouncementSeverity.Info,
      },
      {
        label: this.transloco.translate('admin.announcements.severity.warning'),
        value: AnnouncementSeverity.Warning,
      },
    ];
  });

  protected readonly tenantOptions = computed(() => {
    this.translation();
    return [
      {
        label: this.transloco.translate('admin.announcements.allTenants'),
        value: null as number | null,
      },
      ...this.tenants().map((t) => ({ label: t.name, value: t.id as number | null })),
    ];
  });

  ngOnInit(): void {
    this.load();
    this.api.tenants({ pageSize: 100 }).subscribe({
      next: (result) => this.tenants.set(result.items),
      error: () => this.tenants.set([]),
    });
  }

  protected severityLabel(severity: AnnouncementSeverity): string {
    return severity === AnnouncementSeverity.Warning
      ? 'admin.announcements.severity.warning'
      : 'admin.announcements.severity.info';
  }

  protected openCreate(): void {
    this.form.set(emptyForm());
    this.dialogVisible.set(true);
  }

  protected openEdit(row: AnnouncementDto): void {
    this.form.set({
      id: row.id,
      title: row.title,
      body: row.body,
      severity: row.severity,
      startsAt: row.startsAtUtc ? new Date(row.startsAtUtc) : null,
      endsAt: row.endsAtUtc ? new Date(row.endsAtUtc) : null,
      isActive: row.isActive,
      targetTenantId: row.targetTenantId,
    });
    this.dialogVisible.set(true);
  }

  protected patch(patch: Partial<AnnouncementForm>): void {
    this.form.update((f) => ({ ...f, ...patch }));
  }

  protected save(): void {
    const f = this.form();
    if (!f.title || !f.body) {
      this.messageService.add({
        severity: 'warn',
        summary: this.transloco.translate('validation.formInvalid'),
        life: 4000,
      });
      return;
    }
    const request = {
      title: f.title,
      body: f.body,
      severity: f.severity,
      startsAtUtc: f.startsAt?.toISOString() ?? null,
      endsAtUtc: f.endsAt?.toISOString() ?? null,
      isActive: f.isActive,
      targetTenantId: f.targetTenantId,
    };
    this.saving.set(true);
    const call = f.id
      ? this.api.updateAnnouncement(f.id, request)
      : this.api.createAnnouncement(request);
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

  protected remove(row: AnnouncementDto): void {
    this.confirmationService.confirm({
      header: this.transloco.translate('admin.announcements.deleteTitle'),
      message: this.transloco.translate('admin.announcements.deleteMessage', { title: row.title }),
      acceptLabel: this.transloco.translate('common.yes'),
      rejectLabel: this.transloco.translate('common.no'),
      acceptButtonStyleClass: 'p-button-danger p-button-sm',
      rejectButtonStyleClass: 'p-button-text p-button-sm',
      accept: () => {
        this.api.deleteAnnouncement(row.id).subscribe({
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
    this.api.announcements().subscribe({
      next: (items) => {
        this.items.set(items);
        this.loading.set(false);
      },
      error: () => {
        this.items.set([]);
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
