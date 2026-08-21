import { Pipe, PipeTransform } from '@angular/core';
import { format } from 'date-fns';
import { tr } from 'date-fns/locale';
import { parseUtc } from '../../core/api/api.models';

/**
 * UTC ISO dizesini (veya Date'i) yerel saatte bicimler; TR ay/gun adlari.
 * `dateOnly=true` ile "yyyy-MM-dd" dizeleri yerel gun olarak yorumlanir.
 */
@Pipe({ name: 'trDate' })
export class TrDatePipe implements PipeTransform {
  transform(
    value: string | Date | null | undefined,
    pattern = 'dd.MM.yyyy',
    dateOnly = false,
  ): string {
    if (!value) {
      return '—';
    }
    let date: Date;
    if (value instanceof Date) {
      date = value;
    } else if (dateOnly || /^\d{4}-\d{2}-\d{2}$/.test(value)) {
      const [y, m, d] = value.split('T')[0].split('-').map(Number);
      date = new Date(y, m - 1, d);
    } else {
      date = parseUtc(value);
    }
    return format(date, pattern, { locale: tr });
  }
}
