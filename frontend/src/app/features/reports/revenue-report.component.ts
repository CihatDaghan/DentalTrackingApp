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
import { ChartModule } from 'primeng/chart';
import { TableModule } from 'primeng/table';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { ReportsApiService } from '../../core/api/reports-api.service';
import { ReportQuery, RevenueReportDto } from '../../core/api/reports-api.models';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { CHART_COLORS, moneyChartOptions, paletteFor, pieChartOptions } from '../../shared/utils/chart-theme';
import { injectTranslationSignal } from '../../shared/utils/transloco-signal';
import { PAYMENT_METHOD_KEYS } from './report-labels';

/** Ciro raporu: tedavi geliri + tahsilat cizgisi, yontem kirilimi pastasi, donem tablosu. */
@Component({
  selector: 'app-revenue-report',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ChartModule, TableModule, TranslocoPipe, MoneyPipe],
  template: `
    <div class="flex flex-col gap-4" data-testid="report-revenue">
      <!-- Ozet kartlari -->
      <div class="grid grid-cols-1 sm:grid-cols-3 gap-3">
        <div class="dt-card p-3 flex flex-col gap-1">
          <span class="text-[11px] font-medium text-slate-500 uppercase tracking-wide">
            {{ 'reports.revenue.totalTreatment' | transloco }}
          </span>
          <span class="text-xl font-semibold text-slate-700" data-testid="revenue-total">
            {{ data()?.totalTreatmentRevenue ?? 0 | money }}
          </span>
        </div>
        <div class="dt-card p-3 flex flex-col gap-1">
          <span class="text-[11px] font-medium text-slate-500 uppercase tracking-wide">
            {{ 'reports.revenue.totalCollected' | transloco }}
          </span>
          <span class="text-xl font-semibold text-emerald-600">
            {{ data()?.totalCollected ?? 0 | money }}
          </span>
        </div>
        <div class="dt-card p-3 flex flex-col gap-1">
          <span class="text-[11px] font-medium text-slate-500 uppercase tracking-wide">
            {{ 'reports.revenue.treatmentCount' | transloco }}
          </span>
          <span class="text-xl font-semibold text-slate-700">
            {{ data()?.totalTreatmentCount ?? 0 }}
          </span>
        </div>
      </div>

      <div class="grid grid-cols-1 xl:grid-cols-3 gap-4">
        <div class="dt-card p-4 xl:col-span-2 min-w-0">
          <h3 class="m-0 mb-3 text-base font-semibold text-slate-800">
            {{ 'reports.revenue.chartTitle' | transloco }}
          </h3>
          @if (loading()) {
            <p class="text-slate-400 text-sm py-10 text-center">{{ 'common.loading' | transloco }}</p>
          } @else {
            <p-chart type="line" [data]="lineData()" [options]="lineOptions" height="260px" />
          }
        </div>

        <div class="dt-card p-4 min-w-0">
          <h3 class="m-0 mb-3 text-base font-semibold text-slate-800">
            {{ 'reports.revenue.byMethod' | transloco }}
          </h3>
          @if ((data()?.byMethod ?? []).length === 0) {
            <p class="text-slate-400 text-sm py-10 text-center">{{ 'table.empty' | transloco }}</p>
          } @else {
            <p-chart type="doughnut" [data]="pieData()" [options]="pieOptions" height="240px" />
          }
        </div>
      </div>

      <div class="dt-card p-0 overflow-hidden">
        <p-table [value]="data()?.series ?? []" styleClass="p-datatable-sm" dataKey="period">
          <ng-template #header>
            <tr>
              <th>{{ 'reports.revenue.period' | transloco }}</th>
              <th class="text-right!">{{ 'reports.revenue.totalTreatment' | transloco }}</th>
              <th class="text-right!">{{ 'reports.revenue.totalCollected' | transloco }}</th>
              <th class="text-right!">{{ 'reports.revenue.treatmentCount' | transloco }}</th>
            </tr>
          </ng-template>
          <ng-template #body let-row>
            <tr>
              <td>{{ row.periodLabel }}</td>
              <td class="text-right!">{{ row.treatmentRevenue | money }}</td>
              <td class="text-right!">{{ row.collected | money }}</td>
              <td class="text-right!">{{ row.treatmentCount }}</td>
            </tr>
          </ng-template>
          <ng-template #emptymessage>
            <tr>
              <td colspan="4" class="text-center text-slate-400 py-6">
                {{ 'table.empty' | transloco }}
              </td>
            </tr>
          </ng-template>
        </p-table>
      </div>
    </div>
  `,
})
export class RevenueReportComponent {
  private readonly api = inject(ReportsApiService);
  private readonly transloco = inject(TranslocoService);
  private readonly translation = injectTranslationSignal();

  readonly query = input.required<ReportQuery>();

  protected readonly data = signal<RevenueReportDto | null>(null);
  protected readonly loading = signal(true);

  protected readonly lineOptions = moneyChartOptions();
  protected readonly pieOptions = pieChartOptions(true);

  protected readonly lineData = computed(() => {
    this.translation();
    const series = this.data()?.series ?? [];
    return {
      labels: series.map((p) => p.periodLabel),
      datasets: [
        {
          label: this.transloco.translate('reports.revenue.totalTreatment'),
          data: series.map((p) => p.treatmentRevenue),
          borderColor: CHART_COLORS.primary,
          backgroundColor: CHART_COLORS.primarySoft,
          tension: 0.35,
          borderWidth: 2,
          fill: true,
        },
        {
          label: this.transloco.translate('reports.revenue.totalCollected'),
          data: series.map((p) => p.collected),
          borderColor: CHART_COLORS.emerald,
          backgroundColor: CHART_COLORS.emeraldSoft,
          tension: 0.35,
          borderWidth: 2,
          fill: true,
        },
      ],
    };
  });

  protected readonly pieData = computed(() => {
    this.translation();
    const rows = this.data()?.byMethod ?? [];
    return {
      labels: rows.map((r) =>
        this.transloco.translate(PAYMENT_METHOD_KEYS[r.method] ?? 'paymentMethod.cash'),
      ),
      datasets: [{ data: rows.map((r) => r.total), backgroundColor: paletteFor(rows.length) }],
    };
  });

  constructor() {
    effect(() => {
      const query = this.query();
      untracked(() => this.load(query));
    });
  }

  private load(query: ReportQuery): void {
    this.loading.set(true);
    this.api.revenue(query).subscribe({
      next: (data) => {
        this.data.set(data);
        this.loading.set(false);
      },
      error: () => {
        this.data.set(null);
        this.loading.set(false);
      },
    });
  }
}
