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
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TranslocoPipe } from '@jsverse/transloco';
import { ReportsApiService } from '../../core/api/reports-api.service';
import { DebtorRowDto, ReportQuery } from '../../core/api/reports-api.models';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { TrDatePipe } from '../../shared/pipes/tr-date.pipe';
import { HasPermissionDirective } from '../../core/auth/has-permission.directive';

/**
 * Borclular raporu: sayfali tablo + minimum bakiye filtresi.
 * "Toplu odeme hatirlatmasi" secili (yoksa filtrelenmis) hastalarla
 * `/app/messaging` toplu gonderim sekmesine yonlendirir.
 */
@Component({
  selector: 'app-debtors-report',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    ButtonModule,
    TableModule,
    TranslocoPipe,
    MoneyPipe,
    TrDatePipe,
    HasPermissionDirective,
  ],
  template: `
    <div class="flex flex-col gap-4" data-testid="report-debtors">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <div class="flex items-center gap-4">
          <div class="dt-card px-3 py-2 flex flex-col">
            <span class="text-[11px] font-medium text-slate-500 uppercase tracking-wide">
              {{ 'reports.debtors.patientCount' | transloco }}
            </span>
            <span class="text-lg font-semibold text-slate-700" data-testid="debtors-count">
              {{ totalCount() }}
            </span>
          </div>
          <div class="dt-card px-3 py-2 flex flex-col">
            <span class="text-[11px] font-medium text-slate-500 uppercase tracking-wide">
              {{ 'reports.debtors.pageTotal' | transloco }}
            </span>
            <span class="text-lg font-semibold text-rose-600">{{ pageTotal() | money }}</span>
          </div>
        </div>

        <p-button
          *hasPermission="'messaging.bulk'"
          [label]="'reports.debtors.bulkReminder' | transloco"
          icon="fa-solid fa-bullhorn"
          size="small"
          [disabled]="totalCount() === 0"
          (onClick)="goToBulkReminder()"
          data-testid="debtors-bulk-reminder"
        />
      </div>

      <div class="dt-card p-0 overflow-hidden">
        <p-table
          [value]="rows()"
          [lazy]="true"
          [loading]="loading()"
          [paginator]="true"
          [rows]="pageSize"
          [totalRecords]="totalCount()"
          [first]="first()"
          (onLazyLoad)="onLazyLoad($event)"
          [(selection)]="selected"
          dataKey="patientId"
          styleClass="p-datatable-sm"
        >
          <ng-template #header>
            <tr>
              <th style="width: 3rem"><p-tableHeaderCheckbox /></th>
              <th>{{ 'reports.debtors.fileNo' | transloco }}</th>
              <th>{{ 'reports.debtors.name' | transloco }}</th>
              <th>{{ 'reports.debtors.phone' | transloco }}</th>
              <th class="text-right!">{{ 'reports.debtors.balance' | transloco }}</th>
              <th>{{ 'reports.debtors.lastEntry' | transloco }}</th>
              <th>{{ 'reports.debtors.lastAppointment' | transloco }}</th>
            </tr>
          </ng-template>
          <ng-template #body let-row>
            <tr>
              <td><p-tableCheckbox [value]="row" /></td>
              <td class="text-slate-500 font-mono text-xs">{{ row.fileNo }}</td>
              <td>
                <a
                  class="text-blue-600 font-medium hover:underline"
                  [href]="'/app/patients/' + row.patientId"
                  (click)="openPatient($event, row.patientId)"
                >
                  {{ row.fullName }}
                </a>
              </td>
              <td class="text-slate-500">{{ row.phone || '—' }}</td>
              <td class="text-right! font-semibold text-rose-600">{{ row.balance | money }}</td>
              <td class="text-slate-500">{{ row.lastEntryDate | trDate }}</td>
              <td class="text-slate-500">
                {{ row.lastAppointmentUtc ? (row.lastAppointmentUtc | trDate: 'dd.MM.yyyy') : '—' }}
              </td>
            </tr>
          </ng-template>
          <ng-template #emptymessage>
            <tr>
              <td colspan="7" class="text-center text-slate-400 py-6">
                {{ 'table.empty' | transloco }}
              </td>
            </tr>
          </ng-template>
        </p-table>
      </div>
    </div>
  `,
})
export class DebtorsReportComponent {
  private readonly api = inject(ReportsApiService);
  private readonly router = inject(Router);

  readonly query = input.required<ReportQuery>();

  protected readonly pageSize = 25;
  protected readonly rows = signal<DebtorRowDto[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly loading = signal(false);
  protected readonly first = signal(0);
  protected readonly selected = signal<DebtorRowDto[]>([]);

  protected readonly pageTotal = computed(() =>
    this.rows().reduce((sum, r) => sum + r.balance, 0),
  );

  constructor() {
    effect(() => {
      this.query();
      untracked(() => {
        this.first.set(0);
        this.selected.set([]);
        this.load(1);
      });
    });
  }

  protected onLazyLoad(event: TableLazyLoadEvent): void {
    const page = Math.floor((event.first ?? 0) / this.pageSize) + 1;
    this.first.set(event.first ?? 0);
    this.load(page);
  }

  protected openPatient(event: Event, patientId: number): void {
    event.preventDefault();
    void this.router.navigate(['/app/patients', patientId]);
  }

  /** Secili hasta yoksa filtrelenmis kitleyle (borclu hastalar) sihirbaza gecilir. */
  protected goToBulkReminder(): void {
    const ids = this.selected().map((r) => r.patientId);
    void this.router.navigate(['/app/messaging'], {
      queryParams: {
        tab: 'bulk',
        hasDebt: 'true',
        minBalance: this.query().minBalance ?? undefined,
        patientIds: ids.length ? ids.join(',') : undefined,
      },
    });
  }

  private load(page: number): void {
    this.loading.set(true);
    this.api.debtors(this.query(), page, this.pageSize).subscribe({
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
