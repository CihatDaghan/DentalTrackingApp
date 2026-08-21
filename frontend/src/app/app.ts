import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ToastModule } from 'primeng/toast';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { PrimeNG } from 'primeng/config';
import { TranslocoService } from '@jsverse/transloco';
import { PRIMENG_EN, PRIMENG_TR } from './core/i18n/primeng-translations';

@Component({
  selector: 'app-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, ToastModule, ConfirmDialogModule],
  template: `
    <router-outlet />
    <p-toast position="top-right" />
    <p-confirmdialog [style]="{ width: '26rem' }" />
  `,
})
export class App {
  constructor() {
    // PrimeNG bilesen metinleri (datepicker ay/gun adlari vb.) aktif dile baglanir.
    const primeng = inject(PrimeNG);
    const transloco = inject(TranslocoService);
    transloco.langChanges$
      .pipe(takeUntilDestroyed())
      .subscribe((lang) => primeng.setTranslation(lang === 'en' ? PRIMENG_EN : PRIMENG_TR));
  }
}
