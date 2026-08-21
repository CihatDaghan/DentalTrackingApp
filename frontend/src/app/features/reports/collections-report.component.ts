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
import { CollectionsReportDto, ReportQuery } from '../../core/api/reports-api.models';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { CHART_COLORS, moneyChartOptions, paletteFor, pieChartOptions } from '../../shared/utils/chart-theme';
import { injectTranslationSignal } from '../../shared/utils/transloco-signal';
import { agingColor, PAYMENT_METHOD_KEYS } from './report-labels';

/** Tahsilat raporu: donem tahsilati + 4 kovali yaslandirma gorseli + tablo. */
@Component({
  selector: 'app-collections-report',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ChartModule, TableModule, TranslocoPipe, MoneyPipe],
  template: `
    <div class="flex flex-col gap-4" data-testid="report-collections">
      <div class="grid grid-cols-1 sm:grid-cols-3 gap-3">
        <div class="dt-card p-3 flex flex-col gap-1">
          <span class="text-[11px] font-medium text-slate-500 uppercase tracking-wide">
            {{ 'reports.collections.totalCollected' | transloco }}
          </span>
          <span class="text-xl font-semibold text-emerald-600" data-testid="collections-total">
            {{ data()?.totalCollected ?? 0 | money }}
          </span>
        </div>
        <div class="dt-card p-3 flex flex-col gap-1">
          <span class="text-[11px] font-medium text-slate-500 uppercase tracking-wide">
            {{ 'reports.collections.count' | transloco }}
          </span>
          <span class="text-xl font-semibold text-slate-700">{{ data()?.totalCount ?? 0 }}</span>
        </div>
        <div class="dt-card p-3 flex flex-col gap-1">
          <span class="text-[11px] font-medium text-slate-500 uppercase tracking-wide">
            {{ 'reports.collections.outstanding' | transloco }}
          </span>
          <span class="text-xl font-semibold text-rose-600">
            {{ data()?.totalOutstanding ?? 0 | money }}
          </span>
        </div>
      </div>

      <!-- Yaslandirma kovalari: 0-30 yesil ... 90+ kirmizi -->
      <div class="dt-card p-4" data-testid="aging-buckets">
        <h3 class="m-0 mb-3 text-base font-semibold text-slate-800">
          {{ 'reports.collections.aging' | transloco }}
        </h3>
        <div class="grid grid-cols-2 lg:grid-cols-4 gap-3">
          @for (bucket of data()?.aging ?? []; track bucket.bucket) {
            <div
              class="rounded-xl border p-3 flex flex-col gap-1"
              [style.border-color]="color(bucket.bucket) + '55'"
              [style.background]="color(bucket.bucket) + '12'"
              [attr.data-testid]="'aging-' + bucket.bucket"
            >
              <span class="text-[11px] font-semibold uppercase tracking-wide" [style.color]="color(bucket.bucket)">
                {{ 'reports.collections.bucket.' + bucketKey(bucket.bucket) | transloco }}
              </span>
              <span class="text-lg font-semibold text-slate-800">{{ bucket.amount | money }}</span>
              <span class="text-[11px] text-slate-500">
                {{ 'reports.collections.patientCount' | transloco: { count: bucket.patientCount } }}
              </span>
              <div class="mt-1 h-1.5 rounded-full bg-slate-200 overflow-hidden">
                <div
                  class="h-full rounded-full"
                  [style.width.%]="share(bucket.amount)"
                  [style.background]="color(bucket.bucket)"
                ></div>
              </div>
            </div>
          }
        </div>
      </div>

      <div class="grid grid-cols-1 xl:grid-cols-3 gap-4">
        <div class="dt-card p-4 xl:col-span-2 min-w-0">
          <h3 class="m-0 mb-3 text-base font-semibold text-slate-800">
            {{ 'reports.collections.chartTitle' | transloco }}
          </h3>
          @if (loading()) {
            <p class="text-slate-400 text-sm py-10 text-center">{{ 'common.loading' | transloco }}</p>
          } @else {
            <p-chart type="bar" [data]="barData()" [options]="barOptions" height="240px" />
          }
        </div>
        <div class="dt-card p-4 min-w-0">
          <h3 class="m-0 mb-3 text-base font-semibold text-slate-800">
            {{ 'reports.revenue.byMethod' | transloco }}
          </h3>
          @if ((data()?.byMethod ?? []).length === 0) {
            <p class="text-slate-400 text-sm py-10 text-center">{{ 'table.empty' | transloco }}</p>
          } @else {
            <p-chart type="doughnut" [data]="pieData()" [options]="pieOptions" height="220px" />
          }
        </div>
      </div>

      <div class="dt-card p-0 overflow-hidden">
        <p-table [value]="data()?.series ?? []" styleClass="p-datatable-sm" dataKey="period">
          <ng-template #header>
            <tr>
              <th>{{ 'reports.revenue.period' | transloco }}</th>
              <th class="text-right!">{{ 'reports.collections.totalCollected' | transloco }}</th>
              <th class="text-right!">{{ 'reports.collections.count' | transloco }}</th>
            </tr>
          </ng-template>
          <ng-template #body let-row>
            <tr>
              <td>{{ row.periodLabel }}</td>
              <td class="text-right! text-emerald-700">{{ row.total | money }}</td>
              <td class="text-right!">{{ row.count }}</td>
            </tr>
          </ng-template>
          <ng-template #emptymessage>
            <tr>
              <td colspan="3" class="text-center text-slate-400 py-6">
                {{ 'table.empty' | transloco }}
              </td>
            </tr>
          </ng-template>
        </p-table>
      </div>
    </div>
  `,
})
export class CollectionsReportComponent {
  private readonly api = inject(ReportsApiService);
  private readonly transloco = inject(TranslocoService);
  private readonly translation = injectTranslationSignal();

  readonly query = input.required<ReportQuery>();

  protected readonly data = signal<CollectionsReportDto | null>(null);
  protected readonly loading = signal(true);

  protected readonly barOptions = moneyChartOptions();
  protected readonly pieOptions = pieChartOptions(true);

  private readonly agingMax = computed(() =>
    Math.max(1, ...(this.data()?.aging ?? []).map((b) => b.amount)),
  );

  protected readonly barData = computed(() => {
    this.translation();
    const series = this.data()?.series ?? [];
    return {
      labels: series.map((p) => p.periodLabel),
      datasets: [
        {
          label: this.transloco.translate('reports.collections.totalCollected'),
          data: series.map((p) => p.total),
          backgroundColor: CHART_COLORS.primary,
          borderRadius: 4,
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

  protected color(bucket: string): string {
    return agingColor(bucket);
  }

  /** "90+" gibi degerler i18n anahtarina cevrilir. */
  protected bucketKey(bucket: string): string {
    return bucket === '90+' ? 'over90' : bucket.replace('-', 'to');
  }

  protected share(amount: number): number {
    return Math.round((amount / this.agingMax()) * 100);
  }

  constructor() {
    effect(() => {
      const query = this.query();
      untracked(() => this.load(query));
    });
  }

  private load(query: ReportQuery): void {
    this.loading.set(true);
    this.api.collections(query).subscribe({
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
