import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  PagedResult,
  RecallConvertRequest,
  RecallCreateRequest,
  RecallDto,
  RecallStatus,
  RecallStatusRequest,
} from './api.models';

@Injectable({ providedIn: 'root' })
export class RecallsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/v1/recalls`;

  list(options: {
    patientId?: number | null;
    status?: RecallStatus | null;
    from?: string | null;
    to?: string | null;
    page?: number;
    pageSize?: number;
  }): Observable<PagedResult<RecallDto>> {
    let params = new HttpParams()
      .set('page', options.page ?? 1)
      .set('pageSize', options.pageSize ?? 50);
    if (options.patientId != null) {
      params = params.set('patientId', options.patientId);
    }
    if (options.status != null) {
      params = params.set('status', options.status);
    }
    if (options.from) {
      params = params.set('from', options.from);
    }
    if (options.to) {
      params = params.set('to', options.to);
    }
    return this.http.get<PagedResult<RecallDto>>(this.baseUrl, { params });
  }

  create(request: RecallCreateRequest): Observable<RecallDto> {
    return this.http.post<RecallDto>(this.baseUrl, request);
  }

  updateStatus(id: number, request: RecallStatusRequest): Observable<RecallDto> {
    return this.http.put<RecallDto>(`${this.baseUrl}/${id}/status`, request);
  }

  convert(id: number, request: RecallConvertRequest): Observable<RecallDto> {
    return this.http.post<RecallDto>(`${this.baseUrl}/${id}/convert`, request);
  }
}
