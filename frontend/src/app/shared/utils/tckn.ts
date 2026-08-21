import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/** TCKN algoritma dogrulamasi (11 hane, ilk hane 0 olamaz, cift checksum). */
export function isValidTckn(value: string | null | undefined): boolean {
  if (!value || !/^[1-9]\d{10}$/.test(value)) {
    return false;
  }
  const digits = value.split('').map(Number);
  const oddSum = digits[0] + digits[2] + digits[4] + digits[6] + digits[8];
  const evenSum = digits[1] + digits[3] + digits[5] + digits[7];
  const d10 = (oddSum * 7 - evenSum) % 10;
  const d11 = digits.slice(0, 10).reduce((a, b) => a + b, 0) % 10;
  return (d10 + 10) % 10 === digits[9] && d11 === digits[10];
}

/** Reactive form validator'i: bos deger gecerli sayilir (required ayri kullanilir). */
export function tcknValidator(): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = control.value as string | null;
    if (!value) {
      return null;
    }
    return isValidTckn(value) ? null : { tckn: true };
  };
}

/** Kimlik maskeleme: ilk 3 + *** + son 2 (or. 123*****89). */
export function maskIdentity(value: string | null | undefined): string {
  if (!value || value.length < 6) {
    return value ?? '—';
  }
  return `${value.slice(0, 3)}${'*'.repeat(value.length - 5)}${value.slice(-2)}`;
}
