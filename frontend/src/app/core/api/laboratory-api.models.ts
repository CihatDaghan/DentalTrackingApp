/**
 * Elle tiplenmis laboratuvar API sozlesmesi (lab firmasi + lab vakasi).
 * NOT: Sonraki asamada NSwag ile `api-client.generated.ts` uretilecek ve bu dosya kaldirilacak.
 * Enum degerleri arka uctaki `Dental.Domain.Enums` ile birebir ayni tutulmalidir.
 */

// ---------------------------------------------------------------------------
// Enumlar (arka uc: byte)
// ---------------------------------------------------------------------------

export const LabCaseStatus = {
  Draft: 1,
  Sent: 2,
  InLab: 3,
  TryIn: 4,
  Received: 5,
  Delivered: 6,
  Redo: 7,
} as const;
export type LabCaseStatus = (typeof LabCaseStatus)[keyof typeof LabCaseStatus];

/** Kanban kolon sirasi (Redo ayri bir uyari kolonu olarak sona eklenir). */
export const LAB_KANBAN_STATUSES: LabCaseStatus[] = [
  LabCaseStatus.Draft,
  LabCaseStatus.Sent,
  LabCaseStatus.InLab,
  LabCaseStatus.TryIn,
  LabCaseStatus.Received,
  LabCaseStatus.Delivered,
];

export const LAB_ALL_STATUSES: LabCaseStatus[] = [...LAB_KANBAN_STATUSES, LabCaseStatus.Redo];

/** i18n anahtar sonekleri: `laboratory.status.<key>`. */
export const LAB_STATUS_KEYS: Record<number, string> = {
  [LabCaseStatus.Draft]: 'draft',
  [LabCaseStatus.Sent]: 'sent',
  [LabCaseStatus.InLab]: 'inLab',
  [LabCaseStatus.TryIn]: 'tryIn',
  [LabCaseStatus.Received]: 'received',
  [LabCaseStatus.Delivered]: 'delivered',
  [LabCaseStatus.Redo]: 'redo',
};

/** Vita klasik renk skalasi — protez/kron renk secimi. */
export const VITA_SHADES = [
  'A1',
  'A2',
  'A3',
  'A3.5',
  'A4',
  'B1',
  'B2',
  'B3',
  'B4',
  'C1',
  'C2',
  'C3',
  'C4',
  'D2',
  'D3',
  'D4',
] as const;

/** Sik kullanilan is turleri (serbest metin; dropdown yalniz oneri). */
export const LAB_WORK_TYPE_KEYS = [
  'zirconiaCrown',
  'metalCeramicCrown',
  'emax',
  'inlayOnlay',
  'totalDenture',
  'partialDenture',
  'nightGuard',
  'implantAbutment',
  'temporaryCrown',
  'orthoAppliance',
] as const;

/** Sik kullanilan materyaller. */
export const LAB_MATERIAL_KEYS = [
  'zirconia',
  'metalCeramic',
  'emax',
  'acrylic',
  'composite',
  'chromeCobalt',
  'titanium',
  'pmma',
] as const;

// ---------------------------------------------------------------------------
// Laboratuvar firmasi
// ---------------------------------------------------------------------------

export interface LaboratoryDto {
  id: number;
  name: string;
  phone: string | null;
  email: string | null;
  address: string | null;
  contactPerson: string | null;
}

export interface LaboratoryUpsertRequest {
  name: string;
  phone?: string | null;
  email?: string | null;
  address?: string | null;
  contactPerson?: string | null;
}

// ---------------------------------------------------------------------------
// Lab vakasi
// ---------------------------------------------------------------------------

export interface LabCaseDto {
  id: number;
  caseNo: string;
  clinicId: number;
  patientId: number;
  patientName: string;
  doctorUserId: number;
  doctorName: string;
  laboratoryId: number;
  laboratoryName: string;
  workType: string;
  teethCsv: string | null;
  shade: string | null;
  material: string | null;
  status: LabCaseStatus;
  sentDate: string | null; // "yyyy-MM-dd"
  dueDate: string | null;
  receivedDate: string | null;
  price: number | null;
  note: string | null;
  /** Arka uc hesaplar: beklenen teslim gecti ve vaka henuz gelmedi. */
  isOverdue: boolean;
  createdAtUtc: string;
}

export interface LabCaseUpsertRequest {
  patientId: number;
  doctorUserId: number;
  laboratoryId: number;
  workType: string;
  teethCsv?: string | null;
  shade?: string | null;
  material?: string | null;
  sentDate?: string | null;
  dueDate?: string | null;
  price?: number | null;
  note?: string | null;
  clinicId?: number | null;
}

export interface LabCaseStatusChangeRequest {
  status: LabCaseStatus;
  note?: string | null;
}

export interface LabCaseHistoryDto {
  id: number;
  status: LabCaseStatus;
  changedAtUtc: string;
  changedByUserId: number | null;
  changedByName: string | null;
  note: string | null;
}

export interface LabCaseListQuery {
  status?: LabCaseStatus | null;
  laboratoryId?: number | null;
  doctorUserId?: number | null;
  patientId?: number | null;
  dueFrom?: string | null;
  dueTo?: string | null;
  overdueOnly?: boolean | null;
  page?: number;
  pageSize?: number;
}
