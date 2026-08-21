import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { EpicrisisCreateRequest, EpicrisisDto, IcdCodeDto } from './epicrisis-api.models';

/** Epikriz uclari + ICD kodu arama (tani autocomplete'i buradan beslenir). */
@Injectable({ providedIn: 'root' })
export class EpicrisisApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/v1`;

  list(patientId: number): Observable<EpicrisisDto[]> {
    return this.http.get<EpicrisisDto[]>(`${this.baseUrl}/patients/${patientId}/epicrisis`);
  }

  create(patientId: number, request: EpicrisisCreateRequest): Observable<EpicrisisDto> {
    return this.http.post<EpicrisisDto>(`${this.baseUrl}/patients/${patientId}/epicrisis`, request);
  }

  get(id: number): Observable<EpicrisisDto> {
    return this.http.get<EpicrisisDto>(`${this.baseUrl}/epicrisis/${id}`);
  }

  /** PDF — Authorization interceptor'dan gectigi icin blob ile cekilir. */
  pdfBlob(id: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/epicrisis/${id}/pdf`, { responseType: 'blob' });
  }

  /** ICD-10 kodu arama (kod ya da ad). */
  icdCodes(search: string): Observable<IcdCodeDto[]> {
    const params = new HttpParams().set('search', search);
    return this.http.get<IcdCodeDto[]>(`${this.baseUrl}/icd-codes`, { params });
  }
}
