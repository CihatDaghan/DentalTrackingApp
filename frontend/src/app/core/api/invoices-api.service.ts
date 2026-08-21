import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from './api.models';
import {
  GibTaxpayerDto,
  InvoiceCancelRequest,
  InvoiceDraftRequest,
  InvoiceDto,
  InvoiceListItemDto,
  InvoiceListQuery,
  InvoicePreviewDto,
} from './invoice-api.models';

/**
 * e-Belge uclari: karar onizleme, taslak, UBL uretimi, gonderim, iptal, indirmeler.
 * Belge tipi (e-Fatura / e-Arsiv / e-SMM) daima arka uctaki karar motorunca belirlenir.
 */
@Injectable({ providedIn: 'root' })
export class InvoicesApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/v1`;

  // --- Liste / detay ---------------------------------------------------------

  list(query: InvoiceListQuery): Observable<PagedResult<InvoiceListItemDto>> {
    let params = new HttpParams()
      .set('page', query.page ?? 1)
      .set('pageSize', query.pageSize ?? 25);
    if (query.status != null) {
      params = params.set('status', query.status);
    }
    if (query.from) {
      params = params.set('from', query.from);
    }
    if (query.to) {
      params = params.set('to', query.to);
    }
    return this.http.get<PagedResult<InvoiceListItemDto>>(`${this.baseUrl}/invoices`, { params });
  }

  get(id: number): Observable<InvoiceDto> {
    return this.http.get<InvoiceDto>(`${this.baseUrl}/invoices/${id}`);
  }

  // --- Olusturma akisi -------------------------------------------------------

  /** Karar motoru onizlemesi: belge tipi + senaryo + gerekce + eksik alan uyarilari. */
  preview(request: InvoiceDraftRequest): Observable<InvoicePreviewDto> {
    return this.http.post<InvoicePreviewDto>(`${this.baseUrl}/invoices/preview`, request);
  }

  create(request: InvoiceDraftRequest): Observable<InvoiceDto> {
    return this.http.post<InvoiceDto>(`${this.baseUrl}/invoices`, request);
  }

  /** Draft -> UblGenerated: belge numarasi + ETTN atanir. */
  generateUbl(id: number): Observable<InvoiceDto> {
    return this.http.post<InvoiceDto>(`${this.baseUrl}/invoices/${id}/generate-ubl`, {});
  }

  send(id: number, sendNow = true): Observable<InvoiceDto> {
    const params = new HttpParams().set('sendNow', sendNow);
    return this.http.post<InvoiceDto>(`${this.baseUrl}/invoices/${id}/send`, {}, { params });
  }

  cancel(id: number, request: InvoiceCancelRequest): Observable<InvoiceDto> {
    return this.http.post<InvoiceDto>(`${this.baseUrl}/invoices/${id}/cancel`, request);
  }

  // --- Indirmeler (Authorization basligi gerektigi icin blob) -----------------

  ubl(id: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/invoices/${id}/ubl`, { responseType: 'blob' });
  }

  pdf(id: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/invoices/${id}/pdf`, { responseType: 'blob' });
  }

  // --- GIB mukellef aynasi ---------------------------------------------------

  gibTaxpayer(vkn: string): Observable<GibTaxpayerDto> {
    return this.http.get<GibTaxpayerDto>(`${this.baseUrl}/gib-taxpayers/${vkn}`);
  }
}
