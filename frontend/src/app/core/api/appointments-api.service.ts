import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AppointmentDto,
  AppointmentStatusRequest,
  AppointmentUpsertRequest,
  DoctorDto,
  DoctorWorkingHourDto,
} from './api.models';

@Injectable({ providedIn: 'root' })
export class AppointmentsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/v1`;

  list(options: {
    from: string;
    to: string;
    clinicId?: number | null;
    doctorIds?: number[] | null;
  }): Observable<AppointmentDto[]> {
    let params = new HttpParams().set('from', options.from).set('to', options.to);
    if (options.clinicId != null) {
      params = params.set('clinicId', options.clinicId);
    }
    if (options.doctorIds?.length) {
      params = params.set('doctorIds', options.doctorIds.join(','));
    }
    return this.http.get<AppointmentDto[]>(`${this.baseUrl}/appointments`, { params });
  }

  get(id: number): Observable<AppointmentDto> {
    return this.http.get<AppointmentDto>(`${this.baseUrl}/appointments/${id}`);
  }

  create(request: AppointmentUpsertRequest): Observable<AppointmentDto> {
    return this.http.post<AppointmentDto>(`${this.baseUrl}/appointments`, request);
  }

  update(id: number, request: AppointmentUpsertRequest): Observable<AppointmentDto> {
    return this.http.put<AppointmentDto>(`${this.baseUrl}/appointments/${id}`, request);
  }

  updateStatus(id: number, request: AppointmentStatusRequest): Observable<AppointmentDto> {
    return this.http.put<AppointmentDto>(`${this.baseUrl}/appointments/${id}/status`, request);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/appointments/${id}`);
  }

  doctors(): Observable<DoctorDto[]> {
    return this.http.get<DoctorDto[]>(`${this.baseUrl}/doctors`);
  }

  workingHours(doctorId?: number | null): Observable<DoctorWorkingHourDto[]> {
    let params = new HttpParams();
    if (doctorId != null) {
      params = params.set('doctorId', doctorId);
    }
    return this.http.get<DoctorWorkingHourDto[]>(`${this.baseUrl}/working-hours`, { params });
  }
}
