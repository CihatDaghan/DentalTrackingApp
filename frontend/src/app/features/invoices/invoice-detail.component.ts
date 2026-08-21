import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
} from '@angular/core';
import { Router } from '@angular/router';
import { Observable } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { TimelineModule } from 'primeng/timeline';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmationService, MessageService } from 'primeng/api';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { InvoicesApiService } from '../../core/api/invoices-api.service';
import {
  INTEGRATOR_KEYS,
  INVOICE_RETRY_STATUSES,
  INVOICE_STATUS_KEYS,
  InvoiceDto,
  InvoiceStatus,
  InvoiceStatusLogDto,
} from '../../core/api/invoice-api.models';
import { HasPermissionDirective } from '../../core/auth/has-permission.directive';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { TrDatePipe } from '../../shared/pipes/tr-date.pipe';
import { downloadBlob, openBlobInNewTab } from '../../shared/utils/file-download';
import {
  InvoiceKindTagComponent,
  InvoiceScenarioTagComponent,
  InvoiceStatusTagComponent,
} from './invoice-tags.component';

/** Zaman cizelgesi satirinin gorsel dokusu. */
interface TimelineEntry {
  log: InvoiceStatusLogDto;
  icon: string;
  color: string;
  statusKey: string;
}

const TIMELINE_LOOK: Record<number, { icon: string; color: string }> = {
  [InvoiceStatus.Draft]: { icon: 'fa-solid fa-file-pen', color: '#64748b' },
  [InvoiceStatus.UblGenerated]: { icon: 'fa-solid fa-file-code', color: '#3b82f6' },
  [InvoiceStatus.Queued]: { icon: 'fa-solid fa-hourglass-half', color: '#d97706' },
  [InvoiceStatus.SentToIntegrator]: { icon: 'fa-solid fa-paper-plane', color: '#d97706' },
  [InvoiceStatus.GibProcessing]: { icon: 'fa-solid fa-building-columns', color: '#d97706' },
  [InvoiceStatus.Succeeded]: { icon: 'fa-solid fa-circle-check', color: '#16a34a' },
  [InvoiceStatus.GibRejected]: { icon: 'fa-solid fa-circle-xmark', color: '#dc2626' },
  [InvoiceStatus.BuyerRejected]: { icon: 'fa-solid fa-user-xmark', color: '#dc2626' },
  [InvoiceStatus.Error]: { icon: 'fa-solid fa-triangle-exclamation', color: '#dc2626' },
  [InvoiceStatus.ManualReview]: { icon: 'fa-solid fa-user-clock', color: '#dc2626' },
  [InvoiceStatus.Cancelled]: { icon: 'fa-solid fa-ban', color: '#94a3b8' },
};

/**
 * e-Belge detayi (/app/invoices/:id): kunye + alici + kalemler + toplamlar +
 * durum gecmisi zaman cizelgesi; aksiyonlar belgenin durumuna gore acilir.
 */
@Component({
  selector: 'app-invoice-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ButtonModule,
    TableModule,
    TimelineModule,
    TooltipModule,
    TranslocoPipe,
    HasPermissionDirective,
    PageHeaderComponent,
    InvoiceKindTagComponent,
    InvoiceScenarioTagComponent,
    InvoiceStatusTagComponent,
    MoneyPipe,
    TrDatePipe,
  ],
  templateUrl: './invoice-detail.component.html',
  styles: `
    /* Dar kolonda uzun entegrator/UBL ozet metinleri tasmasin. */
    :host ::ng-deep .invoice-timeline .p-timeline-event-opposite {
      display: none;
    }
    :host ::ng-deep .invoice-timeline .p-timeline-event-content {
      min-width: 0;
      overflow-wrap: anywhere;
    }
  `,
})
export class InvoiceDetailComponent {
  private readonly api = inject(InvoicesApiService);
  private readonly router = inject(Router);
  private readonly messageService = inject(MessageService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly transloco = inject(TranslocoService);

  /** Route parametresi (withComponentInputBinding). */
  readonly id = input.required<string>();

  protected readonly InvoiceStatus = InvoiceStatus;

  protected readonly invoice = signal<InvoiceDto | null>(null);
  protected readonly loading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly busy = signal(false);

  protected readonly integratorKey = computed(() => {
    const provider = this.invoice()?.integratorProvider;
    return provider ? 'invoices.integrator.' + INTEGRATOR_KEYS[provider] : null;
  });

  protected readonly canRetry = computed(() => {
    const status = this.invoice()?.status;
    return status != null && INVOICE_RETRY_STATUSES.includes(status);
  });

  protected readonly timeline = computed<TimelineEntry[]>(() =>
    [...(this.invoice()?.statusLogs ?? [])]
      .sort((a, b) => a.atUtc.localeCompare(b.atUtc))
      .map((log) => ({
        log,
        icon: TIMELINE_LOOK[log.toStatus]?.icon ?? 'fa-solid fa-circle',
        color: TIMELINE_LOOK[log.toStatus]?.color ?? '#94a3b8',
        statusKey: 'invoices.status.' + (INVOICE_STATUS_KEYS[log.toStatus] ?? 'unknown'),
      })),
  );

  constructor() {
    effect(() => {
      const id = Number(this.id());
      untracked(() => this.load(id));
    });
  }

  private load(id: number): void {
    this.loading.set(true);
    this.notFound.set(false);
    this.api.get(id).subscribe({
      next: (invoice) => {
        this.invoice.set(invoice);
        this.loading.set(false);
      },
      error: () => {
        this.invoice.set(null);
        this.notFound.set(true);
        this.loading.set(false);
      },
    });
  }

  protected back(): void {
    void this.router.navigate(['/app/invoices']);
  }

  // --- Durum aksiyonlari -----------------------------------------------------

  protected generateUbl(): void {
    this.run((id) => this.api.generateUbl(id), 'invoices.toast.ublGenerated');
  }

  protected send(): void {
    this.run((id) => this.api.send(id), 'invoices.toast.sent');
  }

  private run(
    call: (id: number) => Observable<InvoiceDto>,
    successKey: string,
  ): void {
    const current = this.invoice();
    if (!current) {
      return;
    }
    this.busy.set(true);
    call(current.id).subscribe({
      next: (invoice) => {
        this.invoice.set(invoice);
        this.busy.set(false);
        const failed = INVOICE_RETRY_STATUSES.includes(invoice.status);
        this.messageService.add({
          severity: failed ? 'warn' : 'success',
          summary: this.transloco.translate(successKey),
          detail: failed ? (invoice.errorMessage ?? undefined) : undefined,
          life: failed ? 8000 : 4000,
        });
      },
      error: () => this.busy.set(false),
    });
  }

  protected cancel(): void {
    const current = this.invoice();
    if (!current) {
      return;
    }
    this.confirmation.confirm({
      header: this.transloco.translate('invoices.cancelTitle'),
      message: this.transloco.translate('invoices.cancelMessage', {
        number: current.invoiceNumber ?? '#' + current.id,
      }),
      icon: 'pi pi-exclamation-triangle',
      acceptButtonProps: {
        label: this.transloco.translate('invoices.cancelAccept'),
        severity: 'danger',
      },
      rejectButtonProps: {
        label: this.transloco.translate('common.cancel'),
        severity: 'secondary',
        outlined: true,
      },
      accept: () => {
        this.busy.set(true);
        this.api
          .cancel(current.id, {
            reason: this.transloco.translate('invoices.cancelDefaultReason'),
          })
          .subscribe({
            next: (invoice) => {
              this.invoice.set(invoice);
              this.busy.set(false);
              this.messageService.add({
                severity: 'success',
                summary: this.transloco.translate('invoices.toast.cancelled'),
                life: 4000,
              });
            },
            error: () => this.busy.set(false),
          });
      },
    });
  }

  // --- Indirmeler ------------------------------------------------------------

  protected downloadUbl(): void {
    const current = this.invoice();
    if (!current) {
      return;
    }
    this.busy.set(true);
    this.api.ubl(current.id).subscribe({
      next: (blob) => {
        downloadBlob(blob, `${current.invoiceNumber ?? 'fatura-' + current.id}.xml`);
        this.busy.set(false);
      },
      error: () => this.busy.set(false),
    });
  }

  protected downloadPdf(): void {
    const current = this.invoice();
    if (!current) {
      return;
    }
    this.busy.set(true);
    this.api.pdf(current.id).subscribe({
      next: (blob) => {
        openBlobInNewTab(blob, `${current.invoiceNumber ?? 'fatura-' + current.id}.pdf`);
        this.busy.set(false);
      },
      error: () => this.busy.set(false),
    });
  }
}
