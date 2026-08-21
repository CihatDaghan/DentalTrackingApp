import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ConsentCreateRequest,
  ConsentFormDto,
  ConsentSendSmsResult,
  ConsentSignRequest,
  ConsentTemplateDto,
  ConsentTemplateListItemDto,
  ConsentTemplateUpsertRequest,
  PublicConsentSignRequest,
  PublicConsentViewDto,
} from './clinical-api.models';

/** Dijital onam uclari: sablon CRUD, hasta onamlari, tablet imza, SMS, public imza. */
@Injectable({ providedIn: 'root' })
export class ConsentsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/v1`;

  // --- Sablonlar ------------------------------------------------------------

  templates(): Observable<ConsentTemplateListItemDto[]> {
    return this.http.get<ConsentTemplateListItemDto[]>(`${this.baseUrl}/consent-templates`);
  }

  template(id: number): Observable<ConsentTemplateDto> {
    return this.http.get<ConsentTemplateDto>(`${this.baseUrl}/consent-templates/${id}`);
  }

  createTemplate(request: ConsentTemplateUpsertRequest): Observable<ConsentTemplateDto> {
    return this.http.post<ConsentTemplateDto>(`${this.baseUrl}/consent-templates`, request);
  }

  updateTemplate(id: number, request: ConsentTemplateUpsertRequest): Observable<ConsentTemplateDto> {
    return this.http.put<ConsentTemplateDto>(`${this.baseUrl}/consent-templates/${id}`, request);
  }

  deleteTemplate(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/consent-templates/${id}`);
  }

  // --- Hasta onamlari -------------------------------------------------------

  patientConsents(patientId: number): Observable<ConsentFormDto[]> {
    return this.http.get<ConsentFormDto[]>(`${this.baseUrl}/patients/${patientId}/consents`);
  }

  createConsent(patientId: number, request: ConsentCreateRequest): Observable<ConsentFormDto> {
    return this.http.post<ConsentFormDto>(
      `${this.baseUrl}/patients/${patientId}/consents`,
      request,
    );
  }

  consent(id: number): Observable<ConsentFormDto> {
    return this.http.get<ConsentFormDto>(`${this.baseUrl}/consents/${id}`);
  }

  /** Klinik ici tablet imzasi: imza PNG (base64) -> PDF uretilir, durum Signed olur. */
  sign(id: number, request: ConsentSignRequest): Observable<ConsentFormDto> {
    return this.http.post<ConsentFormDto>(`${this.baseUrl}/consents/${id}/sign`, request);
  }

  sendSms(id: number): Observable<ConsentSendSmsResult> {
    return this.http.post<ConsentSendSmsResult>(`${this.baseUrl}/consents/${id}/send-sms`, null);
  }

  pdfBlob(id: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/consents/${id}/pdf`, { responseType: 'blob' });
  }

  // --- Public (auth'suz) imza sayfasi ---------------------------------------

  publicView(token: string): Observable<PublicConsentViewDto> {
    return this.http.get<PublicConsentViewDto>(`${this.baseUrl}/public/consents/${token}`);
  }

  publicSign(token: string, request: PublicConsentSignRequest): Observable<unknown> {
    return this.http.post(`${this.baseUrl}/public/consents/${token}/sign`, request);
  }
}
