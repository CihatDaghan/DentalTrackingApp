/**
 * Elle tiplenmis klinik API sozlesmesi (anamnez, not, medya, dijital onam).
 * Enum degerleri arka uctaki `Dental.Domain.Enums.ClinicalEnums` ile birebir ayni tutulmalidir.
 */

// ---------------------------------------------------------------------------
// Enumlar (arka uc: byte)
// ---------------------------------------------------------------------------

export const AnamnesisAnswerType = {
  YesNo: 1,
  /** Evet/Hayir + Evet'te aciklama metni (orn. alerji detayi). */
  YesNoDetail: 2,
  Text: 3,
  /** Secenekler soru uzerindeki optionsJson'dan gelir. */
  MultiSelect: 4,
} as const;
export type AnamnesisAnswerType = (typeof AnamnesisAnswerType)[keyof typeof AnamnesisAnswerType];

export const MediaCategory = {
  Xray: 1,
  IntraoralPhoto: 2,
  Document: 3,
  ConsentPdf: 4,
  LabAttachment: 5,
  InvoiceUbl: 6,
  InvoicePdf: 7,
  Logo: 8,
  SignatureImage: 9,
  PrescriptionPdf: 10,
  EpicrisisPdf: 11,
  /** Hasta karti "Rapor" sekmesinden uretilen belge (tedavi dokumu, durum raporu, proforma). */
  PatientReportPdf: 12,
} as const;
export type MediaCategory = (typeof MediaCategory)[keyof typeof MediaCategory];

export const ConsentFormStatus = {
  Draft: 1,
  SentBySms: 2,
  Signed: 3,
  Expired: 4,
  Declined: 5,
} as const;
export type ConsentFormStatus = (typeof ConsentFormStatus)[keyof typeof ConsentFormStatus];

export const ConsentSignChannel = {
  Tablet: 1,
  SmsLink: 2,
} as const;
export type ConsentSignChannel = (typeof ConsentSignChannel)[keyof typeof ConsentSignChannel];

// ---------------------------------------------------------------------------
// Anamnez
// ---------------------------------------------------------------------------

export interface AnamnesisQuestionDto {
  id: number;
  sortOrder: number;
  questionText: string;
  questionTextEn: string | null;
  answerType: AnamnesisAnswerType;
  /** MultiSelect secenekleri: JSON dizi dizesi, orn. '["A","B"]'. */
  optionsJson: string | null;
  isCritical: boolean;
}

export interface AnamnesisTemplateListItemDto {
  id: number;
  name: string;
  isDefault: boolean;
  questionCount: number;
}

export interface AnamnesisTemplateDto {
  id: number;
  name: string;
  isDefault: boolean;
  questions: AnamnesisQuestionDto[];
}

export interface AnamnesisAnswerDto {
  questionId: number;
  questionText: string;
  answerType: AnamnesisAnswerType;
  isCritical: boolean;
  boolValue: boolean | null;
  textValue: string | null;
}

export interface AnamnesisAnswerInput {
  questionId: number;
  boolValue?: boolean | null;
  textValue?: string | null;
}

export interface AnamnesisFillRequest {
  templateId: number;
  answers: AnamnesisAnswerInput[];
}

export interface AnamnesisResponseDto {
  id: number;
  templateId: number;
  templateName: string;
  filledByUserId: number;
  filledByName: string;
  filledAtUtc: string;
  answers: AnamnesisAnswerDto[];
}

/** Hasta basliginda kirmizi rozetle gosterilen kritik anamnez cevabi. */
export interface CriticalFlagDto {
  questionId: number;
  questionText: string;
  boolValue: boolean | null;
  textValue: string | null;
}

// ---------------------------------------------------------------------------
// Not
// ---------------------------------------------------------------------------

export interface PatientNoteDto {
  id: number;
  patientId: number;
  authorUserId: number;
  authorName: string;
  noteText: string;
  isPinned: boolean;
  colorHex: string | null;
  createdAtUtc: string;
  updatedAtUtc: string | null;
}

export interface PatientNoteUpsertRequest {
  noteText: string;
  isPinned: boolean;
  colorHex?: string | null;
}

// ---------------------------------------------------------------------------
// Medya (goruntu arsivi)
// ---------------------------------------------------------------------------

export interface MediaFileDto {
  id: number;
  patientId: number | null;
  category: MediaCategory;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  sha256: string;
  hasThumbnail: boolean;
  takenAt: string | null; // "yyyy-MM-dd"
  description: string | null;
  toothNumber: string | null;
  uploadedByUserId: number | null;
  uploadedByName: string | null;
  createdAtUtc: string;
}

export interface MediaUploadMeta {
  category: MediaCategory;
  description?: string | null;
  toothNumber?: string | null;
  takenAt?: string | null; // "yyyy-MM-dd"
}

// ---------------------------------------------------------------------------
// Dijital onam
// ---------------------------------------------------------------------------

export interface ConsentTemplateListItemDto {
  id: number;
  name: string;
  locale: string;
  version: number;
  isActive: boolean;
}

export interface ConsentTemplateDto {
  id: number;
  name: string;
  bodyHtml: string;
  locale: string;
  version: number;
  isActive: boolean;
}

export interface ConsentTemplateUpsertRequest {
  name: string;
  bodyHtml: string;
  locale: string;
  isActive: boolean;
}

export interface ConsentFormDto {
  id: number;
  patientId: number;
  patientName: string;
  templateId: number;
  templateName: string;
  templateVersion: number;
  treatmentRecordId: number | null;
  renderedHtml: string;
  status: ConsentFormStatus;
  signChannel: ConsentSignChannel | null;
  signTokenExpiresAtUtc: string | null;
  signedAtUtc: string | null;
  signatureFileId: number | null;
  pdfFileId: number | null;
  pdfSha256: string | null;
  createdAtUtc: string;
}

export interface ConsentCreateRequest {
  templateId: number;
  treatmentRecordId?: number | null;
}

export interface ConsentSignRequest {
  signaturePngBase64: string;
}

export interface ConsentSendSmsResult {
  consentId: number;
  publicUrl: string;
  expiresAtUtc: string;
  sentToPhone: string;
}

// Public (auth'suz) imza sayfasi ----------------------------------------------

export interface PublicConsentViewDto {
  clinicName: string;
  patientMaskedName: string;
  renderedHtml: string;
  status: ConsentFormStatus;
  locale: string;
}

export interface PublicConsentSignRequest {
  signaturePngBase64?: string | null;
  declined: boolean;
}
