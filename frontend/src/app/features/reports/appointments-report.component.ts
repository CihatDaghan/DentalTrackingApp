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
import { AppointmentsReportDto, ReportQuery } from '../../core/api/reports-api.models';
import { StatusTagComponent } from '../../shared/components/status-tag/status-tag.component';
import { CHART_COLORS, countChartOptions, paletteFor, pieChartOptions } from '../../shared/utils/chart-theme';
import { injectTranslationSignal } from '../../shared/utils/transloco-signal';
import { APPOINTMENT_STATUS_KEYS } from './report-labels';

/** Randevu raporu: doluluk %, durum dagilimi, no-show trend cizgisi, iptal sayilari. */
@Component({
  selector: 'app-appointments-report',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ChartModule, TableModule, TranslocoPipe, DecimalPipe, StatusTagComponent],
  template: `
    <div class="flex flex-col gap-4" data-testid="report-appointments">
      <div class="grid grid-cols-2 lg:grid-cols-4 gap-3">
        <div class="dt-card p-3 flex flex-col gap-1">
          <span class="text-[11px] font-medium text-slate-500 uppercase tracking-wide">
            {{ 'reports.appointments.total' | transloco }}
          </span>
          <span class="text-xl font-semibold text-slate-700" data-testid="appointments-total">
            {{ data()?.totalCount ?? 0 }}
          </span>
        </div>
        <div class="dt-card p-3 flex flex-col gap-1">
          <span class="text-[11px] font-medium text-slate-500 uppercase tracking-wide">
            {{ 'reports.appointments.occupancy' | transloco }}
          </span>
          <span class="text-xl font-semibold text-blue-700">
            %{{ data()?.occupancyRate ?? 0 | number: '1.0-1' }}
          </span>
        </div>
        <div class="dt-card p-3 flex flex-col gap-1">
          <span class="text-[11px] font-medium text-slate-500 uppercase tracking-wide">
            {{ 'reports.appointments.noShow' | transloco }}
          </span>
          <span class="text-xl font-semibold text-rose-600">
            {{ data()?.noShowCount ?? 0 }}
            <span class="text-sm font-normal text-slate-400">
              (%{{ data()?.noShowRate ?? 0 | number: '1.0-1' }})
            </span>
          </span>
        </div>
        <div class="dt-card p-3 flex flex-col gap-1">
          <span class="text-[11px] font-medium text-slate-500 uppercase tracking-wide">
            {{ 'reports.appointments.cancelled' | transloco }}
          </span>
          <span class="text-xl font-semibold text-amber-600">{{ data()?.cancelledCount ?? 0 }}</span>
        </div>
      </div>

      <div class="grid grid-cols-1 xl:grid-cols-3 gap-4">
        <div class="dt-card p-4 xl:col-span-2 min-w-0">
          <h3 class="m-0 mb-3 text-base font-semibold text-slate-800">
            {{ 'reports.appointments.trendTitle' | transloco }}
          </h3>
          @if (loading()) {
            <p class="text-slate-400 text-sm py-10 text-center">{{ 'common.loading' | transloco }}</p>
          } @else {
            <p-chart type="line" [data]="trendData()" [options]="trendOptions" height="250px" />
          }
        </div>

        <div class="dt-card p-4 min-w-0">
          <h3 class="m-0 mb-3 text-base font-semibold text-slate-800">
            {{ 'reports.appointments.byStatus' | transloco }}
          </h3>
          @if ((data()?.byStatus ?? []).length === 0) {
            <p class="text-slate-400 text-sm py-10 text-center">{{ 'table.empty' | transloco }}</p>
          } @else {
            <p-chart type="doughnut" [data]="statusData()" [options]="pieOptions" height="200px" />
            <div class="flex flex-wrap gap-2 mt-3">
              @for (s of data()?.byStatus ?? []; track s.status) {
                <span class="inline-flex items-center gap-1 text-xs text-slate-600">
                  <app-status-tag kind="appointment" [value]="s.status" />
                  <b>{{ s.count }}</b>
                </span>
              }
            </div>
          }
        </div>
      </div>

      <div class="dt-card p-0 overflow-hidden">
        <p-table [value]="data()?.trend ?? []" styleClass="p-datatable-sm" dataKey="period">
          <ng-template #header>
            <tr>
              <th>{{ 'reports.revenue.period' | transloco }}</th>
              <th class="text-right!">{{ 'reports.appointments.total' | transloco }}</th>
              <th class="text-right!">{{ 'reports.appointments.completed' | transloco }}</th>
              <th class="text-right!">{{ 'reports.appointments.noShow' | transloco }}</th>
              <th class="text-right!">{{ 'reports.appointments.cancelled' | transloco }}</th>
              <th class="text-right!">{{ 'reports.appointments.occupancy' | transloco }}</th>
            </tr>
          </ng-template>
          <ng-template #body let-row>
            <tr>
              <td>{{ row.periodLabel }}</td>
              <td class="text-right!">{{ row.total }}</td>
              <td class="text-right! text-emerald-700">{{ row.completed }}</td>
              <td class="text-right! text-rose-600">{{ row.noShow }}</td>
              <td class="text-right! text-amber-600">{{ row.cancelled }}</td>
              <td class="text-right!">%{{ row.occupancyRate | number: '1.0-1' }}</td>
            </tr>
          </ng-template>
          <ng-template #emptymessage>
            <tr>
              <td colspan="6" class="text-center text-slate-400 py-6">
                {{ 'table.empty' | transloco }}
              </td>
            </tr>
          </ng-template>
        </p-table>
      </div>
    </div>
  `,
})
export class AppointmentsReportComponent {
  private readonly api = inject(ReportsApiService);
  private readonly transloco = inject(TranslocoService);
  private readonly translation = injectTranslationSignal();

  readonly query = input.required<ReportQuery>();

  protected readonly data = signal<AppointmentsReportDto | null>(null);
  protected readonly loading = signal(true);
  protected readonly pieOptions = pieChartOptions(false);
  protected readonly trendOptions = countChartOptions();

  protected readonly trendData = computed(() => {
    this.translation();
    const trend = this.data()?.trend ?? [];
    return {
      labels: trend.map((p) => p.periodLabel),
      datasets: [
        {
          label: this.transloco.translate('reports.appointments.noShow'),
          data: trend.map((p) => p.noShow),
          borderColor: CHART_COLORS.rose,
          backgroundColor: 'rgba(239, 68, 68, 0.12)',
          tension: 0.35,
          borderWidth: 2,
          fill: true,
        },
        {
          label: this.transloco.translate('reports.appointments.cancelled'),
          data: trend.map((p) => p.cancelled),
          borderColor: CHART_COLORS.amber,
          backgroundColor: 'rgba(245, 158, 11, 0.12)',
          tension: 0.35,
          borderWidth: 2,
          fill: true,
        },
        {
          label: this.transloco.translate('reports.appointments.completed'),
          data: trend.map((p) => p.completed),
          borderColor: CHART_COLORS.emerald,
          backgroundColor: CHART_COLORS.emeraldSoft,
          tension: 0.35,
          borderWidth: 2,
          fill: true,
        },
      ],
    };
  });

  protected readonly statusData = computed(() => {
    this.translation();
    const rows = this.data()?.byStatus ?? [];
    return {
      labels: rows.map((r) =>
        this.transloco.translate(APPOINTMENT_STATUS_KEYS[r.status] ?? 'appointmentStatus.scheduled'),
      ),
      datasets: [{ data: rows.map((r) => r.count), backgroundColor: paletteFor(rows.length) }],
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
    this.api.appointments(query).subscribe({
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
