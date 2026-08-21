import {
  ToothChartLegendItem,
  ToothState,
} from '../../shared/components/tooth-chart/tooth-chart.models';

/**
 * Dev onizleme icin zengin ornek veri.
 * Renkler tedavi katalogu kategorilerini temsil eder (DB: Category.ColorHex).
 */
const FILLING = '#3b82f6'; // Dolgu
const ENDO = '#ef4444'; // Endodonti
const PROSTH = '#8b5cf6'; // Protez
const SURGERY = '#6b7280'; // Cerrahi / Cekim
const IMPLANT = '#10b981'; // Implant
const PERIO = '#f59e0b'; // Periodontoloji

export const SAMPLE_LEGEND: ToothChartLegendItem[] = [
  { color: FILLING, labelKey: 'toothChart.categories.filling' },
  { color: ENDO, labelKey: 'toothChart.categories.endo' },
  { color: PROSTH, labelKey: 'toothChart.categories.prosthesis' },
  { color: SURGERY, labelKey: 'toothChart.categories.surgery' },
  { color: IMPLANT, labelKey: 'toothChart.categories.implant' },
  { color: PERIO, labelKey: 'toothChart.categories.perio' },
];

export const ADULT_SAMPLE: ToothState[] = [
  // Eksik disler
  {
    toothNo: '18',
    layers: [{ layer: 'treatment', treatmentCode: 'EXT', treatmentName: 'Çekim', color: SURGERY, missing: true }],
  },
  {
    toothNo: '38',
    layers: [{ layer: 'treatment', treatmentCode: 'EXT', treatmentName: 'Çekim', color: SURGERY, missing: true }],
  },
  // Cekim plani (tum kuron, kesikli gri)
  {
    toothNo: '17',
    layers: [{ layer: 'plan', treatmentCode: 'EXT', treatmentName: 'Çekim planı', color: SURGERY }],
  },
  // Dolgular — farkli yuzeyler
  {
    toothNo: '16',
    layers: [
      { layer: 'treatment', treatmentCode: 'F-COM', treatmentName: 'Kompozit dolgu', color: FILLING, surfaces: ['O'] },
    ],
  },
  {
    toothNo: '14',
    layers: [
      { layer: 'diagnosis', treatmentCode: 'CARIES', treatmentName: 'Çürük', color: FILLING, surfaces: ['O', 'D'] },
    ],
  },
  {
    toothNo: '24',
    layers: [
      { layer: 'plan', treatmentCode: 'F-COM', treatmentName: 'Kompozit dolgu planı', color: FILLING, surfaces: ['M', 'O'] },
    ],
  },
  {
    toothNo: '44',
    layers: [
      { layer: 'treatment', treatmentCode: 'F-AMG', treatmentName: 'Amalgam dolgu', color: FILLING, surfaces: ['B', 'O'] },
    ],
  },
  {
    toothNo: '47',
    layers: [
      { layer: 'plan', treatmentCode: 'F-COM', treatmentName: 'Kompozit dolgu planı', color: FILLING, surfaces: ['D'] },
    ],
  },
  // Kanal tedavileri
  {
    toothNo: '13',
    layers: [{ layer: 'plan', treatmentCode: 'RCT', treatmentName: 'Kanal tedavisi planı', color: ENDO, rootCanal: true }],
  },
  {
    toothNo: '26',
    layers: [
      { layer: 'treatment', treatmentCode: 'RCT', treatmentName: 'Kanal tedavisi', color: ENDO, rootCanal: true },
      { layer: 'treatment', treatmentCode: 'F-COM', treatmentName: 'Kompozit dolgu', color: FILLING, surfaces: ['M', 'O'] },
    ],
  },
  // Kuronlar
  {
    toothNo: '11',
    layers: [{ layer: 'treatment', treatmentCode: 'CRW-Z', treatmentName: 'Zirkonyum kuron', color: PROSTH }],
  },
  {
    toothNo: '21',
    layers: [{ layer: 'plan', treatmentCode: 'CRW-Z', treatmentName: 'Kuron planı', color: PROSTH }],
  },
  // Kopru: 34 (ayak) - 35 (eksik govde) - 36 (ayak)
  {
    toothNo: '34',
    layers: [{ layer: 'treatment', treatmentCode: 'BRG', treatmentName: 'Köprü ayağı', color: PROSTH }],
  },
  {
    toothNo: '35',
    layers: [
      { layer: 'treatment', treatmentCode: 'BRG-P', treatmentName: 'Köprü gövdesi (eksik diş)', color: PROSTH, missing: true },
    ],
  },
  {
    toothNo: '36',
    layers: [{ layer: 'treatment', treatmentCode: 'BRG', treatmentName: 'Köprü ayağı', color: PROSTH }],
  },
  // Implant + ustu kuron
  {
    toothNo: '46',
    layers: [
      { layer: 'treatment', treatmentCode: 'IMP', treatmentName: 'İmplant', color: IMPLANT, implant: true },
      { layer: 'treatment', treatmentCode: 'CRW-I', treatmentName: 'İmplant üstü kuron', color: PROSTH },
    ],
  },
  // Periodontal tani (tum kuron, tarama)
  {
    toothNo: '41',
    layers: [{ layer: 'diagnosis', treatmentCode: 'GING', treatmentName: 'Gingivitis', color: PERIO }],
  },
  {
    toothNo: '42',
    layers: [{ layer: 'diagnosis', treatmentCode: 'GING', treatmentName: 'Gingivitis', color: PERIO }],
  },
  // Bukkal yuzey curugu
  {
    toothNo: '27',
    layers: [
      { layer: 'diagnosis', treatmentCode: 'CARIES', treatmentName: 'Çürük', color: FILLING, surfaces: ['B'] },
    ],
  },
  // Karma dentisyon senaryosu: 15 yerine sut disi 55 gosterildiginde bu kayit gorunur
  {
    toothNo: '55',
    layers: [
      { layer: 'diagnosis', treatmentCode: 'CARIES', treatmentName: 'Çürük (süt dişi)', color: FILLING, surfaces: ['O'] },
    ],
  },
];

export const PRIMARY_SAMPLE: ToothState[] = [
  {
    toothNo: '55',
    layers: [
      { layer: 'treatment', treatmentCode: 'F-COM', treatmentName: 'Kompozit dolgu', color: FILLING, surfaces: ['O'] },
    ],
  },
  {
    toothNo: '54',
    layers: [
      { layer: 'diagnosis', treatmentCode: 'CARIES', treatmentName: 'Çürük', color: FILLING, surfaces: ['O', 'M'] },
    ],
  },
  {
    toothNo: '51',
    layers: [{ layer: 'treatment', treatmentCode: 'EXT', treatmentName: 'Çekim', color: SURGERY, missing: true }],
  },
  {
    toothNo: '61',
    layers: [{ layer: 'plan', treatmentCode: 'CRW-S', treatmentName: 'Paslanmaz çelik kuron planı', color: PROSTH }],
  },
  {
    toothNo: '64',
    layers: [
      { layer: 'treatment', treatmentCode: 'RCT-P', treatmentName: 'Amputasyon (kanal)', color: ENDO, rootCanal: true },
      { layer: 'treatment', treatmentCode: 'F-COM', treatmentName: 'Kompozit dolgu', color: FILLING, surfaces: ['O'] },
    ],
  },
  {
    toothNo: '75',
    layers: [
      { layer: 'plan', treatmentCode: 'F-COM', treatmentName: 'Kompozit dolgu planı', color: FILLING, surfaces: ['O', 'B'] },
    ],
  },
  {
    toothNo: '84',
    layers: [
      { layer: 'diagnosis', treatmentCode: 'CARIES', treatmentName: 'Çürük', color: FILLING, surfaces: ['D'] },
    ],
  },
];

/** Karma dentisyon ornegi: 15 ve 25 pozisyonlarinda sut disleri gosterilir. */
export const MIXED_OVERRIDES: Record<string, 'adult' | 'primary'> = {
  '15': 'primary',
  '25': 'primary',
};
