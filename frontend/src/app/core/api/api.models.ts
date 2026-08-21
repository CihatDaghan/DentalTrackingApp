/**
 * Elle tiplenmis API sozlesmesi (hasta + randevu + recall + hekim).
 * NOT: Sonraki asamada NSwag ile `api-client.generated.ts` uretilecek ve bu dosya kaldirilacak.
 * Enum degerleri arka uctaki `Dental.Domain.Enums` ile birebir ayni tutulmalidir.
 */

// ---------------------------------------------------------------------------
// Ortak
// ---------------------------------------------------------------------------

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

/** AppTable ile API arasindaki lazy-load sozlesmesi. */
export interface TableQuery {
  page: number;
  pageSize: number;
  /** Or. "name" | "-createdat" (eksi on eki = azalan). */
  sort?: string;
  search?: string;
}

// ---------------------------------------------------------------------------
// Enumlar (arka uc: byte)
// ---------------------------------------------------------------------------

export const AppointmentType = {
  Normal: 1,
  Control: 2,
  EmptySlot: 3,
  Blocked: 4,
} as const;
export type AppointmentType = (typeof AppointmentType)[keyof typeof AppointmentType];

export const AppointmentStatus = {
  Scheduled: 1,
  Confirmed: 2,
  Arrived: 3,
  InChair: 4,
  Completed: 5,
  Cancelled: 6,
  NoShow: 7,
} as const;
export type AppointmentStatus = (typeof AppointmentStatus)[keyof typeof AppointmentStatus];

export const ReminderState = {
  None: 0,
  Queued: 1,
  Sent: 2,
} as const;
export type ReminderState = (typeof ReminderState)[keyof typeof ReminderState];

export const RecallStatus = {
  Planned: 1,
  ConvertedToAppointment: 2,
  Arrived: 3,
  Abandoned: 4,
} as const;
export type RecallStatus = (typeof RecallStatus)[keyof typeof RecallStatus];

export const IdentityType = {
  Tckn: 1,
  Passport: 2,
} as const;
export type IdentityType = (typeof IdentityType)[keyof typeof IdentityType];

export const Gender = {
  Unknown: 0,
  Male: 1,
  Female: 2,
} as const;
export type Gender = (typeof Gender)[keyof typeof Gender];

export const ConsentType = {
  KvkkProcessing: 1,
  SmsInfo: 2,
  SmsMarketing: 3,
  WhatsApp: 4,
  Email: 5,
} as const;
export type ConsentType = (typeof ConsentType)[keyof typeof ConsentType];

export const ConsentSource = {
  Form: 1,
  Verbal: 2,
  SmsLink: 3,
} as const;
export type ConsentSource = (typeof ConsentSource)[keyof typeof ConsentSource];

// ---------------------------------------------------------------------------
// Hasta
// ---------------------------------------------------------------------------

export interface ConsentDto {
  consentType: ConsentType;
  isGranted: boolean;
  grantedAtUtc: string | null;
  source: ConsentSource;
}

export interface ConsentUpsertDto {
  consentType: ConsentType;
  isGranted: boolean;
  source?: ConsentSource;
}

export interface PatientListItemDto {
  id: number;
  fileNo: string;
  firstName: string;
  lastName: string;
  phone: string | null;
  city: string | null;
  birthDate: string | null; // "yyyy-MM-dd"
  balance: number;
  createdAtUtc: string;
}

export interface PatientDto {
  id: number;
  clinicId: number;
  fileNo: string;
  firstName: string;
  lastName: string;
  identityType: IdentityType;
  tckn: string | null;
  passportNo: string | null;
  nationalityCode: string;
  lastEntryDate: string | null; // "yyyy-MM-dd"
  birthDate: string | null; // "yyyy-MM-dd"
  gender: Gender;
  phone: string | null;
  phone2: string | null;
  email: string | null;
  address: string | null;
  city: string | null;
  district: string | null;
  referralSource: string | null;
  profession: string | null;
  generalNote: string | null;
  balance: number;
  createdAtUtc: string;
  consents: ConsentDto[];
}

export interface PatientUpsertRequest {
  firstName: string;
  lastName: string;
  identityType?: IdentityType;
  /** Duz TCKN — null = degistirme, "" = temizle. */
  tckn?: string | null;
  passportNo?: string | null;
  nationalityCode?: string;
  lastEntryDate?: string | null;
  birthDate?: string | null;
  gender?: Gender;
  phone?: string | null;
  phone2?: string | null;
  email?: string | null;
  address?: string | null;
  city?: string | null;
  district?: string | null;
  referralSource?: string | null;
  profession?: string | null;
  generalNote?: string | null;
  fileNo?: string | null;
  clinicId?: number | null;
  consents?: ConsentUpsertDto[] | null;
}

export interface PatientSummaryDto {
  id: number;
  fullName: string;
  age: number | null;
  balance: number;
  phone: string | null;
}

// ---------------------------------------------------------------------------
// Hekim + calisma saatleri
// ---------------------------------------------------------------------------

export interface DoctorDto {
  id: number;
  firstName: string;
  lastName: string;
  color: string | null;
  branch: string | null;
}

/** .NET DayOfWeek: 0=Pazar ... 6=Cumartesi. */
export interface DoctorWorkingHourDto {
  id: number;
  doctorUserId: number;
  clinicId: number;
  dayOfWeek: number;
  startTime: string; // "HH:mm:ss"
  endTime: string;
}

// ---------------------------------------------------------------------------
// Randevu
// ---------------------------------------------------------------------------

export interface AppointmentDto {
  id: number;
  clinicId: number;
  patientId: number | null;
  patientName: string | null;
  doctorUserId: number;
  doctorName: string;
  startUtc: string; // ISO, UTC (sonek 'Z' olmayabilir)
  endUtc: string;
  type: AppointmentType;
  status: AppointmentStatus;
  title: string | null;
  note: string | null;
  color: string | null;
  sourceTreatmentRecordId: number | null;
  reminderState: ReminderState;
  cancelReason: string | null;
  rowVersion: string;
}

export interface AppointmentUpsertRequest {
  clinicId: number;
  doctorUserId: number;
  startUtc: string;
  endUtc: string;
  type?: AppointmentType;
  patientId?: number | null;
  title?: string | null;
  note?: string | null;
  color?: string | null;
  sourceTreatmentRecordId?: number | null;
  rowVersion?: string | null;
}

export interface AppointmentStatusRequest {
  status: AppointmentStatus;
  cancelReason?: string | null;
}

// ---------------------------------------------------------------------------
// Kontrol (recall)
// ---------------------------------------------------------------------------

export interface RecallDto {
  id: number;
  patientId: number;
  patientName: string;
  doctorUserId: number | null;
  suggestedDate: string; // "yyyy-MM-dd"
  reason: string | null;
  status: RecallStatus;
  appointmentId: number | null;
  lastReminderAtUtc: string | null;
  createdAtUtc: string;
}

export interface RecallCreateRequest {
  patientId: number;
  suggestedDate: string;
  reason?: string | null;
  doctorUserId?: number | null;
  sourceTreatmentRecordId?: number | null;
}

export interface RecallStatusRequest {
  status: RecallStatus;
}

export interface RecallConvertRequest {
  clinicId: number;
  doctorUserId: number;
  startUtc: string;
  endUtc: string;
  note?: string | null;
}

// ---------------------------------------------------------------------------
// Yardimcilar
// ---------------------------------------------------------------------------

/** API'nin 'Z' soneksiz dondurdugu UTC tarihleri guvenle Date'e cevirir. */
export function parseUtc(value: string): Date {
  if (/Z$|[+-]\d{2}:\d{2}$/.test(value)) {
    return new Date(value);
  }
  return new Date(value + 'Z');
}

/** Date -> API'ye gonderilecek ISO UTC dizesi. */
export function toUtcIso(date: Date): string {
  return date.toISOString();
}

/** Date -> "yyyy-MM-dd" (yerel takvim gunu; DateOnly alanlari icin). */
export function toDateOnly(date: Date): string {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

/** "yyyy-MM-dd" -> yerel Date (00:00). */
export function fromDateOnly(value: string | null): Date | null {
  if (!value) {
    return null;
  }
  const [y, m, d] = value.split('-').map(Number);
  return new Date(y, m - 1, d);
}
