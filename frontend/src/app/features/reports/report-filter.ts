import { toDateOnly } from '../../core/api/api.models';
import { ReportGroupBy, ReportQuery } from '../../core/api/reports-api.models';

/** Sol dikey menudeki 7 rapor. `key` ayni zamanda export route parcasidir. */
export type ReportKeyName =
  | 'revenue'
  | 'income-expense'
  | 'doctor-performance'
  | 'collections'
  | 'treatments'
  | 'appointments'
  | 'debtors';

export interface ReportTabDef {
  key: ReportKeyName;
  /** i18n: `reports.tabs.<labelKey>`. */
  labelKey: string;
  icon: string;
  /** Ortak filtre cubugunda hangi kontroller gorunsun. */
  showDoctor: boolean;
  showGroupBy: boolean;
  showDateRange: boolean;
  showCategory: boolean;
  showMinBalance: boolean;
}

export const REPORT_TABS: ReportTabDef[] = [
  {
    key: 'revenue',
    labelKey: 'revenue',
    icon: 'fa-solid fa-turkish-lira-sign',
    showDoctor: true,
    showGroupBy: true,
    showDateRange: true,
    showCategory: false,
    showMinBalance: false,
  },
  {
    key: 'income-expense',
    labelKey: 'incomeExpense',
    icon: 'fa-solid fa-scale-balanced',
    showDoctor: false,
    showGroupBy: false,
    showDateRange: true,
    showCategory: false,
    showMinBalance: false,
  },
  {
    key: 'doctor-performance',
    labelKey: 'doctorPerformance',
    icon: 'fa-solid fa-user-doctor',
    showDoctor: true,
    showGroupBy: false,
    showDateRange: true,
    showCategory: false,
    showMinBalance: false,
  },
  {
    key: 'collections',
    labelKey: 'collections',
    icon: 'fa-solid fa-hand-holding-dollar',
    showDoctor: false,
    showGroupBy: true,
    showDateRange: true,
    showCategory: false,
    showMinBalance: false,
  },
  {
    key: 'treatments',
    labelKey: 'treatments',
    icon: 'fa-solid fa-tooth',
    showDoctor: true,
    showGroupBy: false,
    showDateRange: true,
    showCategory: true,
    showMinBalance: false,
  },
  {
    key: 'appointments',
    labelKey: 'appointments',
    icon: 'fa-solid fa-calendar-check',
    showDoctor: true,
    showGroupBy: true,
    showDateRange: true,
    showCategory: false,
    showMinBalance: false,
  },
  {
    key: 'debtors',
    labelKey: 'debtors',
    icon: 'fa-solid fa-file-invoice-dollar',
    showDoctor: false,
    showGroupBy: false,
    showDateRange: false,
    showCategory: false,
    showMinBalance: true,
  },
];

export type DateRangePreset = 'today' | 'thisWeek' | 'thisMonth' | 'lastMonth' | 'custom';

export const DATE_PRESETS: DateRangePreset[] = [
  'today',
  'thisWeek',
  'thisMonth',
  'lastMonth',
  'custom',
];

/** Ön ayarli tarih araligini yerel takvime gore hesaplar (Pazartesi hafta basi). */
export function resolvePreset(preset: DateRangePreset): { from: Date; to: Date } | null {
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  switch (preset) {
    case 'today':
      return { from: today, to: today };
    case 'thisWeek': {
      const day = (today.getDay() + 6) % 7; // Pazartesi = 0
      const from = new Date(today);
      from.setDate(today.getDate() - day);
      const to = new Date(from);
      to.setDate(from.getDate() + 6);
      return { from, to };
    }
    case 'thisMonth': {
      const from = new Date(today.getFullYear(), today.getMonth(), 1);
      const to = new Date(today.getFullYear(), today.getMonth() + 1, 0);
      return { from, to };
    }
    case 'lastMonth': {
      const from = new Date(today.getFullYear(), today.getMonth() - 1, 1);
      const to = new Date(today.getFullYear(), today.getMonth(), 0);
      return { from, to };
    }
    default:
      return null;
  }
}

/** Filtre cubugunun disari verdigi hal. */
export interface ReportFilterState {
  preset: DateRangePreset;
  from: Date | null;
  to: Date | null;
  doctorId: number | null;
  clinicId: number | null;
  categoryId: number | null;
  groupBy: ReportGroupBy;
  minBalance: number;
}

export function initialFilterState(): ReportFilterState {
  const range = resolvePreset('thisMonth')!;
  return {
    preset: 'thisMonth',
    from: range.from,
    to: range.to,
    doctorId: null,
    clinicId: null,
    categoryId: null,
    groupBy: ReportGroupBy.Day,
    minBalance: 0.01,
  };
}

/** Filtre halini API sorgusuna cevirir (yalniz ilgili rapor icin anlamli alanlar). */
export function toReportQuery(state: ReportFilterState, tab: ReportTabDef): ReportQuery {
  return {
    from: tab.showDateRange && state.from ? toDateOnly(state.from) : null,
    to: tab.showDateRange && state.to ? toDateOnly(state.to) : null,
    doctorId: tab.showDoctor ? state.doctorId : null,
    clinicId: state.clinicId,
    categoryId: tab.showCategory ? state.categoryId : null,
    groupBy: tab.showGroupBy ? state.groupBy : null,
    minBalance: tab.showMinBalance ? state.minBalance : null,
  };
}
