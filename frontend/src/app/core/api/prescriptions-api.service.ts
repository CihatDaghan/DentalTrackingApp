import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  DrugDto,
  PrescriptionCreateRequest,
  PrescriptionDto,
  PrescriptionSaveAsTemplateRequest,
  PrescriptionTemplateDto,
  PrescriptionTemplateUpsertRequest,
} from './prescription-api.models';

/** Recete uclari: ilac katalogu, hasta receteleri, sablonlar, PDF. */
@Injectable({ providedIn: 'root' })
export class PrescriptionsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/v1`;

  // --- Ilac katalogu --------------------------------------------------------

  /** Ad veya barkoda gore arama (min 2 karakter cagiranin sorumlulugunda). */
  drugs(search: string): Observable<DrugDto[]> {
    const params = new HttpParams().set('search', search);
    return this.http.get<DrugDto[]>(`${this.baseUrl}/drugs`, { params });
  }

  // --- Receteler ------------------------------------------------------------

  list(patientId: number): Observable<PrescriptionDto[]> {
    return this.http.get<PrescriptionDto[]>(`${this.baseUrl}/patients/${patientId}/prescriptions`);
  }

  create(patientId: number, request: PrescriptionCreateRequest): Observable<PrescriptionDto> {
    return this.http.post<PrescriptionDto>(
      `${this.baseUrl}/patients/${patientId}/prescriptions`,
      request,
    );
  }

  get(id: number): Observable<PrescriptionDto> {
    return this.http.get<PrescriptionDto>(`${this.baseUrl}/prescriptions/${id}`);
  }

  /** PDF — Authorization interceptor'dan gectigi icin blob ile cekilir. */
  pdfBlob(id: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/prescriptions/${id}/pdf`, { responseType: 'blob' });
  }

  // --- Sablonlar ------------------------------------------------------------

  templates(): Observable<PrescriptionTemplateDto[]> {
    return this.http.get<PrescriptionTemplateDto[]>(`${this.baseUrl}/prescription-templates`);
  }

  createTemplate(request: PrescriptionTemplateUpsertRequest): Observable<PrescriptionTemplateDto> {
    return this.http.post<PrescriptionTemplateDto>(
      `${this.baseUrl}/prescription-templates`,
      request,
    );
  }

  updateTemplate(
    id: number,
    request: PrescriptionTemplateUpsertRequest,
  ): Observable<PrescriptionTemplateDto> {
    return this.http.put<PrescriptionTemplateDto>(
      `${this.baseUrl}/prescription-templates/${id}`,
      request,
    );
  }

  deleteTemplate(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/prescription-templates/${id}`);
  }

  saveAsTemplate(
    prescriptionId: number,
    request: PrescriptionSaveAsTemplateRequest,
  ): Observable<PrescriptionTemplateDto> {
    return this.http.post<PrescriptionTemplateDto>(
      `${this.baseUrl}/prescriptions/${prescriptionId}/save-as-template`,
      request,
    );
  }
}
