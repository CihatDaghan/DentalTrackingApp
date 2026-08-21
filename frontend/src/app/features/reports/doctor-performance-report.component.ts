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
import { DecimalPipe } from '@angular/common';
import { ChartModule } from 'primeng/chart';
import { TableModule } from 'primeng/table';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { ReportsApiService } from '../../core/api/reports-api.service';
import { DoctorPerformanceReportDto, ReportQuery } from '../../core/api/reports-api.models';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { CHART_COLORS, moneyChartOptions } from '../../shared/utils/chart-theme';
import { injectTranslationSignal } from '../../shared/utils/transloco-signal';

/** Hekim performansi: hekime gore ciro yatay cubugu + detay tablosu. */
@Component({
  selector: 'app-doctor-performance-report',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ChartModule, TableModule, TranslocoPipe, MoneyPipe, DecimalPipe],
  template: `
    <div class="flex flex-col gap-4" data-testid="report-doctor-performance">
      <div class="dt-card p-4 min-w-0">
        <h3 class="m-0 mb-3 text-base font-semibold text-slate-800">
          {{ 'reports.doctorPerformance.chartTitle' | transloco }}
        </h3>
        @if (loading()) {
          <p class="text-slate-400 text-sm py-10 text-center">{{ 'common.loading' | transloco }}</p>
        } @else if (rows().length === 0) {
          <p class="text-slate-400 text-sm py-10 text-center">{{ 'table.empty' | transloco }}</p>
        } @else {
          <p-chart type="bar" [data]="barData()" [options]="barOptions" [height]="chartHeight()" />
        }
      </div>

      <div class="dt-card p-0 overflow-hidden">
        <p-table [value]="rows()" styleClass="p-datatable-sm" dataKey="doctorUserId">
          <ng-template #header>
            <tr>
              <th>{{ 'reports.doctorPerformance.doctor' | transloco }}</th>
              <th>{{ 'reports.doctorPerformance.branch' | transloco }}</th>
              <th class="text-right!">{{ 'reports.doctorPerformance.patients' | transloco }}</th>
              <th class="text-right!">{{ 'reports.doctorPerformance.treatments' | transloco }}</th>
              <th class="text-right!">{{ 'reports.doctorPerformance.produced' | transloco }}</th>
              <th class="text-right!">{{ 'reports.doctorPerformance.collected' | transloco }}</th>
              <th class="text-right!">{{ 'reports.doctorPerformance.appointments' | transloco }}</th>
              <th class="text-right!">{{ 'reports.doctorPerformance.noShowRate' | transloco }}</th>
            </tr>
          </ng-template>
          <ng-template #body let-row>
            <tr>
              <td class="font-medium text-slate-700">{{ row.doctorName }}</td>
              <td class="text-slate-500">{{ row.branch || '—' }}</td>
              <td class="text-right!">{{ row.patientCount }}</td>
              <td class="text-right!">{{ row.treatmentCount }}</td>
              <td class="text-right!">{{ row.producedRevenue | money }}</td>
              <td class="text-right! text-emerald-700">{{ row.collectedRevenue | money }}</td>
              <td class="text-right!">{{ row.appointmentCount }}</td>
              <td class="text-right!" [class.text-rose-600]="row.noShowRate > 10">
                %{{ row.noShowRate | number: '1.0-1' }}
              </td>
            </tr>
          </ng-template>
          <ng-template #emptymessage>
            <tr>
              <td colspan="8" class="text-center text-slate-400 py-6">
                {{ 'table.empty' | transloco }}
              </td>
            </tr>
          </ng-template>
        </p-table>
      </div>
    </div>
  `,
})
export class DoctorPerformanceReportComponent {
  private readonly api = inject(ReportsApiService);
  private readonly transloco = inject(TranslocoService);
  private readonly translation = injectTranslationSignal();

  readonly query = input.required<ReportQuery>();

  protected readonly data = signal<DoctorPerformanceReportDto | null>(null);
  protected readonly loading = signal(true);

  protected readonly rows = computed(() => this.data()?.rows ?? []);
  protected readonly barOptions = moneyChartOptions({ horizontal: true });
  protected readonly chartHeight = computed(() => `${Math.max(180, this.rows().length * 52)}px`);

  protected readonly barData = computed(() => {
    this.translation();
    const rows = this.rows();
    return {
      labels: rows.map((r) => r.doctorName),
      datasets: [
        {
          label: this.transloco.translate('reports.doctorPerformance.produced'),
          data: rows.map((r) => r.producedRevenue),
          backgroundColor: CHART_COLORS.primary,
          borderRadius: 4,
        },
        {
          label: this.transloco.translate('reports.doctorPerformance.collected'),
          data: rows.map((r) => r.collectedRevenue),
          backgroundColor: CHART_COLORS.emerald,
          borderRadius: 4,
        },
      ],
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
    this.api.doctorPerformance(query).subscribe({
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
