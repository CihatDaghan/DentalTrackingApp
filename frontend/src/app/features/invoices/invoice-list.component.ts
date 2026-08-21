import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { Observable, map } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmationService, MessageService } from 'primeng/api';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { InvoicesApiService } from '../../core/api/invoices-api.service';
import {
  INVOICE_ALL_STATUSES,
  INVOICE_RETRY_STATUSES,
  INVOICE_STATUS_KEYS,
  InvoiceListItemDto,
  InvoiceStatus,
} from '../../core/api/invoice-api.models';
import { PagedResult, TableQuery, toDateOnly } from '../../core/api/api.models';
import { HasPermissionDirective } from '../../core/auth/has-permission.directive';
import {
  AppTableComponent,
  TableColumn,
} from '../../shared/components/app-table/app-table.component';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { TrDatePipe } from '../../shared/pipes/tr-date.pipe';
import { downloadBlob, openBlobInNewTab } from '../../shared/utils/file-download';
import { injectTranslationSignal } from '../../shared/utils/transloco-signal';
import {
  InvoiceKindTagComponent,
  InvoiceScenarioTagComponent,
  InvoiceStatusTagComponent,
} from './invoice-tags.component';

/** Arama yapilirken sunucudan tek seferde cekilecek ust sinir (API'de arama ucu yok). */
const SEARCH_FETCH_SIZE = 500;

/** Dashboard "hata kuyrugu" linki ?status=9 ile gelir; ilk filtre degeri buradan okunur. */
function readStatusParam(route: ActivatedRoute): InvoiceStatus | null {
  const raw = route.snapshot.queryParamMap.get('status');
  if (!raw) {
    return null;
  }
  const parsed = Number(raw);
  return INVOICE_ALL_STATUSES.includes(parsed as InvoiceStatus)
    ? (parsed as InvoiceStatus)
    : null;
}

/**
 * e-Belge listesi (/app/invoices): durum + tarih araligi + arama filtreleri,
 * hata satirinda genisletilebilir entegrator mesaji ve "yeniden gonder" aksiyonu.
 */
@Component({
  selector: 'app-invoice-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    ButtonModule,
    DatePickerModule,
    InputTextModule,
    SelectModule,
    TooltipModule,
    TranslocoPipe,
    HasPermissionDirective,
    AppTableComponent,
    PageHeaderComponent,
    InvoiceKindTagComponent,
    InvoiceScenarioTagComponent,
    InvoiceStatusTagComponent,
    MoneyPipe,
    TrDatePipe,
  ],
  templateUrl: './invoice-list.component.html',
})
export class InvoiceListComponent {
  private readonly api = inject(InvoicesApiService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly messageService = inject(MessageService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly transloco = inject(TranslocoService);
  private readonly translation = injectTranslationSignal();

  protected readonly InvoiceStatus = InvoiceStatus;

  private readonly table = viewChild(AppTableComponent<InvoiceListItemDto>);

  // Ilk deger route'tan okunur; app-table ilk lazy yuklemesinde bu filtreyi kullanir.
  protected readonly filterStatus = signal<InvoiceStatus | null>(readStatusParam(this.route));
  protected readonly filterFrom = signal<Date | null>(null);
  protected readonly filterTo = signal<Date | null>(null);
  protected readonly searchTerm = signal('');
  protected readonly expandedRowKeys = signal<Record<string, boolean>>({});
  protected readonly busyId = signal<number | null>(null);

  protected readonly columns: TableColumn[] = [
    { field: 'expander', headerKey: 'invoices.col.expander', width: '3rem' },
    { field: 'invoiceNumber', headerKey: 'invoices.col.number', width: '11rem' },
    { field: 'documentKind', headerKey: 'invoices.col.kind', width: '8rem' },
    { field: 'typeCode', headerKey: 'invoices.col.scenario', width: '8rem' },
    { field: 'buyerName', headerKey: 'invoices.col.buyer' },
    { field: 'issueDate', headerKey: 'invoices.col.issueDate', width: '7rem' },
    { field: 'payableAmount', headerKey: 'invoices.col.amount', width: '9rem', align: 'right' },
    { field: 'status', headerKey: 'invoices.col.status', width: '9rem' },
    { field: 'actions', headerKey: 'invoices.col.actions', width: '11rem', align: 'right' },
  ];

  protected readonly statusOptions = computed(() => {
    this.translation();
    return INVOICE_ALL_STATUSES.map((value) => ({
      label: this.transloco.translate('invoices.status.' + INVOICE_STATUS_KEYS[value]),
      value,
    }));
  });

  /** app-table lazy yukleyicisi — filtreler degisince `reload()` ile tetiklenir. */
  protected readonly loader = (query: TableQuery): Observable<PagedResult<InvoiceListItemDto>> => {
    const term = this.searchTerm().trim().toLocaleLowerCase('tr');
    const base = {
      status: this.filterStatus(),
      from: this.filterFrom() ? toDateOnly(this.filterFrom() as Date) : null,
      to: this.filterTo() ? toDateOnly(this.filterTo() as Date) : null,
    };

    if (!term) {
      return this.api.list({ ...base, page: query.page, pageSize: query.pageSize });
    }

    // API'de arama parametresi yok: genis bir pencere cekilip istemcide suzulur.
    return this.api.list({ ...base, page: 1, pageSize: SEARCH_FETCH_SIZE }).pipe(
      map((result) => {
        const filtered = result.items.filter((item) =>
          [item.invoiceNumber, item.buyerName, item.ettn, item.typeCode]
            .filter((v): v is string => !!v)
            .some((v) => v.toLocaleLowerCase('tr').includes(term)),
        );
        const start = (query.page - 1) * query.pageSize;
        return {
          items: filtered.slice(start, start + query.pageSize),
          page: query.page,
          pageSize: query.pageSize,
          totalCount: filtered.length,
        };
      }),
    );
  };

  protected reload(): void {
    this.expandedRowKeys.set({});
    this.table()?.reload(true);
  }

  protected clearFilters(): void {
    this.filterStatus.set(null);
    this.filterFrom.set(null);
    this.filterTo.set(null);
    this.searchTerm.set('');
    void this.router.navigate([], { relativeTo: this.route, queryParams: {} });
    this.reload();
  }

  protected isRetryable(status: InvoiceStatus): boolean {
    return INVOICE_RETRY_STATUSES.includes(status);
  }

  protected isExpanded(row: InvoiceListItemDto): boolean {
    return !!this.expandedRowKeys()[String(row.id)];
  }

  protected toggleExpand(row: InvoiceListItemDto, event: Event): void {
    event.stopPropagation();
    this.expandedRowKeys.update((keys) => {
      const next = { ...keys };
      if (next[String(row.id)]) {
        delete next[String(row.id)];
      } else {
        next[String(row.id)] = true;
      }
      return next;
    });
  }

  protected openDetail(row: InvoiceListItemDto): void {
    void this.router.navigate(['/app/invoices', row.id]);
  }

  protected newInvoice(): void {
    void this.router.navigate(['/app/invoices/new']);
  }

  // --- Satir aksiyonlari -----------------------------------------------------

  protected downloadUbl(row: InvoiceListItemDto, event?: Event): void {
    event?.stopPropagation();
    this.busyId.set(row.id);
    this.api.ubl(row.id).subscribe({
      next: (blob) => {
        downloadBlob(blob, `${row.invoiceNumber ?? 'fatura-' + row.id}.xml`);
        this.busyId.set(null);
      },
      error: () => this.busyId.set(null),
    });
  }

  protected downloadPdf(row: InvoiceListItemDto, event?: Event): void {
    event?.stopPropagation();
    this.busyId.set(row.id);
    this.api.pdf(row.id).subscribe({
      next: (blob) => {
        openBlobInNewTab(blob, `${row.invoiceNumber ?? 'fatura-' + row.id}.pdf`);
        this.busyId.set(null);
      },
      error: () => this.busyId.set(null),
    });
  }

  protected retry(row: InvoiceListItemDto, event?: Event): void {
    event?.stopPropagation();
    this.busyId.set(row.id);
    this.api.send(row.id).subscribe({
      next: (invoice) => {
        this.busyId.set(null);
        this.messageService.add({
          severity: this.isRetryable(invoice.status) ? 'warn' : 'success',
          summary: this.transloco.translate('invoices.toast.resent'),
          detail: invoice.errorMessage ?? undefined,
          life: 6000,
        });
        this.table()?.reload();
      },
      error: () => this.busyId.set(null),
    });
  }

  protected cancel(row: InvoiceListItemDto, event?: Event): void {
    event?.stopPropagation();
    this.confirmation.confirm({
      header: this.transloco.translate('invoices.cancelTitle'),
      message: this.transloco.translate('invoices.cancelMessage', {
        number: row.invoiceNumber ?? '#' + row.id,
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
        this.busyId.set(row.id);
        this.api
          .cancel(row.id, { reason: this.transloco.translate('invoices.cancelDefaultReason') })
          .subscribe({
            next: () => {
              this.busyId.set(null);
              this.messageService.add({
                severity: 'success',
                summary: this.transloco.translate('invoices.toast.cancelled'),
                life: 4000,
              });
              this.table()?.reload();
            },
            error: () => this.busyId.set(null),
          });
      },
    });
  }

  /** app-table `search` girisi degisince kendi kendine ilk sayfadan yeniden yukler. */
  protected onSearchChange(value: string): void {
    this.searchTerm.set(value);
    this.expandedRowKeys.set({});
  }
}
