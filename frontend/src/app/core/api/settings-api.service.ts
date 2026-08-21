import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  ClinicSettingsDto,
  ClinicSettingsUpdateRequest,
  ClinicWorkingHourDto,
  ClinicWorkingHoursSaveRequest,
  EnabizSettingsDto,
  EnabizSettingsRequest,
  IntegrationSettingDto,
  IntegrationSettingUpdateRequest,
  IntegrationTestResultDto,
  PermissionCatalogDto,
  RolePermissionsDto,
  StaffDto,
  StaffInviteRequest,
  StaffInviteResultDto,
  StaffUpdateRequest,
  TemporaryPasswordDto,
} from './settings-api.models';

/** Klinik ayarlari: kunye, calisma saatleri, personel, yetki matrisi, entegrasyonlar. */
@Injectable({ providedIn: 'root' })
export class SettingsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/v1`;

  // --- Klinik kunyesi -------------------------------------------------------

  clinic(): Observable<ClinicSettingsDto> {
    return this.http.get<ClinicSettingsDto>(`${this.baseUrl}/settings/clinic`);
  }

  updateClinic(request: ClinicSettingsUpdateRequest): Observable<ClinicSettingsDto> {
    return this.http.put<ClinicSettingsDto>(`${this.baseUrl}/settings/clinic`, request);
  }

  // --- Calisma saatleri -----------------------------------------------------

  workingHours(clinicId?: number | null): Observable<ClinicWorkingHourDto[]> {
    let params = new HttpParams();
    if (clinicId != null) {
      params = params.set('clinicId', String(clinicId));
    }
    return this.http.get<ClinicWorkingHourDto[]>(`${this.baseUrl}/settings/working-hours`, {
      params,
    });
  }

  saveWorkingHours(request: ClinicWorkingHoursSaveRequest): Observable<ClinicWorkingHourDto[]> {
    return this.http.put<ClinicWorkingHourDto[]>(`${this.baseUrl}/settings/working-hours`, request);
  }

  // --- Personel -------------------------------------------------------------

  staff(includeInactive = true): Observable<StaffDto[]> {
    const params = new HttpParams().set('includeInactive', String(includeInactive));
    return this.http.get<StaffDto[]>(`${this.baseUrl}/settings/staff`, { params });
  }

  inviteStaff(request: StaffInviteRequest): Observable<StaffInviteResultDto> {
    return this.http.post<StaffInviteResultDto>(`${this.baseUrl}/settings/staff`, request);
  }

  updateStaff(id: number, request: StaffUpdateRequest): Observable<StaffDto> {
    return this.http.put<StaffDto>(`${this.baseUrl}/settings/staff/${id}`, request);
  }

  deactivateStaff(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/settings/staff/${id}`);
  }

  resetStaffPassword(id: number): Observable<TemporaryPasswordDto> {
    return this.http.post<TemporaryPasswordDto>(
      `${this.baseUrl}/settings/staff/${id}/reset-password`,
      {},
    );
  }

  // --- Yetki matrisi --------------------------------------------------------

  roles(): Observable<RolePermissionsDto[]> {
    return this.http.get<RolePermissionsDto[]>(`${this.baseUrl}/settings/roles`);
  }

  permissionCatalog(): Observable<PermissionCatalogDto> {
    return this.http.get<PermissionCatalogDto>(`${this.baseUrl}/settings/permissions`);
  }

  updateRolePermissions(id: number, permissions: string[]): Observable<RolePermissionsDto> {
    return this.http.put<RolePermissionsDto>(`${this.baseUrl}/settings/roles/${id}/permissions`, {
      permissions,
    });
  }

  // --- Entegrasyonlar -------------------------------------------------------

  integrations(): Observable<IntegrationSettingDto[]> {
    return this.http.get<IntegrationSettingDto[]>(`${this.baseUrl}/settings/integrations`);
  }

  updateIntegration(
    key: string,
    request: IntegrationSettingUpdateRequest,
  ): Observable<IntegrationSettingDto> {
    return this.http.put<IntegrationSettingDto>(
      `${this.baseUrl}/settings/integrations/${key}`,
      request,
    );
  }

  testIntegration(key: string): Observable<IntegrationTestResultDto> {
    return this.http.post<IntegrationTestResultDto>(
      `${this.baseUrl}/settings/integrations/${key}/test`,
      {},
    );
  }

  // --- e-Nabiz modu ---------------------------------------------------------

  enabizSettings(): Observable<EnabizSettingsDto> {
    return this.http.get<EnabizSettingsDto>(`${this.baseUrl}/enabiz/settings`);
  }

  saveEnabizSettings(request: EnabizSettingsRequest): Observable<EnabizSettingsDto> {
    return this.http.put<EnabizSettingsDto>(`${this.baseUrl}/enabiz/settings`, request);
  }
}
