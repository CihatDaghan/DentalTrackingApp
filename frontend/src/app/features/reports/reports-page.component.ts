import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { MessageService } from 'primeng/api';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { ReportsApiService } from '../../core/api/reports-api.service';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { downloadBlob } from '../../shared/utils/file-download';
import { injectTranslationSignal } from '../../shared/utils/transloco-signal';
import { ReportFilterBarComponent } from './report-filter-bar.component';
import {
  initialFilterState,
  ReportFilterState,
  ReportKeyName,
  REPORT_TABS,
  ReportTabDef,
  toReportQuery,
} from './report-filter';
import { RevenueReportComponent } from './revenue-report.component';
import { IncomeExpenseReportComponent } from './income-expense-report.component';
import { DoctorPerformanceReportComponent } from './doctor-performance-report.component';
import { CollectionsReportComponent } from './collections-report.component';
import { TreatmentsReportComponent } from './treatments-report.component';
import { AppointmentsReportComponent } from './appointments-report.component';
import { DebtorsReportComponent } from './debtors-report.component';

/**
 * Raporlar kabugu (/app/reports): sol dikey menude 7 rapor, ustte ortak filtre
 * cubugu ve "Excel indir". Aktif rapor `?r=` ile derin baglanabilir.
 */
@Component({
  selector: 'app-reports-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    TranslocoPipe,
    PageHeaderComponent,
    ReportFilterBarComponent,
    RevenueReportComponent,
    IncomeExpenseReportComponent,
    DoctorPerformanceReportComponent,
    CollectionsReportComponent,
    TreatmentsReportComponent,
    AppointmentsReportComponent,
    DebtorsReportComponent,
  ],
  templateUrl: './reports-page.component.html',
  styleUrl: './reports-page.component.scss',
})
export class ReportsPageComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(ReportsApiService);
  private readonly messageService = inject(MessageService);
  private readonly transloco = inject(TranslocoService);
  private readonly translation = injectTranslationSignal();

  protected readonly tabs = REPORT_TABS;

  private readonly queryParams = toSignal(this.route.queryParamMap, {
    initialValue: this.route.snapshot.queryParamMap,
  });

  private readonly manualTab = signal<ReportKeyName | null>(null);

  protected readonly activeKey = computed<ReportKeyName>(() => {
    const manual = this.manualTab();
    if (manual) {
      return manual;
    }
    const fromUrl = this.queryParams().get('r');
    return REPORT_TABS.some((t) => t.key === fromUrl) ? (fromUrl as ReportKeyName) : 'revenue';
  });

  protected readonly activeTab = computed<ReportTabDef>(
    () => REPORT_TABS.find((t) => t.key === this.activeKey())!,
  );

  protected readonly filter = signal<ReportFilterState>(initialFilterState());
  protected readonly exporting = signal(false);

  /** Aktif rapora gore sadelestirilmis API sorgusu. */
  protected readonly query = computed(() => toReportQuery(this.filter(), this.activeTab()));

  protected readonly subtitle = computed(() => {
    this.translation();
    const state = this.filter();
    if (!this.activeTab().showDateRange || !state.from || !state.to) {
      return '';
    }
    return `${state.from.toLocaleDateString('tr-TR')} — ${state.to.toLocaleDateString('tr-TR')}`;
  });

  protected select(key: ReportKeyName): void {
    this.manualTab.set(key);
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { r: key },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  protected exportExcel(): void {
    const key = this.activeKey();
    this.exporting.set(true);
    this.api.export(key, this.query()).subscribe({
      next: (blob) => {
        const stamp = new Date().toISOString().slice(0, 10);
        downloadBlob(blob, `${key}-${stamp}.xlsx`);
        this.exporting.set(false);
        this.messageService.add({
          severity: 'success',
          summary: this.transloco.translate('reports.exportReady'),
          life: 3000,
        });
      },
      error: () => this.exporting.set(false),
    });
  }
}
