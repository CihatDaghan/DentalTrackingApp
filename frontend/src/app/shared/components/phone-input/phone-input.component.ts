import { ChangeDetectionStrategy, Component, forwardRef, input, signal } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { InputTextModule } from 'primeng/inputtext';

/**
 * TR cep telefonu girisi: +90 5xx xxx xx xx bicimine kademeli maskeler.
 * Deger olarak bicimli dize ("+90 532 111 22 33") ya da null uretir.
 */
@Component({
  selector: 'app-phone-input',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [InputTextModule],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => PhoneInputComponent),
      multi: true,
    },
  ],
  template: `
    <input
      pInputText
      type="tel"
      class="w-full"
      [id]="inputId()"
      [value]="display()"
      [disabled]="disabled()"
      placeholder="+90 5__ ___ __ __"
      autocomplete="tel"
      (input)="onInput($event)"
      (blur)="onTouched()"
    />
  `,
})
export class PhoneInputComponent implements ControlValueAccessor {
  readonly inputId = input<string>('');

  protected readonly display = signal('');
  protected readonly disabled = signal(false);

  private onChange: (value: string | null) => void = () => undefined;
  protected onTouched: () => void = () => undefined;

  writeValue(value: string | null): void {
    this.display.set(value ? this.format(this.normalize(value)) : '');
  }

  registerOnChange(fn: (value: string | null) => void): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }

  setDisabledState(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
  }

  protected onInput(event: Event): void {
    const inputEl = event.target as HTMLInputElement;
    const digits = this.normalize(inputEl.value);
    const formatted = this.format(digits);
    this.display.set(formatted);
    inputEl.value = formatted;
    this.onChange(formatted || null);
  }

  /** Girdiyi ulusal 10 haneye indirger: +90/90/0 oneklerini atar. */
  private normalize(raw: string): string {
    let digits = raw.replace(/\D/g, '');
    if (digits.startsWith('90') && digits.length > 10) {
      digits = digits.slice(2);
    }
    if (digits.startsWith('0')) {
      digits = digits.slice(1);
    }
    return digits.slice(0, 10);
  }

  /** "5321112233" -> "+90 532 111 22 33" (kademeli). */
  private format(digits: string): string {
    if (!digits) {
      return '';
    }
    const parts = [digits.slice(0, 3), digits.slice(3, 6), digits.slice(6, 8), digits.slice(8, 10)];
    return ('+90 ' + parts.filter(Boolean).join(' ')).trim();
  }
}
