/**
 * e-Belge (e-Fatura / e-Arsiv / e-SMM) API sozlesmesi.
 * Enum degerleri arka uctaki `Dental.Domain.Enums.InvoiceEnums` ile birebir ayni tutulmalidir.
 */

// ---------------------------------------------------------------------------
// Enumlar (arka uc: byte)
// ---------------------------------------------------------------------------

/** Belge tipi — karar motoru belirler, kullanici elle secemez. */
export const InvoiceDocumentKind = {
  EFatura: 1,
  EArsiv: 2,
  ESmm: 3,
} as const;
export type InvoiceDocumentKind =
  (typeof InvoiceDocumentKind)[keyof typeof InvoiceDocumentKind];

export const InvoiceStatus = {
  Draft: 1,
  UblGenerated: 2,
  Queued: 3,
  SentToIntegrator: 4,
  GibProcessing: 5,
  Succeeded: 6,
  GibRejected: 7,
  BuyerRejected: 8,
  Error: 9,
  ManualReview: 10,
  Cancelled: 11,
} as const;
export type InvoiceStatus = (typeof InvoiceStatus)[keyof typeof InvoiceStatus];

export const InvoiceCustomerType = {
  Patient: 1,
  Company: 2,
} as const;
export type InvoiceCustomerType =
  (typeof InvoiceCustomerType)[keyof typeof InvoiceCustomerType];

export const IntegratorProvider = {
  Fake: 1,
  Uyumsoft: 2,
  Nes: 3,
  TurkcellEsirket: 4,
  Izibiz: 5,
} as const;
export type IntegratorProvider =
  (typeof IntegratorProvider)[keyof typeof IntegratorProvider];

/** i18n anahtar eslemeleri (`invoices.status.*`, `invoices.kind.*`, `invoices.integrator.*`). */
export const INVOICE_STATUS_KEYS: Record<number, string> = {
  [InvoiceStatus.Draft]: 'draft',
  [InvoiceStatus.UblGenerated]: 'ublGenerated',
  [InvoiceStatus.Queued]: 'queued',
  [InvoiceStatus.SentToIntegrator]: 'sentToIntegrator',
  [InvoiceStatus.GibProcessing]: 'gibProcessing',
  [InvoiceStatus.Succeeded]: 'succeeded',
  [InvoiceStatus.GibRejected]: 'gibRejected',
  [InvoiceStatus.BuyerRejected]: 'buyerRejected',
  [InvoiceStatus.Error]: 'error',
  [InvoiceStatus.ManualReview]: 'manualReview',
  [InvoiceStatus.Cancelled]: 'cancelled',
};

export const INVOICE_KIND_KEYS: Record<number, string> = {
  [InvoiceDocumentKind.EFatura]: 'eFatura',
  [InvoiceDocumentKind.EArsiv]: 'eArsiv',
  [InvoiceDocumentKind.ESmm]: 'eSmm',
};

export const INTEGRATOR_KEYS: Record<number, string> = {
  [IntegratorProvider.Fake]: 'fake',
  [IntegratorProvider.Uyumsoft]: 'uyumsoft',
  [IntegratorProvider.Nes]: 'nes',
  [IntegratorProvider.TurkcellEsirket]: 'turkcellEsirket',
  [IntegratorProvider.Izibiz]: 'izibiz',
};

/** Filtre acilir listesinde gosterilecek durum sirasi. */
export const INVOICE_ALL_STATUSES: InvoiceStatus[] = [
  InvoiceStatus.Draft,
  InvoiceStatus.UblGenerated,
  InvoiceStatus.Queued,
  InvoiceStatus.SentToIntegrator,
  InvoiceStatus.GibProcessing,
  InvoiceStatus.Succeeded,
  InvoiceStatus.GibRejected,
  InvoiceStatus.BuyerRejected,
  InvoiceStatus.Error,
  InvoiceStatus.ManualReview,
  InvoiceStatus.Cancelled,
];

/** Yeniden gonderime uygun (hata) durumlari. */
export const INVOICE_RETRY_STATUSES: readonly InvoiceStatus[] = [
  InvoiceStatus.Error,
  InvoiceStatus.ManualReview,
  InvoiceStatus.GibRejected,
];

/** "Yolda" durumlar — rozet sari + nabiz animasyonu. */
export const INVOICE_IN_FLIGHT_STATUSES: readonly InvoiceStatus[] = [
  InvoiceStatus.Queued,
  InvoiceStatus.SentToIntegrator,
  InvoiceStatus.GibProcessing,
];

// ---------------------------------------------------------------------------
// DTO'lar
// ---------------------------------------------------------------------------

export interface InvoiceTotalsDto {
  subTotal: number;
  discountTotal: number;
  vatTotal: number;
  withholdingTotal: number;
  gvStopajTotal: number;
  payableAmount: number;
}

export interface InvoiceLineDto {
  id: number;
  seqNo: number;
  treatmentRecordId: number | null;
  itemName: string;
  quantity: number;
  unitCode: string;
  unitPrice: number;
  discountAmount: number;
  vatRate: number;
  vatAmount: number;
  lineTotal: number;
  isAesthetic: boolean;
}

export interface InvoiceStatusLogDto {
  id: number;
  fromStatus: InvoiceStatus | null;
  toStatus: InvoiceStatus;
  atUtc: string;
  actorUserId: number | null;
  detail: string | null;
}

export interface InvoiceListItemDto {
  id: number;
  invoiceNumber: string | null;
  documentKind: InvoiceDocumentKind;
  typeCode: string;
  buyerName: string;
  payableAmount: number;
  currencyCode: string;
  status: InvoiceStatus;
  errorMessage: string | null;
  issueDate: string; // "yyyy-MM-dd"
  ettn: string | null;
}

export interface InvoiceDto {
  id: number;
  clinicId: number;
  documentKind: InvoiceDocumentKind;
  profileId: string | null;
  typeCode: string;
  status: InvoiceStatus;
  invoiceNumber: string | null;
  serial: string | null;
  ettn: string | null;
  issueDate: string;
  issueTime: string | null;
  customerType: InvoiceCustomerType;
  patientId: number | null;
  companyId: number | null;
  buyerName: string;
  buyerTcknVkn: string | null;
  buyerPassportNo: string | null;
  buyerNationality: string | null;
  buyerLastEntryDate: string | null;
  buyerAddress: string | null;
  buyerEmail: string | null;
  currencyCode: string;
  exchangeRate: number;
  totals: InvoiceTotalsDto;
  exemptionCode: string | null;
  exemptionReason: string | null;
  withholdingCode: string | null;
  integratorProvider: IntegratorProvider | null;
  integratorRefId: string | null;
  lastStatusCheckUtc: string | null;
  attemptCount: number;
  nextAttemptAtUtc: string | null;
  errorMessage: string | null;
  ublFileId: number | null;
  pdfFileId: number | null;
  sourceInvoiceId: number | null;
  lines: InvoiceLineDto[];
  statusLogs: InvoiceStatusLogDto[];
  createdAtUtc: string;
}

/** Onizleme + taslak olusturma icin ortak govde. */
export interface InvoiceDraftRequest {
  patientId?: number | null;
  companyId?: number | null;
  treatmentRecordIds: number[];
  isForeignPatient?: boolean;
  isRefund?: boolean;
  sourceInvoiceId?: number | null;
  isGovernmentBuyer?: boolean;
}

/** Karar motorunun cikti kartina beslenen onizleme. */
export interface InvoicePreviewDto {
  documentKind: InvoiceDocumentKind;
  profileId: string | null;
  typeCode: string;
  /** Kararin insan okunur gerekcesi — bilgi kartinda gosterilir. */
  rationale: string;
  exemptionCode: string | null;
  exemptionReason: string | null;
  withholdingCode: string | null;
  withholdingPercent: number | null;
  customerType: InvoiceCustomerType;
  patientId: number | null;
  companyId: number | null;
  buyerName: string;
  buyerTcknVkn: string | null;
  buyerPassportNo: string | null;
  buyerNationality: string | null;
  buyerLastEntryDate: string | null;
  currencyCode: string;
  lines: InvoiceLineDto[];
  totals: InvoiceTotalsDto;
  /** Sari kutu — belge kesilebilir ama eksik var. */
  warnings: string[];
  /** Kirmizi kutu — giderilmeden ilerlenemez. */
  errors: string[];
  canCreate: boolean;
}

export interface GibTaxpayerDto {
  vkn: string;
  title: string | null;
  alias: string | null;
  accountType: string | null;
  isEInvoiceUser: boolean;
  lastSyncUtc: string | null;
}

export interface InvoiceCancelRequest {
  reason: string;
}

export interface InvoiceListQuery {
  status?: InvoiceStatus | null;
  from?: string | null;
  to?: string | null;
  page?: number;
  pageSize?: number;
}
