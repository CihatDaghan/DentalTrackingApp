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
import { IncomeExpenseReportDto, ReportQuery } from '../../core/api/reports-api.models';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { CHART_COLORS, moneyChartOptions, paletteFor, pieChartOptions } from '../../shared/utils/chart-theme';
import { injectTranslationSignal } from '../../shared/utils/transloco-signal';

/** Gelir-gider raporu: aylik gelir/gider cubugu, gider kategori pastasi, net kar karti. */
@Component({
  selector: 'app-income-expense-report',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ChartModule, TableModule, TranslocoPipe, MoneyPipe],
  template: `
    <div class="flex flex-col gap-4" data-testid="report-income-expense">
      <div class="grid grid-cols-1 sm:grid-cols-3 gap-3">
        <div class="dt-card p-3 flex flex-col gap-1">
          <span class="text-[11px] font-medium text-slate-500 uppercase tracking-wide">
            {{ 'reports.incomeExpense.income' | transloco }}
          </span>
          <span class="text-xl font-semibold text-emerald-600" data-testid="ie-income">
            {{ data()?.totalIncome ?? 0 | money }}
          </span>
        </div>
        <div class="dt-card p-3 flex flex-col gap-1">
          <span class="text-[11px] font-medium text-slate-500 uppercase tracking-wide">
            {{ 'reports.incomeExpense.expense' | transloco }}
          </span>
          <span class="text-xl font-semibold text-rose-600">
            {{ data()?.totalExpense ?? 0 | money }}
          </span>
        </div>
        <div
          class="dt-card p-3 flex flex-col gap-1"
          [class.border-emerald-200!]="(data()?.netProfit ?? 0) >= 0"
          [class.border-rose-200!]="(data()?.netProfit ?? 0) < 0"
        >
          <span class="text-[11px] font-medium text-blue-600 uppercase tracking-wide">
            {{ 'reports.incomeExpense.net' | transloco }}
          </span>
          <span
            class="text-xl font-semibold"
            [class.text-emerald-700]="(data()?.netProfit ?? 0) >= 0"
            [class.text-rose-700]="(data()?.netProfit ?? 0) < 0"
            data-testid="ie-net"
          >
            {{ data()?.netProfit ?? 0 | money }}
          </span>
        </div>
      </div>

      <div class="grid grid-cols-1 xl:grid-cols-3 gap-4">
        <div class="dt-card p-4 xl:col-span-2 min-w-0">
          <h3 class="m-0 mb-3 text-base font-semibold text-slate-800">
            {{ 'reports.incomeExpense.chartTitle' | transloco }}
          </h3>
          @if (loading()) {
            <p class="text-slate-400 text-sm py-10 text-center">{{ 'common.loading' | transloco }}</p>
          } @else {
            <p-chart type="bar" [data]="barData()" [options]="barOptions" height="260px" />
          }
        </div>

        <div class="dt-card p-4 min-w-0">
          <h3 class="m-0 mb-3 text-base font-semibold text-slate-800">
            {{ 'reports.incomeExpense.byCategory' | transloco }}
          </h3>
          @if ((data()?.expensesByCategory ?? []).length === 0) {
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
              <th class="text-right!">{{ 'reports.incomeExpense.income' | transloco }}</th>
              <th class="text-right!">{{ 'reports.incomeExpense.expense' | transloco }}</th>
              <th class="text-right!">{{ 'reports.incomeExpense.net' | transloco }}</th>
            </tr>
          </ng-template>
          <ng-template #body let-row>
            <tr>
              <td>{{ row.periodLabel }}</td>
              <td class="text-right! text-emerald-700">{{ row.income | money }}</td>
              <td class="text-right! text-rose-700">{{ row.expense | money }}</td>
              <td class="text-right! font-medium">{{ row.net | money }}</td>
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
export class IncomeExpenseReportComponent {
  private readonly api = inject(ReportsApiService);
  private readonly transloco = inject(TranslocoService);
  private readonly translation = injectTranslationSignal();

  readonly query = input.required<ReportQuery>();

  protected readonly data = signal<IncomeExpenseReportDto | null>(null);
  protected readonly loading = signal(true);

  protected readonly barOptions = moneyChartOptions();
  protected readonly pieOptions = pieChartOptions(true);

  protected readonly barData = computed(() => {
    this.translation();
    const series = this.data()?.series ?? [];
    return {
      labels: series.map((p) => p.periodLabel),
      datasets: [
        {
          label: this.transloco.translate('reports.incomeExpense.income'),
          data: series.map((p) => p.income),
          backgroundColor: CHART_COLORS.emerald,
          borderRadius: 4,
        },
        {
          label: this.transloco.translate('reports.incomeExpense.expense'),
          data: series.map((p) => p.expense),
          backgroundColor: CHART_COLORS.rose,
          borderRadius: 4,
        },
      ],
    };
  });

  protected readonly pieData = computed(() => {
    const rows = this.data()?.expensesByCategory ?? [];
    return {
      labels: rows.map((r) => r.categoryName),
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
    this.api.incomeExpense(query).subscribe({
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
