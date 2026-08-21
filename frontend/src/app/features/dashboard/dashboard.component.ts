import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { ChartModule } from 'primeng/chart';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AuthStore } from '../../core/auth/auth.store';
import { HasPermissionDirective } from '../../core/auth/has-permission.directive';
import { AppointmentsApiService } from '../../core/api/appointments-api.service';
import { AppointmentDto, DoctorDto, parseUtc } from '../../core/api/api.models';
import { ReportsApiService } from '../../core/api/reports-api.service';
import { DashboardSummaryDto } from '../../core/api/reports-api.models';
import { StatusTagComponent } from '../../shared/components/status-tag/status-tag.component';
import { TrDatePipe } from '../../shared/pipes/tr-date.pipe';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { toUtcNaive } from '../calendar/appointment-utils';
import { InvoiceStatus } from '../../core/api/invoice-api.models';
import { OutboundMessageState } from '../../core/api/messaging-api.models';
import { CHART_COLORS, moneyChartOptions } from '../../shared/utils/chart-theme';
import { injectTranslationSignal } from '../../shared/utils/transloco-signal';

/** KPI karti tanimi — `permission` bos ise herkese gorunur. */
interface KpiCard {
  key: string;
  icon: string;
  value: number;
  permission: string;
}

/** "Bekleyen isler" seridi: sayac + ilgili sayfaya link. */
interface PendingTask {
  key: string;
  icon: string;
  count: number;
  route: string;
  queryParams: Record<string, string>;
}

/**
 * Dashboard: `GET /dashboard/summary` tek cagrisindan beslenen KPI kartlari,
 * son 30 gun ciro cizgisi, bugunun randevulari, bekleyen isler ve dogum gunleri.
 */
@Component({
  selector: 'app-dashboard',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    ChartModule,
    TranslocoPipe,
    StatusTagComponent,
    TrDatePipe,
    MoneyPipe,
    HasPermissionDirective,
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent implements OnInit {
  protected readonly authStore = inject(AuthStore);
  private readonly appointmentsApi = inject(AppointmentsApiService);
  private readonly reportsApi = inject(ReportsApiService);
  private readonly transloco = inject(TranslocoService);
  private readonly translation = injectTranslationSignal();

  protected readonly summary = signal<DashboardSummaryDto | null>(null);
  protected readonly appointments = signal<AppointmentDto[]>([]);
  protected readonly doctors = signal<DoctorDto[]>([]);
  protected readonly loading = signal(true);
  protected readonly pendingLoading = signal(true);

  /** Ciro kartlari `report.view`, tahsilat/bakiye kartlari `payment.read` ister. */
  protected readonly kpis = computed<KpiCard[]>(() => {
    const s = this.summary();
    return [
      {
        key: 'todayRevenue',
        icon: 'fa-solid fa-turkish-lira-sign',
        value: s?.todayRevenue ?? 0,
        permission: 'report.view',
      },
      {
        key: 'monthRevenue',
        icon: 'fa-solid fa-chart-line',
        value: s?.monthRevenue ?? 0,
        permission: 'report.view',
      },
      {
        key: 'todayCollection',
        icon: 'fa-solid fa-hand-holding-dollar',
        value: s?.todayCollections ?? 0,
        permission: 'payment.read',
      },
      {
        key: 'openBalance',
        icon: 'fa-solid fa-scale-balanced',
        value: s?.totalOutstanding ?? 0,
        permission: 'payment.read',
      },
    ];
  });

  protected readonly pendingTasks = computed((): PendingTask[] => {
    const work = this.summary()?.pendingWork;
    return [
      {
        key: 'overdueLabCases',
        icon: 'fa-solid fa-flask',
        count: work?.overdueLabCases ?? 0,
        route: '/app/laboratory',
        queryParams: { overdueOnly: 'true' },
      },
      {
        key: 'lowStock',
        icon: 'fa-solid fa-boxes-stacked',
        count: work?.lowStockItems ?? 0,
        route: '/app/inventory',
        queryParams: { lowOnly: 'true' },
      },
      {
        key: 'invoiceErrors',
        icon: 'fa-solid fa-file-invoice',
        count: work?.eInvoiceErrors ?? 0,
        route: '/app/invoices',
        queryParams: { status: String(InvoiceStatus.Error) },
      },
      {
        key: 'failedMessages',
        icon: 'fa-solid fa-comment-slash',
        count: work?.failedMessages ?? 0,
        route: '/app/messaging',
        queryParams: { tab: 'history', state: String(OutboundMessageState.Failed) },
      },
      {
        key: 'unsignedConsents',
        icon: 'fa-solid fa-file-signature',
        count: work?.unsignedConsents ?? 0,
        route: '/app/patients',
        queryParams: {},
      },
      {
        key: 'pendingEnabiz',
        icon: 'fa-solid fa-notes-medical',
        count: work?.pendingEnabizPackets ?? 0,
        route: '/app/settings/integrations',
        queryParams: {},
      },
    ];
  });

  protected readonly totalPending = computed(() =>
    this.pendingTasks().reduce((sum, t) => sum + t.count, 0),
  );

  protected readonly birthdays = computed(() => this.summary()?.birthdayPatients ?? []);

  /** Dogum gunu kutlamasi: toplu gonderim sihirbazi, dogum ayi on secili. */
  protected readonly birthdayQueryParams = computed(() => ({
    tab: 'bulk',
    birthMonth: String(new Date().getMonth() + 1),
  }));

  protected readonly revenueChartData = computed(() => {
    this.translation();
    const points = this.summary()?.last30DaysRevenue ?? [];
    return {
      labels: points.map((p) => {
        const [, month, day] = p.date.split('-');
        return `${day}.${month}`;
      }),
      datasets: [
        {
          label: this.transloco.translate('dashboard.revenueTrend'),
          data: points.map((p) => p.amount),
          borderColor: CHART_COLORS.primary,
          backgroundColor: CHART_COLORS.primarySoft,
          fill: true,
          tension: 0.35,
          borderWidth: 2,
          pointRadius: 0,
          pointHoverRadius: 4,
        },
      ],
    };
  });

  protected readonly revenueChartOptions = moneyChartOptions();

  ngOnInit(): void {
    this.reportsApi.dashboardSummary().subscribe({
      next: (summary) => {
        this.summary.set(summary);
        this.pendingLoading.set(false);
      },
      error: () => this.pendingLoading.set(false),
    });

    const dayStart = new Date();
    dayStart.setHours(0, 0, 0, 0);
    const dayEnd = new Date(dayStart);
    dayEnd.setDate(dayEnd.getDate() + 1);

    this.appointmentsApi.list({ from: toUtcNaive(dayStart), to: toUtcNaive(dayEnd) }).subscribe({
      next: (appointments) => {
        this.appointments.set(
          [...appointments].sort(
            (a, b) => parseUtc(a.startUtc).getTime() - parseUtc(b.startUtc).getTime(),
          ),
        );
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });

    this.appointmentsApi.doctors().subscribe({
      next: (doctors) => this.doctors.set(doctors),
      error: () => this.doctors.set([]),
    });
  }

  protected doctorColor(id: number): string {
    return this.doctors().find((d) => d.id === id)?.color ?? '#3b82f6';
  }
}
