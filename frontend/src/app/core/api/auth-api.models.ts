/**
 * Elle tiplenmis auth API sozlesmesi.
 * NOT: Sonraki asamada NSwag ile `api-client.generated.ts` uretilecek ve bu dosya kaldirilacak.
 */

/** Backend Dental.Domain.Enums.UserType — JSON'a sayi olarak serilesir. */
export enum UserType {
  Owner = 1,
  Manager = 2,
  Dentist = 3,
  Assistant = 4,
  Secretary = 5,
  Accountant = 6,
}

export interface AuthUserDto {
  id: number;
  email: string;
  firstName: string;
  lastName: string;
  /** Sayisal enum; string ile karsilastirmayin (inceleme bulgusu: '3' === 3 daima false). */
  userType: UserType;
  locale: string;
  tenantId: number | null;
  isSuperAdmin: boolean;
  permissions: string[];
}

export interface ClinicSummaryDto {
  id: number;
  name: string;
  isDefault: boolean;
}

export interface LoginRequestDto {
  email: string;
  password: string;
  turnstileToken?: string | null;
  rememberMe: boolean;
}

export interface LoginResponseDto {
  accessToken: string;
  refreshToken: string;
  requiresClinicSelection: boolean;
  clinics: ClinicSummaryDto[];
  user: AuthUserDto;
}

export interface SelectClinicRequestDto {
  clinicId: number;
}

export interface RefreshRequestDto {
  refreshToken: string;
}

export interface TokenPairDto {
  accessToken: string;
  refreshToken: string;
}

export interface LogoutRequestDto {
  refreshToken: string;
}
