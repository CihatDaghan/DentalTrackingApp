/**
 * Klinik ayarlari sozlesmeleri (arka uc: Dental.Api.Controllers.SettingsController).
 * Enum degerleri `Dental.Domain.Enums` ile birebir ayni tutulmalidir.
 */

import { UserType } from './auth-api.models';

/** Tenant hukuki tipi — fatura belge tipini belirler (SoleProprietor -> e-SMM, Company -> e-Fatura/e-Arsiv). */
export const TenantLegalType = {
  SoleProprietor: 1,
  Company: 2,
} as const;
export type TenantLegalType = (typeof TenantLegalType)[keyof typeof TenantLegalType];

export const TenantStatus = {
  Trial: 1,
  Active: 2,
  Suspended: 3,
} as const;
export type TenantStatus = (typeof TenantStatus)[keyof typeof TenantStatus];

/** e-Nabiz calisma modu — Live yalnizca KtsRegistered acikken secilebilir. */
export const EnabizMode = {
  Disabled: 0,
  Held: 1,
  TestOnly: 2,
  Live: 3,
} as const;
export type EnabizMode = (typeof EnabizMode)[keyof typeof EnabizMode];

export interface ClinicSettingsDto {
  tenantId: number;
  tenantName: string;
  legalType: TenantLegalType;
  taxNumber: string | null;
  taxOffice: string | null;
  hasHealthTourismAuthorization: boolean;
  defaultLocale: string;
  status: TenantStatus;
  planCode: string | null;
  trialEndsAtUtc: string | null;
  clinicId: number;
  clinicName: string;
  address: string | null;
  city: string | null;
  district: string | null;
  phone: string | null;
  email: string | null;
  ckysCode: string | null;
  logoFileId: number | null;
}

export interface ClinicSettingsUpdateRequest {
  tenantName: string;
  legalType: TenantLegalType;
  clinicName: string;
  taxNumber?: string | null;
  taxOffice?: string | null;
  hasHealthTourismAuthorization: boolean;
  address?: string | null;
  city?: string | null;
  district?: string | null;
  phone?: string | null;
  email?: string | null;
  ckysCode?: string | null;
  logoFileId?: number | null;
  clinicId?: number | null;
}

/** 0 = Pazar ... 6 = Cumartesi (System.DayOfWeek). */
export interface ClinicWorkingHourDto {
  id: number;
  clinicId: number;
  dayOfWeek: number;
  openTime: string | null;
  closeTime: string | null;
  isClosed: boolean;
}

export interface ClinicWorkingHourItem {
  dayOfWeek: number;
  openTime: string | null;
  closeTime: string | null;
  isClosed: boolean;
}

export interface ClinicWorkingHoursSaveRequest {
  clinicId: number;
  items: ClinicWorkingHourItem[];
}

export interface StaffRoleDto {
  id: number;
  name: string;
  isSystem: boolean;
}

export interface StaffDto {
  id: number;
  email: string;
  firstName: string;
  lastName: string;
  fullName: string;
  userType: UserType;
  isActive: boolean;
  mustChangePassword: boolean;
  color: string | null;
  branch: string | null;
  diplomaNo: string | null;
  roles: StaffRoleDto[];
  clinicIds: number[];
  lastLoginUtc: string | null;
  createdAtUtc: string;
}

export interface StaffInviteRequest {
  email: string;
  firstName: string;
  lastName: string;
  userType: UserType;
  roleIds: number[];
  clinicId?: number | null;
  color?: string | null;
  branch?: string | null;
  diplomaNo?: string | null;
}

export interface StaffUpdateRequest {
  firstName: string;
  lastName: string;
  userType: UserType;
  roleIds: number[];
  isActive: boolean;
  color?: string | null;
  branch?: string | null;
  diplomaNo?: string | null;
}

export interface StaffInviteResultDto {
  user: StaffDto;
  temporaryPassword: string;
}

export interface TemporaryPasswordDto {
  temporaryPassword: string;
}

export interface RolePermissionsDto {
  id: number;
  name: string;
  isSystem: boolean;
  permissions: string[];
  userCount: number;
}

/** Izin katalogu: modul -> izin kodlari. */
export interface PermissionCatalogDto {
  byModule: Record<string, string[]>;
}

export interface IntegrationSettingDto {
  /** "EInvoice" | "Sms" | "WhatsApp" | "Payment" | "Enabiz". */
  integrationKey: string;
  providerKey: string;
  /** "Test" | "Live". */
  environment: string;
  isEnabled: boolean;
  /** Sir alanlari maskeli gelir (`••••1234`); bos gonderilirse arka uc mevcut degeri korur. */
  settings: Record<string, string | null>;
  secretFields: string[];
  availableProviders: string[];
  updatedAtUtc: string | null;
  updatedByUserId: number | null;
}

export interface IntegrationSettingUpdateRequest {
  providerKey: string;
  environment: string;
  isEnabled: boolean;
  settings: Record<string, string>;
}

export interface IntegrationTestResultDto {
  success: boolean;
  message: string;
  durationMs: number;
  providerKey: string;
}

export interface EnabizSettingsDto {
  mode: EnabizMode;
  ckysCode: string | null;
  ussUsername: string | null;
  applicationCode: string | null;
  hasPassword: boolean;
  ktsRegistered: boolean;
  canGoLive: boolean;
}

export interface EnabizSettingsRequest {
  mode: EnabizMode;
  ckysCode: string | null;
  ussUsername: string | null;
  ussPassword: string | null;
  applicationCode: string | null;
}
