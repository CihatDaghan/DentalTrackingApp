import { Translation } from 'primeng/api';

/** PrimeNG bileşen yerelleştirmesi (datepicker gün/ay adları, onay metinleri...). */
export const PRIMENG_TR: Translation = {
  accept: 'Evet',
  reject: 'Hayır',
  apply: 'Uygula',
  cancel: 'İptal',
  clear: 'Temizle',
  today: 'Bugün',
  weekHeader: 'Hf',
  firstDayOfWeek: 1,
  dateFormat: 'dd.mm.yy',
  dayNames: ['Pazar', 'Pazartesi', 'Salı', 'Çarşamba', 'Perşembe', 'Cuma', 'Cumartesi'],
  dayNamesShort: ['Paz', 'Pzt', 'Sal', 'Çar', 'Per', 'Cum', 'Cmt'],
  dayNamesMin: ['Pz', 'Pt', 'Sa', 'Ça', 'Pe', 'Cu', 'Ct'],
  monthNames: [
    'Ocak', 'Şubat', 'Mart', 'Nisan', 'Mayıs', 'Haziran',
    'Temmuz', 'Ağustos', 'Eylül', 'Ekim', 'Kasım', 'Aralık',
  ],
  monthNamesShort: [
    'Oca', 'Şub', 'Mar', 'Nis', 'May', 'Haz',
    'Tem', 'Ağu', 'Eyl', 'Eki', 'Kas', 'Ara',
  ],
  emptyMessage: 'Sonuç bulunamadı',
  emptySearchMessage: 'Sonuç bulunamadı',
  emptyFilterMessage: 'Sonuç bulunamadı',
};

export const PRIMENG_EN: Translation = {
  firstDayOfWeek: 1,
  dateFormat: 'dd.mm.yy',
};
