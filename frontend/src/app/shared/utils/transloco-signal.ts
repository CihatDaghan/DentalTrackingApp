import { inject, Signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslocoService } from '@jsverse/transloco';

/**
 * Ceviriler yuklendiginde ve aktif dil degistiginde tetiklenen sinyal.
 *
 * `computed()` icinde `transloco.translate(...)` ile kurulan secenek listeleri
 * (p-select/p-selectbutton `[options]`) bu sinyali okumalidir: aksi halde
 * bilesen ceviriler inmeden once render edilirse computed ham anahtarlari
 * ("inventory.unit.piece") kalici olarak onbellege alir ve bir daha yenilenmez.
 *
 * Kullanim:
 * ```ts
 * private readonly translation = injectTranslationSignal();
 * protected readonly options = computed(() => {
 *   this.translation();
 *   return KEYS.map((k) => this.transloco.translate('ns.' + k));
 * });
 * ```
 */
export function injectTranslationSignal(): Signal<unknown> {
  const transloco = inject(TranslocoService);
  return toSignal(transloco.selectTranslation(), { initialValue: undefined });
}
