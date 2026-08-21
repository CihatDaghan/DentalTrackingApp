/**
 * Elle tiplenmis stok API sozlesmesi (kategori, malzeme, hareket).
 * NOT: Sonraki asamada NSwag ile `api-client.generated.ts` uretilecek ve bu dosya kaldirilacak.
 * Enum degerleri arka uctaki `Dental.Domain.Enums.StockEnums` ile birebir ayni tutulmalidir.
 */

// ---------------------------------------------------------------------------
// Enumlar (arka uc: byte)
// ---------------------------------------------------------------------------

export const StockMovementDirection = {
  In: 1,
  Out: 2,
  /** Sayim duzeltmesi: miktar mutlak degere cekilir. */
  Adjustment: 3,
} as const;
export type StockMovementDirection =
  (typeof StockMovementDirection)[keyof typeof StockMovementDirection];

export const StockMovementRefType = {
  Purchase: 1,
  TreatmentUse: 2,
  Waste: 3,
  Count: 4,
} as const;
export type StockMovementRefType = (typeof StockMovementRefType)[keyof typeof StockMovementRefType];

/** i18n anahtar sonekleri: `inventory.direction.<key>`. */
export const STOCK_DIRECTION_KEYS: Record<number, string> = {
  [StockMovementDirection.In]: 'in',
  [StockMovementDirection.Out]: 'out',
  [StockMovementDirection.Adjustment]: 'adjustment',
};

/** i18n anahtar sonekleri: `inventory.refType.<key>`. */
export const STOCK_REF_TYPE_KEYS: Record<number, string> = {
  [StockMovementRefType.Purchase]: 'purchase',
  [StockMovementRefType.TreatmentUse]: 'treatmentUse',
  [StockMovementRefType.Waste]: 'waste',
  [StockMovementRefType.Count]: 'count',
};

/** Sik kullanilan birimler (serbest metin; dropdown yalniz oneri). */
export const STOCK_UNIT_KEYS = ['piece', 'box', 'pack', 'ml', 'gram', 'set'] as const;

// ---------------------------------------------------------------------------
// Kategori
// ---------------------------------------------------------------------------

export interface StockCategoryDto {
  id: number;
  name: string;
}

export interface StockCategoryUpsertRequest {
  name: string;
}

// ---------------------------------------------------------------------------
// Malzeme
// ---------------------------------------------------------------------------

export interface StockItemDto {
  id: number;
  clinicId: number;
  categoryId: number | null;
  categoryName: string | null;
  name: string;
  barcode: string | null;
  unit: string | null;
  currentQty: number;
  minQty: number;
  lastPurchasePrice: number | null;
  isActive: boolean;
  /** Arka uc hesaplar: currentQty <= minQty. */
  isLow: boolean;
}

export interface StockItemUpsertRequest {
  categoryId?: number | null;
  name: string;
  unit?: string | null;
  barcode?: string | null;
  minQty?: number | null;
  isActive?: boolean;
  clinicId?: number | null;
}

export interface StockItemListQuery {
  search?: string | null;
  categoryId?: number | null;
  includeInactive?: boolean | null;
}

// ---------------------------------------------------------------------------
// Hareket
// ---------------------------------------------------------------------------

export interface StockMovementDto {
  id: number;
  stockItemId: number;
  direction: StockMovementDirection;
  qty: number;
  unitCost: number | null;
  refType: StockMovementRefType;
  movedAtUtc: string;
  note: string | null;
  userId: number | null;
  userName: string | null;
}

export interface StockMovementCreateRequest {
  direction: StockMovementDirection;
  qty: number;
  refType: StockMovementRefType;
  unitCost?: number | null;
  movedAtUtc?: string | null;
  note?: string | null;
}
