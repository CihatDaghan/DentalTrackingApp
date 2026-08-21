import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  LoginRequestDto,
  LoginResponseDto,
  LogoutRequestDto,
  RefreshRequestDto,
  SelectClinicRequestDto,
  TokenPairDto,
} from './auth-api.models';

@Injectable({ providedIn: 'root' })
export class AuthApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/v1/auth`;

  login(request: LoginRequestDto): Observable<LoginResponseDto> {
    return this.http.post<LoginResponseDto>(`${this.baseUrl}/login`, request);
  }

  selectClinic(request: SelectClinicRequestDto): Observable<TokenPairDto> {
    return this.http.post<TokenPairDto>(`${this.baseUrl}/select-clinic`, request);
  }

  refresh(request: RefreshRequestDto): Observable<TokenPairDto> {
    return this.http.post<TokenPairDto>(`${this.baseUrl}/refresh`, request);
  }

  logout(request: LogoutRequestDto): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/logout`, request);
  }
}
