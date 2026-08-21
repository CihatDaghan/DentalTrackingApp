/**
 * Elle tiplenmis finans API sozlesmesi (cari defter, tahsilat, taksit plani, kasa, gider, kurum).
 * Enum degerleri arka uctaki `Dental.Domain.Enums.FinanceEnums` ile birebir ayni tutulmalidir.
 */

// ---------------------------------------------------------------------------
// Enumlar (arka uc: byte)
// ---------------------------------------------------------------------------

export const LedgerAccountType = {
  Patient: 1,
  Company: 2,
} as const;
export type LedgerAccountType = (typeof LedgerAccountType)[keyof typeof LedgerAccountType];

export const LedgerEntryType = {
  /** Done tedaviden dogan borclandirma (Debit). */
  TreatmentCharge: 1,
  /** Tahsilat (Credit). */
  PaymentIn: 2,
  /** Iade (Debit). */
  Refund: 3,
  /** Indirim (Credit). */
  Discount: 4,
  /** Acilis bakiyesi (devir). */
  OpeningBalance: 5,
  /** Kurumun ustlendigi pay aktarimi. */
  CompanyTransfer: 6,
  /** Duzeltme / ters kayit. */
  Correction: 7,
} as const;
export type LedgerEntryType = (typeof LedgerEntryType)[keyof typeof LedgerEntryType];

export const PaymentMethod = {
  Cash: 1,
  CreditCardPos: 2,
  BankTransfer: 3,
  OnlineLink: 4,
  Check: 5,
} as const;
export type PaymentMethod = (typeof PaymentMethod)[keyof typeof PaymentMethod];

export const InstallmentStatus = {
  Pending: 1,
  Partial: 2,
  Paid: 3,
  /** Kalici yazilmaz; vade gecmis Pending/Partial sorgu bazli Overdue sunulur. */
  Overdue: 4,
} as const;
export type InstallmentStatus = (typeof InstallmentStatus)[keyof typeof InstallmentStatus];

// ---------------------------------------------------------------------------
// Cari defter (ekstre)
// ---------------------------------------------------------------------------

export interface LedgerLineDto {
  id: number;
  entryDate: string; // "yyyy-MM-dd"
  entryType: LedgerEntryType;
  description: string | null;
  debit: number;
  credit: number;
  runningBalance: number;
  refType: string | null;
  refId: number | null;
  createdAtUtc: string;
}

export interface LedgerSummaryDto {
  totalTreatment: number;
  totalPayment: number;
  totalDiscount: number;
  totalDebit: number;
  totalCredit: number;
  balance: number;
}

export interface LedgerStatementDto {
  accountType: LedgerAccountType;
  accountId: number;
  accountName: string;
  from: string | null;
  to: string | null;
  openingBalance: number;
  lines: LedgerLineDto[];
  summary: LedgerSummaryDto;
}

// ---------------------------------------------------------------------------
// Tahsilat
// ---------------------------------------------------------------------------

export interface PaymentDto {
  id: number;
  clinicId: number;
  patientId: number | null;
  patientName: string | null;
  companyId: number | null;
  companyName: string | null;
  amount: number;
  currencyCode: string;
  method: PaymentMethod;
  installmentCount: number | null;
  receivedAtUtc: string;
  receivedByUserId: number;
  receivedByName: string;
  ledgerEntryId: number;
  note: string | null;
}

export interface PaymentCreateRequest {
  amount: number;
  method: PaymentMethod;
  patientId?: number | null;
  companyId?: number | null;
  clinicId?: number | null;
  installmentCount?: number | null;
  note?: string | null;
  receivedAtUtc?: string | null;
  currencyCode?: string;
  exchangeRate?: number;
}

export interface DiscountRequest {
  amount: number;
  patientId?: number | null;
  companyId?: number | null;
  description?: string | null;
  clinicId?: number | null;
}

// ---------------------------------------------------------------------------
// Taksit / odeme plani
// ---------------------------------------------------------------------------

export interface InstallmentDto {
  id: number;
  seqNo: number;
  dueDate: string; // "yyyy-MM-dd"
  amount: number;
  paidAmount: number;
  status: InstallmentStatus;
}

export interface PaymentPlanDto {
  id: number;
  patientId: number;
  patientName: string;
  totalAmount: number;
  paidAmount: number;
  startDate: string; // "yyyy-MM-dd"
  note: string | null;
  installments: InstallmentDto[];
}

export interface PaymentPlanCreateRequest {
  patientId: number;
  totalAmount: number;
  installmentCount: number;
  startDate: string;
  note?: string | null;
}

// ---------------------------------------------------------------------------
// Kasa (gunluk ozet)
// ---------------------------------------------------------------------------

export interface CashMethodTotalDto {
  method: PaymentMethod;
  total: number;
  count: number;
}

/** Backend CashMovementKind — sayisal enum; goruntulenen etiket istemcide uretilir. */
export enum CashMovementKind {
  Payment = 1,
  Expense = 2,
}

export interface CashMovementDto {
  atUtc: string;
  kind: CashMovementKind;
  accountName: string | null;
  method: PaymentMethod;
  amount: number;
  userName: string | null;
  description: string | null;
}

export interface CashRegisterDailySummaryDto {
  date: string; // "yyyy-MM-dd"
  clinicId: number | null;
  collectionsByMethod: CashMethodTotalDto[];
  totalCollections: number;
  totalExpenses: number;
  net: number;
  movements: CashMovementDto[];
}

// ---------------------------------------------------------------------------
// Gider
// ---------------------------------------------------------------------------

export interface ExpenseCategoryDto {
  id: number;
  name: string;
}

export interface ExpenseDto {
  id: number;
  clinicId: number;
  categoryId: number;
  categoryName: string;
  amount: number;
  expenseDate: string; // "yyyy-MM-dd"
  method: PaymentMethod;
  description: string | null;
  paidByUserId: number | null;
  paidByName: string | null;
}

export interface ExpenseUpsertRequest {
  categoryId: number;
  amount: number;
  expenseDate: string;
  method: PaymentMethod;
  description?: string | null;
  clinicId?: number | null;
  paidByUserId?: number | null;
}

// ---------------------------------------------------------------------------
// Kurum (firma) carileri
// ---------------------------------------------------------------------------

export interface CompanyDto {
  id: number;
  name: string;
  taxOffice: string | null;
  vkn: string | null;
  address: string | null;
  email: string | null;
  phone: string | null;
  priceListId: number | null;
  isEInvoiceUser: boolean;
  eInvoiceAlias: string | null;
  balance: number;
  createdAtUtc: string;
}

export interface CompanyUpsertRequest {
  name: string;
  taxOffice?: string | null;
  vkn?: string | null;
  address?: string | null;
  email?: string | null;
  phone?: string | null;
  priceListId?: number | null;
  isEInvoiceUser?: boolean;
  eInvoiceAlias?: string | null;
}
