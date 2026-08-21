import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  PaymentLinkCreateRequest,
  PaymentLinkDto,
  PublicPaymentStatusDto,
  PublicPaymentViewDto,
} from './messaging-api.models';

/**
 * Odeme linki uclari. Public (auth'suz) uclar token ile calisir;
 * kart bilgisi hicbir zaman bu uygulamadan gecmez — saglayicinin 3DS sayfasinda alinir.
 */
@Injectable({ providedIn: 'root' })
export class PaymentLinksApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/v1`;

  create(request: PaymentLinkCreateRequest): Observable<PaymentLinkDto> {
    return this.http.post<PaymentLinkDto>(`${this.baseUrl}/payment-links`, request);
  }

  list(patientId?: number | null): Observable<PaymentLinkDto[]> {
    let params = new HttpParams();
    if (patientId != null) {
      params = params.set('patientId', patientId);
    }
    return this.http.get<PaymentLinkDto[]>(`${this.baseUrl}/payment-links`, { params });
  }

  get(id: number): Observable<PaymentLinkDto> {
    return this.http.get<PaymentLinkDto>(`${this.baseUrl}/payment-links/${id}`);
  }

  // --- Public (auth'suz) ----------------------------------------------------

  publicView(token: string): Observable<PublicPaymentViewDto> {
    return this.http.get<PublicPaymentViewDto>(`${this.baseUrl}/public/payments/${token}`);
  }

  publicStatus(token: string): Observable<PublicPaymentStatusDto> {
    return this.http.get<PublicPaymentStatusDto>(
      `${this.baseUrl}/public/payments/${token}/status`,
    );
  }
}
