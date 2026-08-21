import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from './api.models';
import {
  AppointmentsReportDto,
  CollectionsReportDto,
  DashboardSummaryDto,
  DebtorRowDto,
  DoctorPerformanceReportDto,
  IncomeExpenseReportDto,
  ReportQuery,
  RevenueReportDto,
  TreatmentsReportDto,
} from './reports-api.models';

/** Excel disa aktarimlarinda kullanilan rapor anahtarlari (arka uc route: /reports/{report}/export). */
export type ReportKey =
  | 'revenue'
  | 'income-expense'
  | 'doctor-performance'
  | 'collections'
  | 'treatments'
  | 'appointments'
  | 'debtors';

function toParams(query: ReportQuery, extra: Record<string, string | number> = {}): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries({ ...query, ...extra })) {
    if (value !== null && value !== undefined && value !== '') {
      params = params.set(key, String(value));
    }
  }
  return params;
}

/**
 * Rapor uclari (`report.view`) + Excel disa aktarim (`report.export`) + dashboard ozeti.
 * Tarih alanlari "yyyy-MM-dd" (yerel gun) olarak gonderilir.
 */
@Injectable({ providedIn: 'root' })
export class ReportsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/v1`;

  revenue(query: ReportQuery): Observable<RevenueReportDto> {
    return this.http.get<RevenueReportDto>(`${this.baseUrl}/reports/revenue`, {
      params: toParams(query),
    });
  }

  incomeExpense(query: ReportQuery): Observable<IncomeExpenseReportDto> {
    return this.http.get<IncomeExpenseReportDto>(`${this.baseUrl}/reports/income-expense`, {
      params: toParams(query),
    });
  }

  doctorPerformance(query: ReportQuery): Observable<DoctorPerformanceReportDto> {
    return this.http.get<DoctorPerformanceReportDto>(`${this.baseUrl}/reports/doctor-performance`, {
      params: toParams(query),
    });
  }

  collections(query: ReportQuery): Observable<CollectionsReportDto> {
    return this.http.get<CollectionsReportDto>(`${this.baseUrl}/reports/collections`, {
      params: toParams(query),
    });
  }

  treatments(query: ReportQuery): Observable<TreatmentsReportDto> {
    return this.http.get<TreatmentsReportDto>(`${this.baseUrl}/reports/treatments`, {
      params: toParams(query),
    });
  }

  appointments(query: ReportQuery): Observable<AppointmentsReportDto> {
    return this.http.get<AppointmentsReportDto>(`${this.baseUrl}/reports/appointments`, {
      params: toParams(query),
    });
  }

  debtors(query: ReportQuery, page = 1, pageSize = 25): Observable<PagedResult<DebtorRowDto>> {
    return this.http.get<PagedResult<DebtorRowDto>>(`${this.baseUrl}/reports/debtors`, {
      params: toParams(query, { page, pageSize }),
    });
  }

  /** Excel (xlsx) indirimi — Authorization gerektigi icin blob olarak cekilir. */
  export(report: ReportKey, query: ReportQuery): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/reports/${report}/export`, {
      params: toParams(query, { format: 'xlsx' }),
      responseType: 'blob',
    });
  }

  /** Dashboard tek cagri ozeti (KPI + bekleyen isler + 30 gun trend + dogum gunleri). */
  dashboardSummary(date?: string | null, clinicId?: number | null): Observable<DashboardSummaryDto> {
    let params = new HttpParams();
    if (date) {
      params = params.set('date', date);
    }
    if (clinicId != null) {
      params = params.set('clinicId', String(clinicId));
    }
    return this.http.get<DashboardSummaryDto>(`${this.baseUrl}/dashboard/summary`, { params });
  }
}
