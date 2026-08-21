/**
 * Rapor + dashboard sozlesmeleri (arka uc: Dental.Application.Reports).
 * Enum degerleri `Dental.Application.Reports.ReportGroupBy` ile birebir ayni tutulmalidir.
 */

import { AppointmentStatus } from './api.models';
import { PaymentMethod } from './finance-api.models';

export const ReportGroupBy = {
  Day: 1,
  Week: 2,
  Month: 3,
} as const;
export type ReportGroupBy = (typeof ReportGroupBy)[keyof typeof ReportGroupBy];

/** Tum raporlarin ortak filtresi (tarihler "yyyy-MM-dd"). */
export interface ReportQuery {
  from?: string | null;
  to?: string | null;
  doctorId?: number | null;
  clinicId?: number | null;
  categoryId?: number | null;
  groupBy?: ReportGroupBy | null;
  minBalance?: number | null;
}

export interface ReportPeriodDto {
  from: string;
  to: string;
  groupBy: ReportGroupBy;
}

export interface PaymentMethodTotalDto {
  method: PaymentMethod;
  total: number;
  count: number;
}

// --- Ciro ------------------------------------------------------------------

export interface RevenuePointDto {
  period: string;
  periodLabel: string;
  treatmentRevenue: number;
  collected: number;
  treatmentCount: number;
}

export interface RevenueReportDto {
  period: ReportPeriodDto;
  series: RevenuePointDto[];
  byMethod: PaymentMethodTotalDto[];
  totalTreatmentRevenue: number;
  totalCollected: number;
  totalTreatmentCount: number;
}

// --- Gelir-Gider -----------------------------------------------------------

export interface IncomeExpensePointDto {
  period: string;
  periodLabel: string;
  income: number;
  expense: number;
  net: number;
}

export interface ExpenseCategoryTotalDto {
  categoryId: number;
  categoryName: string;
  total: number;
  count: number;
}

export interface IncomeExpenseReportDto {
  period: ReportPeriodDto;
  series: IncomeExpensePointDto[];
  expensesByCategory: ExpenseCategoryTotalDto[];
  totalIncome: number;
  totalExpense: number;
  netProfit: number;
}

// --- Hekim performansi -----------------------------------------------------

export interface DoctorPerformanceRowDto {
  doctorUserId: number;
  doctorName: string;
  branch: string | null;
  patientCount: number;
  treatmentCount: number;
  producedRevenue: number;
  collectedRevenue: number;
  appointmentCount: number;
  noShowCount: number;
  noShowRate: number;
}

export interface DoctorPerformanceReportDto {
  period: ReportPeriodDto;
  rows: DoctorPerformanceRowDto[];
}

// --- Tahsilat + yaslandirma ------------------------------------------------

export interface CollectionPointDto {
  period: string;
  periodLabel: string;
  total: number;
  count: number;
}

/** Yaslandirma kovasi: "0-30" | "31-60" | "61-90" | "90+". */
export interface AgingBucketDto {
  bucket: string;
  amount: number;
  patientCount: number;
}

export interface CollectionsReportDto {
  period: ReportPeriodDto;
  series: CollectionPointDto[];
  byMethod: PaymentMethodTotalDto[];
  totalCollected: number;
  totalCount: number;
  aging: AgingBucketDto[];
  totalOutstanding: number;
}

// --- Tedavi ----------------------------------------------------------------

export interface TreatmentReportRowDto {
  treatmentDefinitionId: number;
  code: string;
  name: string;
  categoryId: number;
  categoryName: string;
  count: number;
  grossAmount: number;
  discountAmount: number;
  netAmount: number;
}

export interface TreatmentCategoryTotalDto {
  categoryId: number;
  categoryName: string;
  count: number;
  netAmount: number;
}

export interface TreatmentsReportDto {
  period: ReportPeriodDto;
  rows: TreatmentReportRowDto[];
  byCategory: TreatmentCategoryTotalDto[];
  totalCount: number;
  totalNetAmount: number;
}

// --- Randevu ---------------------------------------------------------------

export interface AppointmentStatusTotalDto {
  status: AppointmentStatus;
  count: number;
}

export interface AppointmentTrendPointDto {
  period: string;
  periodLabel: string;
  total: number;
  completed: number;
  noShow: number;
  cancelled: number;
  noShowRate: number;
  bookedMinutes: number;
  capacityMinutes: number;
  occupancyRate: number;
}

export interface AppointmentsReportDto {
  period: ReportPeriodDto;
  byStatus: AppointmentStatusTotalDto[];
  trend: AppointmentTrendPointDto[];
  totalCount: number;
  noShowCount: number;
  cancelledCount: number;
  noShowRate: number;
  occupancyRate: number;
}

// --- Borclular -------------------------------------------------------------

export interface DebtorRowDto {
  patientId: number;
  fileNo: string;
  fullName: string;
  phone: string | null;
  balance: number;
  lastEntryDate: string | null;
  lastAppointmentUtc: string | null;
}

// --- Dashboard -------------------------------------------------------------

export interface DashboardAppointmentStatusDto {
  status: AppointmentStatus;
  count: number;
}

export interface DashboardPendingWorkDto {
  overdueLabCases: number;
  lowStockItems: number;
  unsignedConsents: number;
  eInvoiceErrors: number;
  failedMessages: number;
  pendingEnabizPackets: number;
}

export interface RevenueTrendPointDto {
  date: string;
  amount: number;
}

export interface DashboardBirthdayPatientDto {
  patientId: number;
  fullName: string;
  phone: string | null;
  age: number | null;
}

export interface DashboardSummaryDto {
  date: string;
  todayRevenue: number;
  monthRevenue: number;
  todayCollections: number;
  monthCollections: number;
  todayExpenses: number;
  totalOutstanding: number;
  todayAppointmentCount: number;
  todayAppointmentsByStatus: DashboardAppointmentStatusDto[];
  pendingWork: DashboardPendingWorkDto;
  last30DaysRevenue: RevenueTrendPointDto[];
  birthdayPatients: DashboardBirthdayPatientDto[];
  activePatientCount: number;
}
