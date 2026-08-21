/**
 * Dis semasi (odontogram) veri modeli — design-1.md §2.6.
 * Renkler tedavi katalogu kategorisinden gelir (Category.ColorHex), hardcode edilmez.
 */

/** Dis yuzeyi: Mesial, Distal, Okluzal/Insizal, Bukkal/Vestibul, Lingual/Palatinal. */
export type Surface = 'M' | 'D' | 'O' | 'B' | 'L';

/** Kayit katmani: tani / plan / yapilan tedavi. */
export type ToothLayer = 'diagnosis' | 'plan' | 'treatment';

/** Dentisyon tipi: daimi (32 dis) / sut (20 dis). */
export type Dentition = 'adult' | 'primary';

/** Tek bir tani/plan/tedavi kaydi. */
export interface ToothLayerEntry {
  layer: ToothLayer;
  treatmentCode: string;
  /** Tooltip'te gosterilecek ad (katalogdan); yoksa treatmentCode gosterilir. */
  treatmentName?: string;
  /** Katalog kategorisinden gelen renk, orn. '#3b82f6'. */
  color: string;
  /** Yuzey bazli kayit; bos/tanimsiz ise tum kuronu kapsar. */
  surfaces?: Surface[];
  /** Kanal tedavisi — kokler bu kaydin rengiyle isaretlenir. */
  rootCanal?: boolean;
  /** Eksik dis (cekilmis / konjenital eksik). */
  missing?: boolean;
  /** Implant — kok yerine vida ikonu cizilir. */
  implant?: boolean;
}

/** Bir disin tum kayitlari. FDI: "11".."48" daimi, "51".."85" sut. */
export interface ToothState {
  toothNo: string;
  layers: ToothLayerEntry[];
}

/** Yuzey tiklama olayi. */
export interface SurfaceClickEvent {
  toothNo: string;
  surface: Surface;
}

/** Lejant ogesi: sabit metin (katalogdan) veya transloco anahtari. */
export interface ToothChartLegendItem {
  color: string;
  label?: string;
  labelKey?: string;
}
