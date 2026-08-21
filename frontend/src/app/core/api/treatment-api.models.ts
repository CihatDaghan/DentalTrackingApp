/**
 * Tedavi modulu API sozlesmesi (katalog + fiyat listeleri + tedavi kayitlari + dis semasi verisi).
 * NOT: Sonraki asamada NSwag ile `api-client.generated.ts` uretilecek ve bu dosya kaldirilacak.
 * Enum degerleri arka uctaki `Dental.Domain.Enums.TreatmentEnums` ile birebir ayni tutulmalidir.
 */

import { Surface } from '../../shared/components/tooth-chart/tooth-chart.models';

// ---------------------------------------------------------------------------
// Enumlar (arka uc: byte)
// ---------------------------------------------------------------------------

export const ToothScope = {
  PerTooth: 1,
  PerJaw: 2,
  WholeMouth: 3,
} as const;
export type ToothScope = (typeof ToothScope)[keyof typeof ToothScope];

export const ToothCondition = {
  Present: 1,
  Missing: 2,
  Extracted: 3,
  Implant: 4,
  Crown: 5,
  Bridge: 6,
  RootCanalTreated: 7,
  Unerupted: 8,
} as const;
export type ToothCondition = (typeof ToothCondition)[keyof typeof ToothCondition];

export const TreatmentRecordStatus = {
  Diagnosis: 1,
  Planned: 2,
  Done: 3,
  Cancelled: 4,
} as const;
export type TreatmentRecordStatus =
  (typeof TreatmentRecordStatus)[keyof typeof TreatmentRecordStatus];

/** Dis yuzeyleri bit bayragi: M=1, O=2, D=4, B=8, L=16. */
export const ToothSurfaces = {
  None: 0,
  M: 1,
  O: 2,
  D: 4,
  B: 8,
  L: 16,
} as const;
export type ToothSurfacesFlags = number;

export const ToothStatusEffect = {
  None: 0,
  Extracted: 1,
  Implant: 2,
  Crown: 3,
  RootCanal: 4,
  Bridge: 5,
} as const;
export type ToothStatusEffect = (typeof ToothStatusEffect)[keyof typeof ToothStatusEffect];

export const EnabizStatus = {
  NotRequired: 0,
  Pending: 1,
  Sent: 2,
  Accepted: 3,
  Rejected: 4,
} as const;
export type EnabizStatus = (typeof EnabizStatus)[keyof typeof EnabizStatus];

// ---------------------------------------------------------------------------
// Yuzey bit bayragi <-> Surface[] cevirimleri
// ---------------------------------------------------------------------------

/** Gosterim sirasi: MOD-B-L geleneksel yazimi. */
const SURFACE_ORDER: { surface: Surface; bit: number }[] = [
  { surface: 'M', bit: ToothSurfaces.M },
  { surface: 'O', bit: ToothSurfaces.O },
  { surface: 'D', bit: ToothSurfaces.D },
  { surface: 'B', bit: ToothSurfaces.B },
  { surface: 'L', bit: ToothSurfaces.L },
];

/** Bit bayragi -> Surface[] (MOD sirali). */
export function surfacesFromFlags(flags: number | null | undefined): Surface[] {
  if (!flags) {
    return [];
  }
  return SURFACE_ORDER.filter((s) => (flags & s.bit) !== 0).map((s) => s.surface);
}

/** Surface[] -> bit bayragi. */
export function surfacesToFlags(surfaces: readonly Surface[] | null | undefined): number {
  return (surfaces ?? []).reduce<number>(
    (acc, s) => acc | (SURFACE_ORDER.find((x) => x.surface === s)?.bit ?? 0),
    0,
  );
}

/** Bit bayragi -> "MOD" gibi harf gosterimi. */
export function surfacesLabel(flags: number | null | undefined): string {
  return surfacesFromFlags(flags).join('');
}

// ---------------------------------------------------------------------------
// Katalog: kategori + tedavi tanimi
// ---------------------------------------------------------------------------

export interface TreatmentCategoryDto {
  id: number;
  name: string;
  nameEn: string | null;
  colorHex: string | null;
  sortOrder: number;
  treatmentCount: number;
}

export interface TreatmentCategoryUpsertRequest {
  name: string;
  nameEn?: string | null;
  colorHex?: string | null;
  sortOrder?: number;
}

export interface TreatmentDefinitionDto {
  id: number;
  categoryId: number;
  categoryName: string;
  categoryColorHex: string | null;
  code: string;
  name: string;
  nameEn: string | null;
  sutCode: string | null;
  defaultPrice: number;
  vatRate: number;
  toothScope: ToothScope;
  requiresSurface: boolean;
  toothStatusEffect: ToothStatusEffect;
  defaultDurationMinutes: number | null;
  isActive: boolean;
}

export interface TreatmentDefinitionUpsertRequest {
  categoryId: number;
  code: string;
  name: string;
  defaultPrice: number;
  nameEn?: string | null;
  sutCode?: string | null;
  vatRate?: number;
  toothScope?: ToothScope;
  requiresSurface?: boolean;
  toothStatusEffect?: ToothStatusEffect;
  defaultDurationMinutes?: number | null;
  isActive?: boolean;
}

export interface TreatmentCatalogQuery {
  search?: string;
  categoryId?: number | null;
  isActive?: boolean | null;
  page?: number;
  pageSize?: number;
}

// ---------------------------------------------------------------------------
// Fiyat listeleri
// ---------------------------------------------------------------------------

export interface PriceListDto {
  id: number;
  name: string;
  currencyCode: string;
  validFrom: string | null; // "yyyy-MM-dd"
  isDefault: boolean;
  itemCount: number;
}

export interface PriceListUpsertRequest {
  name: string;
  currencyCode?: string;
  validFrom?: string | null;
  isDefault?: boolean;
}

export interface PriceListItemDto {
  treatmentDefinitionId: number;
  treatmentCode: string;
  treatmentName: string;
  price: number;
}

export interface PriceListItemSaveDto {
  treatmentDefinitionId: number;
  price: number;
}

export interface PriceListItemsSaveRequest {
  items: PriceListItemSaveDto[];
}

// ---------------------------------------------------------------------------
// ICD-10
// ---------------------------------------------------------------------------

export interface IcdCodeDto {
  id: number;
  code: string;
  name: string;
  nameEn: string | null;
}

// ---------------------------------------------------------------------------
// Dis semasi verisi
// ---------------------------------------------------------------------------

export interface ToothMarkDto {
  treatmentRecordId: number;
  treatmentDefinitionId: number;
  treatmentName: string;
  status: TreatmentRecordStatus;
  categoryColorHex: string | null;
  surfaces: number;
  rootCanalCount: number | null;
}

export interface ToothDto {
  toothNumber: string;
  condition: ToothCondition;
  marks: ToothMarkDto[];
}

export interface PatientTeethDto {
  patientId: number;
  teeth: ToothDto[];
  mouthWideMarks: ToothMarkDto[];
}

// ---------------------------------------------------------------------------
// Tedavi kayitlari
// ---------------------------------------------------------------------------

export interface TreatmentRecordDto {
  id: number;
  clinicId: number;
  patientId: number;
  doctorUserId: number;
  doctorName: string;
  treatmentDefinitionId: number;
  treatmentName: string;
  treatmentCode: string;
  categoryColorHex: string | null;
  toothNumber: string | null;
  surfaces: number;
  rootCanalCount: number | null;
  status: TreatmentRecordStatus;
  diagnosisIcdCode: string | null;
  planGroupNo: number | null;
  price: number;
  discountAmount: number;
  vatRate: number;
  currencyCode: string;
  performedAtUtc: string | null;
  description: string | null;
  enabizStatus: EnabizStatus;
  visitId: number | null;
  createdAtUtc: string;
}

export interface TreatmentAddItem {
  treatmentDefinitionId: number;
  toothNumber?: string | null;
  surfaces?: number;
  status?: TreatmentRecordStatus;
  doctorUserId?: number | null;
  price?: number | null;
  discountAmount?: number;
  description?: string | null;
  diagnosisIcdCode?: string | null;
  rootCanalCount?: number | null;
  planGroupNo?: number | null;
  visitId?: number | null;
}

export interface TreatmentAddRequest {
  items: TreatmentAddItem[];
  priceListId?: number | null;
  clinicId?: number | null;
}

export interface TreatmentUpdateRequest {
  price?: number | null;
  discountAmount?: number | null;
  description?: string | null;
  surfaces?: number | null;
  rootCanalCount?: number | null;
  diagnosisIcdCode?: string | null;
  planGroupNo?: number | null;
  doctorUserId?: number | null;
}

export interface TreatmentStatusRequest {
  status: TreatmentRecordStatus;
  performedAtUtc?: string | null;
}

export interface TreatmentPriceItem {
  id: number;
  price: number;
  discountAmount?: number;
}

export interface TreatmentBulkPriceRequest {
  items: TreatmentPriceItem[];
}
