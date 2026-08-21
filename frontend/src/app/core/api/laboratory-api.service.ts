import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from './api.models';
import {
  LabCaseDto,
  LabCaseHistoryDto,
  LabCaseListQuery,
  LabCaseStatusChangeRequest,
  LabCaseUpsertRequest,
  LaboratoryDto,
  LaboratoryUpsertRequest,
} from './laboratory-api.models';

/** Laboratuvar uclari: firma CRUD + lab vakasi CRUD/durum/gecmis. */
@Injectable({ providedIn: 'root' })
export class LaboratoryApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/v1`;

  // --- Lab firmalari --------------------------------------------------------

  laboratories(): Observable<LaboratoryDto[]> {
    return this.http.get<LaboratoryDto[]>(`${this.baseUrl}/laboratories`);
  }

  createLaboratory(request: LaboratoryUpsertRequest): Observable<LaboratoryDto> {
    return this.http.post<LaboratoryDto>(`${this.baseUrl}/laboratories`, request);
  }

  updateLaboratory(id: number, request: LaboratoryUpsertRequest): Observable<LaboratoryDto> {
    return this.http.put<LaboratoryDto>(`${this.baseUrl}/laboratories/${id}`, request);
  }

  deleteLaboratory(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/laboratories/${id}`);
  }

  // --- Lab vakalari ---------------------------------------------------------

  cases(query: LabCaseListQuery = {}): Observable<PagedResult<LabCaseDto>> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query)) {
      if (value !== null && value !== undefined && value !== '') {
        params = params.set(key, String(value));
      }
    }
    return this.http.get<PagedResult<LabCaseDto>>(`${this.baseUrl}/lab-cases`, { params });
  }

  patientCases(patientId: number): Observable<LabCaseDto[]> {
    return this.http.get<LabCaseDto[]>(`${this.baseUrl}/patients/${patientId}/lab-cases`);
  }

  case(id: number): Observable<LabCaseDto> {
    return this.http.get<LabCaseDto>(`${this.baseUrl}/lab-cases/${id}`);
  }

  createCase(request: LabCaseUpsertRequest): Observable<LabCaseDto> {
    return this.http.post<LabCaseDto>(`${this.baseUrl}/lab-cases`, request);
  }

  updateCase(id: number, request: LabCaseUpsertRequest): Observable<LabCaseDto> {
    return this.http.put<LabCaseDto>(`${this.baseUrl}/lab-cases/${id}`, request);
  }

  deleteCase(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/lab-cases/${id}`);
  }

  changeStatus(id: number, request: LabCaseStatusChangeRequest): Observable<LabCaseDto> {
    return this.http.put<LabCaseDto>(`${this.baseUrl}/lab-cases/${id}/status`, request);
  }

  history(id: number): Observable<LabCaseHistoryDto[]> {
    return this.http.get<LabCaseHistoryDto[]>(`${this.baseUrl}/lab-cases/${id}/history`);
  }
}
