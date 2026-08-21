/**
 * Elle tiplenmis recete API sozlesmesi (ilac katalogu, recete, recete sablonu).
 * NOT: Sonraki asamada NSwag ile `api-client.generated.ts` uretilecek ve bu dosya kaldirilacak.
 * Enum degerleri arka uctaki `Dental.Domain.Enums` ile birebir ayni tutulmalidir.
 */

// ---------------------------------------------------------------------------
// Enumlar (arka uc: byte)
// ---------------------------------------------------------------------------

export const PrescriptionStatus = {
  Draft: 1,
  Printed: 2,
  /** Saglik Bakanligi Uygulama ve Sistem Servisi'ne gonderildi. */
  SubmittedToUss: 3,
  Accepted: 4,
  Rejected: 5,
} as const;
export type PrescriptionStatus = (typeof PrescriptionStatus)[keyof typeof PrescriptionStatus];

// ---------------------------------------------------------------------------
// Ilac katalogu
// ---------------------------------------------------------------------------

export interface DrugDto {
  id: number;
  tenantId: number | null;
  barcode: string | null;
  name: string;
  atcCode: string | null;
  form: string | null;
  defaultDose: string | null;
  defaultUsage: string | null;
  /** Kirmizi/yesil recete kapsami — formda kalici uyari bandi tetikler. */
  isControlled: boolean;
}

// ---------------------------------------------------------------------------
// Recete
// ---------------------------------------------------------------------------

export interface PrescriptionItemDto {
  id: number;
  drugId: number;
  drugName: string;
  drugForm: string | null;
  isControlled: boolean;
  boxCount: number;
  dose: string | null;
  frequency: string | null;
  duration: string | null;
  usageNote: string | null;
}

export interface PrescriptionDto {
  id: number;
  patientId: number;
  patientName: string;
  doctorUserId: number;
  doctorName: string;
  visitId: number | null;
  prescriptionNo: string | null;
  status: PrescriptionStatus;
  recetemCode: string | null;
  pdfFileId: number | null;
  hasControlledDrug: boolean;
  /** Arka uctan gelen serbest metin uyari (kontrole tabi ilac varsa dolu). */
  controlledWarning: string | null;
  items: PrescriptionItemDto[];
  createdAtUtc: string;
}

export interface PrescriptionItemRequest {
  drugId: number;
  boxCount: number;
  dose?: string | null;
  frequency?: string | null;
  duration?: string | null;
  usageNote?: string | null;
}

export interface PrescriptionCreateRequest {
  doctorUserId: number;
  visitId?: number | null;
  templateId?: number | null;
  items: PrescriptionItemRequest[];
}

export interface PrescriptionSaveAsTemplateRequest {
  name: string;
}

// ---------------------------------------------------------------------------
// Recete sablonu
// ---------------------------------------------------------------------------

export interface PrescriptionTemplateItemDto {
  id: number;
  drugId: number;
  drugName: string;
  drugForm: string | null;
  isControlled: boolean;
  boxCount: number;
  dose: string | null;
  frequency: string | null;
  duration: string | null;
  usageNote: string | null;
}

export interface PrescriptionTemplateDto {
  id: number;
  name: string;
  items: PrescriptionTemplateItemDto[];
}

export interface PrescriptionTemplateUpsertRequest {
  name: string;
  items: PrescriptionItemRequest[];
}

// ---------------------------------------------------------------------------
// Form yardimcilari
// ---------------------------------------------------------------------------

/** Doz/kullanim hizli secim listeleri — dialogdaki chip butonlari buradan beslenir. */
export const FREQUENCY_PRESETS = ['1x1', '2x1', '3x1', '4x1'] as const;

/** Kullanim sekli secenekleri (i18n anahtar sonekleri). */
export const USAGE_PRESET_KEYS = [
  'afterMeal',
  'beforeMeal',
  'oral',
  'topical',
  'rinse',
  'asNeeded',
] as const;
export type UsagePresetKey = (typeof USAGE_PRESET_KEYS)[number];
