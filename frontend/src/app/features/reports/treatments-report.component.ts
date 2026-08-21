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
import { TranslocoPipe } from '@jsverse/transloco';
import { ReportsApiService } from '../../core/api/reports-api.service';
import { ReportQuery, TreatmentsReportDto } from '../../core/api/reports-api.models';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { paletteFor, pieChartOptions } from '../../shared/utils/chart-theme';

/** Tedavi raporu: kategori kirilimi pastasi + tedavi bazinda adet/tutar tablosu. */
@Component({
  selector: 'app-treatments-report',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ChartModule, TableModule, TranslocoPipe, MoneyPipe],
  template: `
    <div class="flex flex-col gap-4" data-testid="report-treatments">
      <div class="grid grid-cols-1 sm:grid-cols-2 gap-3">
        <div class="dt-card p-3 flex flex-col gap-1">
          <span class="text-[11px] font-medium text-slate-500 uppercase tracking-wide">
            {{ 'reports.treatments.totalCount' | transloco }}
          </span>
          <span class="text-xl font-semibold text-slate-700" data-testid="treatments-count">
            {{ data()?.totalCount ?? 0 }}
          </span>
        </div>
        <div class="dt-card p-3 flex flex-col gap-1">
          <span class="text-[11px] font-medium text-slate-500 uppercase tracking-wide">
            {{ 'reports.treatments.totalNet' | transloco }}
          </span>
          <span class="text-xl font-semibold text-slate-700">
            {{ data()?.totalNetAmount ?? 0 | money }}
          </span>
        </div>
      </div>

      <div class="grid grid-cols-1 xl:grid-cols-3 gap-4">
        <div class="dt-card p-4 min-w-0">
          <h3 class="m-0 mb-3 text-base font-semibold text-slate-800">
            {{ 'reports.treatments.byCategory' | transloco }}
          </h3>
          @if (loading()) {
            <p class="text-slate-400 text-sm py-10 text-center">{{ 'common.loading' | transloco }}</p>
          } @else if ((data()?.byCategory ?? []).length === 0) {
            <p class="text-slate-400 text-sm py-10 text-center">{{ 'table.empty' | transloco }}</p>
          } @else {
            <p-chart type="pie" [data]="pieData()" [options]="pieOptions" height="240px" />
          }
        </div>

        <div class="dt-card p-0 overflow-hidden xl:col-span-2">
          <p-table [value]="data()?.rows ?? []" styleClass="p-datatable-sm" dataKey="treatmentDefinitionId">
            <ng-template #header>
              <tr>
                <th>{{ 'reports.treatments.code' | transloco }}</th>
                <th>{{ 'reports.treatments.name' | transloco }}</th>
                <th>{{ 'reports.treatments.category' | transloco }}</th>
                <th class="text-right!">{{ 'reports.treatments.count' | transloco }}</th>
                <th class="text-right!">{{ 'reports.treatments.gross' | transloco }}</th>
                <th class="text-right!">{{ 'reports.treatments.discount' | transloco }}</th>
                <th class="text-right!">{{ 'reports.treatments.net' | transloco }}</th>
              </tr>
            </ng-template>
            <ng-template #body let-row>
              <tr>
                <td class="text-slate-500 font-mono text-xs">{{ row.code }}</td>
                <td class="font-medium text-slate-700">{{ row.name }}</td>
                <td class="text-slate-500">{{ row.categoryName }}</td>
                <td class="text-right!">{{ row.count }}</td>
                <td class="text-right!">{{ row.grossAmount | money }}</td>
                <td class="text-right! text-amber-600">{{ row.discountAmount | money }}</td>
                <td class="text-right! font-medium">{{ row.netAmount | money }}</td>
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
    </div>
  `,
})
export class TreatmentsReportComponent {
  private readonly api = inject(ReportsApiService);

  readonly query = input.required<ReportQuery>();

  protected readonly data = signal<TreatmentsReportDto | null>(null);
  protected readonly loading = signal(true);
  protected readonly pieOptions = pieChartOptions(true);

  protected readonly pieData = computed(() => {
    const rows = this.data()?.byCategory ?? [];
    return {
      labels: rows.map((r) => r.categoryName),
      datasets: [{ data: rows.map((r) => r.netAmount), backgroundColor: paletteFor(rows.length) }],
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
    this.api.treatments(query).subscribe({
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
