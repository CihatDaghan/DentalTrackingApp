import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from './api.models';
import { AnnouncementSeverity } from './notifications-api.service';
import { EnabizMode, TenantLegalType, TenantStatus } from './settings-api.models';

export interface TenantUsageDto {
  userCount: number;
  patientCount: number;
  appointmentCount: number;
  invoiceCount: number;
  treatmentCount: number;
  lastActivityUtc: string | null;
}

export interface TenantListItemDto {
  id: number;
  name: string;
  legalType: TenantLegalType;
  status: TenantStatus;
  planCode: string | null;
  planName: string | null;
  createdAtUtc: string;
  trialEndsAtUtc: string | null;
  isDeleted: boolean;
  usage: TenantUsageDto;
}

export interface TenantClinicDto {
  id: number;
  name: string;
  city: string | null;
  phone: string | null;
  ckysCode: string | null;
}

export interface TenantOwnerDto {
  id: number;
  email: string;
  fullName: string;
  isActive: boolean;
}

export interface TenantDetailDto {
  id: number;
  name: string;
  legalType: TenantLegalType;
  taxNumber: string | null;
  taxOffice: string | null;
  hasHealthTourismAuthorization: boolean;
  status: TenantStatus;
  planCode: string | null;
  planName: string | null;
  createdAtUtc: string;
  trialEndsAtUtc: string | null;
  isDeleted: boolean;
  usage: TenantUsageDto;
  clinics: TenantClinicDto[];
  owners: TenantOwnerDto[];
}

export interface CreateTenantRequest {
  clinicName: string;
  legalType: TenantLegalType;
  adminEmail: string;
  adminFirstName: string;
  adminLastName: string;
  adminPassword: string;
  taxNumber?: string | null;
  phone?: string | null;
}

export interface CreateTenantResult {
  tenantId: number;
  clinicId: number;
  adminUserId: number;
}

export interface TenantUpdateRequest {
  name?: string | null;
  planCode?: string | null;
  status?: TenantStatus | null;
  trialEndsAtUtc?: string | null;
}

export interface ImpersonationResponse {
  accessToken: string;
  expiresInSeconds: number;
  expiresAtUtc: string;
  tenantId: number;
  tenantName: string;
  impersonatedUserId: number;
  impersonatedUserEmail: string;
  auditLogId: number;
}

export interface PlanDto {
  id: number;
  code: string;
  name: string;
  maxUsers: number;
  maxPatients: number;
  monthlySmsQuota: number;
  storageGb: number;
  priceMonthly: number;
  isActive: boolean;
  sortOrder: number;
  tenantCount: number;
}

export interface PlanUpsertRequest {
  code: string;
  name: string;
  maxUsers: number;
  maxPatients: number;
  monthlySmsQuota: number;
  storageGb: number;
  priceMonthly: number;
  isActive: boolean;
  sortOrder: number;
}

export interface AnnouncementDto {
  id: number;
  title: string;
  body: string;
  severity: AnnouncementSeverity;
  startsAtUtc: string;
  endsAtUtc: string | null;
  isActive: boolean;
  targetTenantId: number | null;
  targetTenantName: string | null;
  createdAtUtc: string;
}

export interface AnnouncementUpsertRequest {
  title: string;
  body: string;
  severity: AnnouncementSeverity;
  startsAtUtc?: string | null;
  endsAtUtc?: string | null;
  isActive: boolean;
  targetTenantId?: number | null;
}

export interface IntegrationHealthRowDto {
  integrationKey: string;
  providerKey: string | null;
  environment: string;
  isEnabled: boolean;
  hasCredentials: boolean;
  lastSuccessUtc: string | null;
  lastFailureUtc: string | null;
  callCount24h: number;
  failureCount24h: number;
  lastError: string | null;
}

export interface TenantIntegrationHealthDto {
  tenantId: number;
  tenantName: string;
  status: TenantStatus;
  integrations: IntegrationHealthRowDto[];
  enabizMode: EnabizMode;
  enabizRequestedMode: EnabizMode;
  ktsRegistered: boolean;
}

export interface TenantListQuery {
  search?: string | null;
  status?: TenantStatus | null;
  planCode?: string | null;
  includeDeleted?: boolean;
  page?: number;
  pageSize?: number;
}

/** Super admin uclari (`SuperAdmin` claim'i zorunlu). */
@Injectable({ providedIn: 'root' })
export class AdminApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/v1/admin`;

  // --- Kiracilar ------------------------------------------------------------

  tenants(query: TenantListQuery = {}): Observable<PagedResult<TenantListItemDto>> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query)) {
      if (value !== null && value !== undefined && value !== '') {
        params = params.set(key, String(value));
      }
    }
    return this.http.get<PagedResult<TenantListItemDto>>(`${this.baseUrl}/tenants`, { params });
  }

  tenant(id: number): Observable<TenantDetailDto> {
    return this.http.get<TenantDetailDto>(`${this.baseUrl}/tenants/${id}`);
  }

  createTenant(request: CreateTenantRequest): Observable<CreateTenantResult> {
    return this.http.post<CreateTenantResult>(`${this.baseUrl}/tenants`, request);
  }

  updateTenant(id: number, request: TenantUpdateRequest): Observable<TenantDetailDto> {
    return this.http.put<TenantDetailDto>(`${this.baseUrl}/tenants/${id}`, request);
  }

  deleteTenant(id: number): Observable<void> {
    const params = new HttpParams().set('confirm', 'true');
    return this.http.delete<void>(`${this.baseUrl}/tenants/${id}`, { params });
  }

  /** 15 dakikalik, refresh'siz erisim token'i dondurur. */
  impersonate(id: number): Observable<ImpersonationResponse> {
    return this.http.post<ImpersonationResponse>(`${this.baseUrl}/tenants/${id}/impersonate`, {});
  }

  // --- Planlar --------------------------------------------------------------

  plans(includeInactive = true): Observable<PlanDto[]> {
    const params = new HttpParams().set('includeInactive', String(includeInactive));
    return this.http.get<PlanDto[]>(`${this.baseUrl}/plans`, { params });
  }

  createPlan(request: PlanUpsertRequest): Observable<PlanDto> {
    return this.http.post<PlanDto>(`${this.baseUrl}/plans`, request);
  }

  updatePlan(id: number, request: PlanUpsertRequest): Observable<PlanDto> {
    return this.http.put<PlanDto>(`${this.baseUrl}/plans/${id}`, request);
  }

  deletePlan(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/plans/${id}`);
  }

  // --- Duyurular ------------------------------------------------------------

  announcements(): Observable<AnnouncementDto[]> {
    return this.http.get<AnnouncementDto[]>(`${this.baseUrl}/announcements`);
  }

  createAnnouncement(request: AnnouncementUpsertRequest): Observable<AnnouncementDto> {
    return this.http.post<AnnouncementDto>(`${this.baseUrl}/announcements`, request);
  }

  updateAnnouncement(id: number, request: AnnouncementUpsertRequest): Observable<AnnouncementDto> {
    return this.http.put<AnnouncementDto>(`${this.baseUrl}/announcements/${id}`, request);
  }

  deleteAnnouncement(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/announcements/${id}`);
  }

  // --- Entegrasyon sagligi --------------------------------------------------

  integrationHealth(tenantId?: number | null): Observable<TenantIntegrationHealthDto[]> {
    let params = new HttpParams();
    if (tenantId != null) {
      params = params.set('tenantId', String(tenantId));
    }
    return this.http.get<TenantIntegrationHealthDto[]>(`${this.baseUrl}/integration-health`, {
      params,
    });
  }
}
