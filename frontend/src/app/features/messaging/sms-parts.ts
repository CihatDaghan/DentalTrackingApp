/**
 * SMS karakter/parca hesabi (GSM 03.38).
 *
 * Metnin tamami GSM-7 alfabesindeyse 160 karakter/parca (coklu parcada 153),
 * bir tek GSM-7 disi karakter (or. Turkce ç/ğ/ı/ş/İ) varsa mesaj UCS-2'ye duser:
 * 70 karakter/parca (coklu parcada 67).
 */

/** GSM 03.38 temel alfabesi (satir sonu ve bosluk dahil). */
const GSM7_BASIC = new Set(
  (
    '@£$¥èéùìòÇ\nØø\rÅåΔ_ΦΓΛΩΠΨΣΘΞÆæßÉ !"#¤%&\'()*+,-./0123456789:;<=>?' +
    '¡ABCDEFGHIJKLMNOPQRSTUVWXYZÄÖÑÜ§¿abcdefghijklmnopqrstuvwxyzäöñüà'
  ).split(''),
);

/** Genisletme tablosu: her biri 2 septet yer kaplar. */
const GSM7_EXTENDED = new Set('^{}\\[~]|€'.split(''));

export type SmsEncoding = 'gsm7' | 'ucs2';

export interface SmsPartsInfo {
  encoding: SmsEncoding;
  /** Kullaniciya gosterilen karakter sayisi (UTF-16 kod noktasi bazli). */
  length: number;
  /** GSM-7'de septet, UCS-2'de kod birimi cinsinden agirlik. */
  weight: number;
  parts: number;
  /** Gecerli parca sinirindaki kapasite (70/160 ya da 67/153). */
  perPart: number;
  /** Son parcada kalan karakter hakki. */
  remaining: number;
  /** Metni UCS-2'ye dusuren ilk karakterler (kullaniciya ipucu). */
  nonGsmSample: string[];
}

/** Metin tamamen GSM-7 alfabesinde mi? */
export function isGsm7(text: string): boolean {
  for (const char of text) {
    if (!GSM7_BASIC.has(char) && !GSM7_EXTENDED.has(char)) {
      return false;
    }
  }
  return true;
}

/** GSM-7 disi (Turkce vb.) karakterleri benzersiz olarak dondurur. */
export function nonGsm7Chars(text: string): string[] {
  const found = new Set<string>();
  for (const char of text) {
    if (!GSM7_BASIC.has(char) && !GSM7_EXTENDED.has(char)) {
      found.add(char);
    }
  }
  return [...found];
}

export function smsParts(text: string): SmsPartsInfo {
  const chars = [...text];
  const nonGsm = nonGsm7Chars(text);
  const encoding: SmsEncoding = nonGsm.length === 0 ? 'gsm7' : 'ucs2';

  let weight: number;
  if (encoding === 'gsm7') {
    weight = chars.reduce((sum, char) => sum + (GSM7_EXTENDED.has(char) ? 2 : 1), 0);
  } else {
    // UCS-2: BMP disi karakterler (emoji) 2 kod birimi.
    weight = text.length;
  }

  const single = encoding === 'gsm7' ? 160 : 70;
  const multi = encoding === 'gsm7' ? 153 : 67;

  if (weight === 0) {
    return {
      encoding,
      length: 0,
      weight: 0,
      parts: 0,
      perPart: single,
      remaining: single,
      nonGsmSample: [],
    };
  }

  const parts = weight <= single ? 1 : Math.ceil(weight / multi);
  const perPart = parts <= 1 ? single : multi;

  return {
    encoding,
    length: chars.length,
    weight,
    parts,
    perPart,
    remaining: parts * perPart - weight,
    nonGsmSample: nonGsm.slice(0, 6),
  };
}
