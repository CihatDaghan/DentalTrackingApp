/**
 * Elle yazilmis, stilize-anatomik dis SVG path sablonlari.
 *
 * Yerel koordinat sistemi (her sablon icin ortak sozlesme):
 *  - y = 0  : kole (servikal) cizgisi — kuron/kok birlesimi
 *  - y > 0  : kuron (okluzal/insizal kenara dogru buyur)
 *  - y < 0  : kok(ler) (apekse dogru kuculur)
 *  - x      : mezio-distal eksen, sablonlar ~simetriktir
 *
 * Ust cene disleri bu haliyle cizilir (kokler yukari, kuron asagi/okluzal orta
 * seride bakar); alt cene `scale(1,-1)` ile aynalanir. Kokler ayri path'lerdir
 * ki kanal tedavisi isaretlemesinde bagimsiz boyanabilsinler.
 */

export type ToothShapeKind =
  | 'incisor'
  | 'canine'
  | 'premolar'
  | 'molarUpper'
  | 'molarLower'
  | 'pIncisor'
  | 'pCanine'
  | 'pMolarUpper'
  | 'pMolarLower';

export interface ToothTemplate {
  /** Kuron path'i. */
  crown: string;
  /** Kok path'leri — kanal isaretlemesi icin ayri boyanir. */
  roots: string[];
  /** Kuron yuksekligi (yerel birim) — eksik dis carpisi vb. icin. */
  crownH: number;
}

/** Daimi kesici (santral): keski bicimli kuron, tek konik kok. */
const INCISOR: ToothTemplate = {
  crownH: 84,
  crown:
    'M -15 3 C -18 26 -21 54 -21 70 C -21 80 -14 84 0 84 ' +
    'C 14 84 21 80 21 70 C 21 54 18 26 15 3 C 9 -1 -9 -1 -15 3 Z',
  roots: [
    'M -14 3 C -13 -28 -8 -72 -2 -103 C -1 -108 1 -108 2 -103 ' +
      'C 8 -72 13 -28 14 3 C 8 7 -8 7 -14 3 Z',
  ],
};

/** Daimi kanin: sivri tuberkullu kuron, en uzun tek kok. */
const CANINE: ToothTemplate = {
  crownH: 88,
  crown:
    'M -16 3 C -20 26 -23 48 -23 60 C -23 71 -13 77 -2 87 C -1 88 1 88 2 87 ' +
    'C 13 77 23 71 23 60 C 23 48 20 26 16 3 C 9 -1 -9 -1 -16 3 Z',
  roots: [
    'M -14 3 C -13 -30 -8 -76 -2 -107 C -1 -112 1 -112 2 -107 ' +
      'C 8 -76 13 -30 14 3 C 8 7 -8 7 -14 3 Z',
  ],
};

/** Premolar: cift tuberkullu kuron, tek kok. */
const PREMOLAR: ToothTemplate = {
  crownH: 80,
  crown:
    'M -19 3 C -23 24 -25 46 -25 60 C -25 73 -19 80 -11 80 C -6 80 -5 75 0 75 ' +
    'C 5 75 6 80 11 80 C 19 80 25 73 25 60 C 25 46 23 24 19 3 C 11 -1 -11 -1 -19 3 Z',
  roots: [
    'M -14 3 C -13 -26 -8 -66 -2 -95 C -1 -100 1 -100 2 -95 ' +
      'C 8 -66 13 -26 14 3 C 8 7 -8 7 -14 3 Z',
  ],
};

/** Genis, cok tuberkullu molar kuronu (ust/alt ortak). */
const MOLAR_CROWN =
  'M -22 3 C -27 22 -28 42 -28 56 C -28 70 -22 78 -15 78 C -10 78 -9 73 -5 73 ' +
  'C -2 73 -2 77 0 77 C 2 77 2 73 5 73 C 9 73 10 78 15 78 C 22 78 28 70 28 56 ' +
  'C 28 42 27 22 22 3 C 13 -2 -13 -2 -22 3 Z';

/** Ust molar: 3 kok (2 bukkal + uzun palatinal orta). */
const MOLAR_UPPER: ToothTemplate = {
  crownH: 78,
  crown: MOLAR_CROWN,
  roots: [
    'M -25 5 C -28 -16 -25 -48 -17 -80 C -15 -86 -10 -85 -11 -79 ' +
      'C -13 -52 -12 -26 -7 0 C -13 6 -20 8 -25 5 Z',
    'M 25 5 C 28 -16 25 -48 17 -80 C 15 -86 10 -85 11 -79 ' +
      'C 13 -52 12 -26 7 0 C 13 6 20 8 25 5 Z',
    'M -7 2 C -8 -30 -6 -64 -2 -93 C -1 -98 1 -98 2 -93 ' +
      'C 6 -64 8 -30 7 2 C 3 6 -3 6 -7 2 Z',
  ],
};

/** Alt molar: 2 ayrik kok (mezial + distal). */
const MOLAR_LOWER: ToothTemplate = {
  crownH: 78,
  crown: MOLAR_CROWN,
  roots: [
    'M -24 5 C -27 -18 -24 -54 -15 -88 C -13 -94 -8 -93 -9 -87 ' +
      'C -11 -58 -9 -28 -4 0 C -10 6 -19 8 -24 5 Z',
    'M 24 5 C 27 -18 24 -54 15 -88 C 13 -94 8 -93 9 -87 ' +
      'C 11 -58 9 -28 4 0 C 10 6 19 8 24 5 Z',
  ],
};

/** Sut kesici: kisa, tombul kuron; kisa kok. */
const P_INCISOR: ToothTemplate = {
  crownH: 62,
  crown:
    'M -13 3 C -16 20 -17 38 -17 48 C -17 58 -10 62 0 62 ' +
    'C 10 62 17 58 17 48 C 17 38 16 20 13 3 C 8 -1 -8 -1 -13 3 Z',
  roots: [
    'M -11 3 C -10 -20 -6 -50 -2 -73 C -1 -78 1 -78 2 -73 ' +
      'C 6 -50 10 -20 11 3 C 6 6 -6 6 -11 3 Z',
  ],
};

/** Sut kanin: kucuk sivri kuron. */
const P_CANINE: ToothTemplate = {
  crownH: 66,
  crown:
    'M -14 3 C -17 20 -18 36 -18 44 C -18 53 -10 58 -2 65 C -1 66 1 66 2 65 ' +
    'C 10 58 18 53 18 44 C 18 36 17 20 14 3 C 8 -1 -8 -1 -14 3 Z',
  roots: [
    'M -11 3 C -10 -22 -6 -54 -2 -77 C -1 -82 1 -82 2 -77 ' +
      'C 6 -54 10 -22 11 3 C 6 6 -6 6 -11 3 Z',
  ],
};

/** Tombul sut molar kuronu. */
const P_MOLAR_CROWN =
  'M -20 3 C -26 14 -27 34 -26 44 C -25 55 -19 60 -12 60 C -8 60 -7 56 -4 56 ' +
  'C -1 56 -1 59 0 59 C 1 59 1 56 4 56 C 7 56 8 60 12 60 C 19 60 25 55 26 44 ' +
  'C 27 34 26 14 20 3 C 12 -2 -12 -2 -20 3 Z';

/** Ust sut molar: 3 ince, ayrik kok. */
const P_MOLAR_UPPER: ToothTemplate = {
  crownH: 60,
  crown: P_MOLAR_CROWN,
  roots: [
    'M -22 4 C -26 -12 -25 -36 -18 -58 C -16 -63 -12 -62 -13 -57 ' +
      'C -15 -38 -13 -20 -7 0 C -12 5 -18 7 -22 4 Z',
    'M 22 4 C 26 -12 25 -36 18 -58 C 16 -63 12 -62 13 -57 ' +
      'C 15 -38 13 -20 7 0 C 12 5 18 7 22 4 Z',
    'M -5 1 C -6 -24 -5 -46 -1 -64 C -0.5 -68 0.5 -68 1 -64 ' +
      'C 5 -46 6 -24 5 1 C 2 4 -2 4 -5 1 Z',
  ],
};

/** Alt sut molar: 2 genis-ayrik ince kok. */
const P_MOLAR_LOWER: ToothTemplate = {
  crownH: 60,
  crown: P_MOLAR_CROWN,
  roots: [
    'M -21 4 C -26 -14 -25 -40 -17 -64 C -15 -69 -11 -68 -12 -63 ' +
      'C -14 -42 -12 -22 -6 0 C -11 5 -17 7 -21 4 Z',
    'M 21 4 C 26 -14 25 -40 17 -64 C 15 -69 11 -68 12 -63 ' +
      'C 14 -42 12 -22 6 0 C 11 5 17 7 21 4 Z',
  ],
};

export const TOOTH_TEMPLATES: Record<ToothShapeKind, ToothTemplate> = {
  incisor: INCISOR,
  canine: CANINE,
  premolar: PREMOLAR,
  molarUpper: MOLAR_UPPER,
  molarLower: MOLAR_LOWER,
  pIncisor: P_INCISOR,
  pCanine: P_CANINE,
  pMolarUpper: P_MOLAR_UPPER,
  pMolarLower: P_MOLAR_LOWER,
};

/** Implant vidasi — kok bolgesinde cizilir (govde + yiv cizgileri). */
export const IMPLANT_SHAPE = {
  body:
    'M -8 4 C -9 -10 -8 -30 -6 -55 C -5 -70 -4 -76 0 -76 ' +
    'C 4 -76 5 -70 6 -55 C 8 -30 9 -10 8 4 C 3 8 -3 8 -8 4 Z',
  threads:
    'M -8 -8 L 8 -13 M -8 -22 L 8 -27 M -7 -36 L 7 -41 M -6 -50 L 6 -54 M -5 -61 L 5 -64',
};
