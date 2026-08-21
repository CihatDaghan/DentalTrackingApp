import { Pipe, PipeTransform } from '@angular/core';

const formatter = new Intl.NumberFormat('tr-TR', {
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

/** TR para bicimi: 1.234,56 ₺ (varsayilan TRY). */
@Pipe({ name: 'money' })
export class MoneyPipe implements PipeTransform {
  transform(value: number | null | undefined, currency = '₺'): string {
    if (value == null || Number.isNaN(value)) {
      return '—';
    }
    return `${formatter.format(value)} ${currency}`;
  }
}
