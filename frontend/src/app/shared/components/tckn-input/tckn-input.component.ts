import {
  ChangeDetectionStrategy,
  Component,
  computed,
  forwardRef,
  input,
  signal,
} from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { InputTextModule } from 'primeng/inputtext';
import { TooltipModule } from 'primeng/tooltip';
import { TranslocoPipe } from '@jsverse/transloco';
import { isValidTckn } from '../../utils/tckn';

/**
 * TCKN girisi: 11 hane, yalniz rakam; checksum durumu sag ikonla gosterilir.
 * (Form duzeyinde ayrica `tcknValidator()` kullanin.)
 */
@Component({
  selector: 'app-tckn-input',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [InputTextModule, TooltipModule, TranslocoPipe],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => TcknInputComponent),
      multi: true,
    },
  ],
  template: `
    <div class="tckn">
      <input
        pInputText
        type="text"
        inputmode="numeric"
        class="w-full"
        [id]="inputId()"
        [value]="value()"
        [disabled]="disabled()"
        maxlength="11"
        placeholder="___________"
        (input)="onInput($event)"
        (blur)="onTouched()"
      />
      @if (state() === 'valid') {
        <i
          class="tckn__icon tckn__icon--ok fa-solid fa-circle-check"
          [pTooltip]="'validation.tcknValid' | transloco"
          aria-hidden="true"
        ></i>
      } @else if (state() === 'invalid') {
        <i
          class="tckn__icon tckn__icon--err fa-solid fa-circle-xmark"
          [pTooltip]="'validation.tcknInvalid' | transloco"
          aria-hidden="true"
        ></i>
      }
    </div>
  `,
  styles: `
    .tckn {
      position: relative;
      display: block;
    }
    .tckn input {
      padding-right: 2.25rem;
    }
    .tckn__icon {
      position: absolute;
      right: 0.75rem;
      top: 50%;
      transform: translateY(-50%);
      font-size: 1rem;
    }
    .tckn__icon--ok {
      color: #16a34a;
    }
    .tckn__icon--err {
      color: #dc2626;
    }
  `,
})
export class TcknInputComponent implements ControlValueAccessor {
  readonly inputId = input<string>('');

  protected readonly value = signal('');
  protected readonly disabled = signal(false);

  /** 11 hane dolmadan ikon gosterilmez. */
  protected readonly state = computed<'none' | 'valid' | 'invalid'>(() => {
    const v = this.value();
    if (v.length !== 11) {
      return 'none';
    }
    return isValidTckn(v) ? 'valid' : 'invalid';
  });

  private onChange: (value: string | null) => void = () => undefined;
  protected onTouched: () => void = () => undefined;

  writeValue(value: string | null): void {
    this.value.set(value ?? '');
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
    const digits = inputEl.value.replace(/\D/g, '').slice(0, 11);
    this.value.set(digits);
    inputEl.value = digits;
    this.onChange(digits || null);
  }
}
