import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  PatientTeethDto,
  TreatmentAddRequest,
  TreatmentBulkPriceRequest,
  TreatmentRecordDto,
  TreatmentRecordStatus,
  TreatmentStatusRequest,
  TreatmentUpdateRequest,
} from './treatment-api.models';

/** Hasta tedavi kayitlari + dis semasi verisi uclari. */
@Injectable({ providedIn: 'root' })
export class TreatmentsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/v1`;

  teeth(patientId: number): Observable<PatientTeethDto> {
    return this.http.get<PatientTeethDto>(`${this.baseUrl}/patients/${patientId}/teeth`);
  }

  list(patientId: number, status?: TreatmentRecordStatus | null): Observable<TreatmentRecordDto[]> {
    let params = new HttpParams();
    if (status != null) {
      params = params.set('status', status);
    }
    return this.http.get<TreatmentRecordDto[]>(`${this.baseUrl}/patients/${patientId}/treatments`, {
      params,
    });
  }

  add(patientId: number, request: TreatmentAddRequest): Observable<TreatmentRecordDto[]> {
    return this.http.post<TreatmentRecordDto[]>(
      `${this.baseUrl}/patients/${patientId}/treatments`,
      request,
    );
  }

  update(id: number, request: TreatmentUpdateRequest): Observable<TreatmentRecordDto> {
    return this.http.put<TreatmentRecordDto>(`${this.baseUrl}/treatments/${id}`, request);
  }

  updateStatus(id: number, request: TreatmentStatusRequest): Observable<TreatmentRecordDto> {
    return this.http.put<TreatmentRecordDto>(`${this.baseUrl}/treatments/${id}/status`, request);
  }

  bulkPrice(request: TreatmentBulkPriceRequest): Observable<TreatmentRecordDto[]> {
    return this.http.put<TreatmentRecordDto[]>(`${this.baseUrl}/treatments/bulk-price`, request);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/treatments/${id}`);
  }
}
