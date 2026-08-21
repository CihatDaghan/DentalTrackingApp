import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from './api.models';
import {
  CashRegisterDailySummaryDto,
  CompanyDto,
  CompanyUpsertRequest,
  DiscountRequest,
  ExpenseCategoryDto,
  ExpenseDto,
  ExpenseUpsertRequest,
  LedgerStatementDto,
  PaymentCreateRequest,
  PaymentDto,
  PaymentPlanCreateRequest,
  PaymentPlanDto,
} from './finance-api.models';

/** Finans uclari: cari ekstre, tahsilat, indirim, taksit plani, kasa, gider, kurum. */
@Injectable({ providedIn: 'root' })
export class FinanceApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/v1`;

  // --- Cari ekstre ----------------------------------------------------------

  patientLedger(patientId: number): Observable<LedgerStatementDto> {
    return this.http.get<LedgerStatementDto>(`${this.baseUrl}/patients/${patientId}/ledger`);
  }

  companyLedger(companyId: number): Observable<LedgerStatementDto> {
    return this.http.get<LedgerStatementDto>(`${this.baseUrl}/companies/${companyId}/ledger`);
  }

  // --- Tahsilat -------------------------------------------------------------

  payments(query: {
    patientId?: number;
    companyId?: number;
    from?: string;
    to?: string;
    page?: number;
    pageSize?: number;
  }): Observable<PagedResult<PaymentDto>> {
    let params = new HttpParams()
      .set('page', query.page ?? 1)
      .set('pageSize', query.pageSize ?? 50);
    if (query.patientId != null) {
      params = params.set('patientId', query.patientId);
    }
    if (query.companyId != null) {
      params = params.set('companyId', query.companyId);
    }
    if (query.from) {
      params = params.set('from', query.from);
    }
    if (query.to) {
      params = params.set('to', query.to);
    }
    return this.http.get<PagedResult<PaymentDto>>(`${this.baseUrl}/payments`, { params });
  }

  createPayment(request: PaymentCreateRequest): Observable<PaymentDto> {
    return this.http.post<PaymentDto>(`${this.baseUrl}/payments`, request);
  }

  deletePayment(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/payments/${id}`);
  }

  applyDiscount(request: DiscountRequest): Observable<unknown> {
    return this.http.post(`${this.baseUrl}/payments/discount`, request);
  }

  // --- Taksit plani ---------------------------------------------------------

  patientPaymentPlans(patientId: number): Observable<PaymentPlanDto[]> {
    return this.http.get<PaymentPlanDto[]>(`${this.baseUrl}/patients/${patientId}/payment-plans`);
  }

  createPaymentPlan(request: PaymentPlanCreateRequest): Observable<PaymentPlanDto> {
    return this.http.post<PaymentPlanDto>(`${this.baseUrl}/payment-plans`, request);
  }

  deletePaymentPlan(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/payment-plans/${id}`);
  }

  // --- Kasa -----------------------------------------------------------------

  cashRegister(date: string): Observable<CashRegisterDailySummaryDto> {
    const params = new HttpParams().set('date', date);
    return this.http.get<CashRegisterDailySummaryDto>(`${this.baseUrl}/cash-register`, { params });
  }

  // --- Gider ----------------------------------------------------------------

  expenses(query: {
    from?: string;
    to?: string;
    page?: number;
    pageSize?: number;
  }): Observable<PagedResult<ExpenseDto>> {
    let params = new HttpParams();
    if (query.from) {
      params = params.set('from', query.from);
    }
    if (query.to) {
      params = params.set('to', query.to);
    }
    params = params.set('page', query.page ?? 1).set('pageSize', query.pageSize ?? 50);
    return this.http.get<PagedResult<ExpenseDto>>(`${this.baseUrl}/expenses`, { params });
  }

  createExpense(request: ExpenseUpsertRequest): Observable<ExpenseDto> {
    return this.http.post<ExpenseDto>(`${this.baseUrl}/expenses`, request);
  }

  deleteExpense(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/expenses/${id}`);
  }

  expenseCategories(): Observable<ExpenseCategoryDto[]> {
    return this.http.get<ExpenseCategoryDto[]>(`${this.baseUrl}/expense-categories`);
  }

  // --- Kurum (firma) --------------------------------------------------------

  companies(query?: {
    search?: string;
    page?: number;
    pageSize?: number;
  }): Observable<PagedResult<CompanyDto>> {
    let params = new HttpParams()
      .set('page', query?.page ?? 1)
      .set('pageSize', query?.pageSize ?? 50);
    if (query?.search) {
      params = params.set('search', query.search);
    }
    return this.http.get<PagedResult<CompanyDto>>(`${this.baseUrl}/companies`, { params });
  }

  createCompany(request: CompanyUpsertRequest): Observable<CompanyDto> {
    return this.http.post<CompanyDto>(`${this.baseUrl}/companies`, request);
  }

  updateCompany(id: number, request: CompanyUpsertRequest): Observable<CompanyDto> {
    return this.http.put<CompanyDto>(`${this.baseUrl}/companies/${id}`, request);
  }

  deleteCompany(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/companies/${id}`);
  }
}
