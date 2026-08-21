import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  PagedResult,
  PatientDto,
  PatientListItemDto,
  PatientSummaryDto,
  PatientUpsertRequest,
  TableQuery,
} from './api.models';

@Injectable({ providedIn: 'root' })
export class PatientsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/v1/patients`;

  list(query: TableQuery): Observable<PagedResult<PatientListItemDto>> {
    let params = new HttpParams()
      .set('page', query.page)
      .set('pageSize', query.pageSize);
    if (query.sort) {
      params = params.set('sort', query.sort);
    }
    if (query.search) {
      params = params.set('search', query.search);
    }
    return this.http.get<PagedResult<PatientListItemDto>>(this.baseUrl, { params });
  }

  get(id: number): Observable<PatientDto> {
    return this.http.get<PatientDto>(`${this.baseUrl}/${id}`);
  }

  getSummary(id: number): Observable<PatientSummaryDto> {
    return this.http.get<PatientSummaryDto>(`${this.baseUrl}/${id}/summary`);
  }

  create(request: PatientUpsertRequest): Observable<PatientDto> {
    return this.http.post<PatientDto>(this.baseUrl, request);
  }

  update(id: number, request: PatientUpsertRequest): Observable<PatientDto> {
    return this.http.put<PatientDto>(`${this.baseUrl}/${id}`, request);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
