import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  OnDestroy,
  output,
  viewChild,
} from '@angular/core';
import { environment } from '../../../../environments/environment';

interface TurnstileRenderOptions {
  sitekey: string;
  callback?: (token: string) => void;
  'expired-callback'?: () => void;
  'error-callback'?: () => void;
  theme?: 'light' | 'dark' | 'auto';
  language?: string;
}

interface TurnstileApi {
  render(container: HTMLElement, options: TurnstileRenderOptions): string;
  reset(widgetId?: string): void;
  remove(widgetId: string): void;
}

declare global {
  interface Window {
    turnstile?: TurnstileApi;
  }
}

const TURNSTILE_SCRIPT_URL = 'https://challenges.cloudflare.com/turnstile/v0/api.js?render=explicit';
let turnstileScriptPromise: Promise<void> | null = null;

function loadTurnstileScript(): Promise<void> {
  if (window.turnstile) {
    return Promise.resolve();
  }
  if (!turnstileScriptPromise) {
    turnstileScriptPromise = new Promise<void>((resolve, reject) => {
      const script = document.createElement('script');
      script.src = TURNSTILE_SCRIPT_URL;
      script.async = true;
      script.defer = true;
      script.onload = () => resolve();
      script.onerror = () => {
        turnstileScriptPromise = null;
        reject(new Error('Turnstile script yuklenemedi'));
      };
      document.head.appendChild(script);
    });
  }
  return turnstileScriptPromise;
}

/**
 * Cloudflare Turnstile YER TUTUCU bilesen.
 * `environment.turnstileSiteKey` bos ise hicbir sey render etmez ve `token` olarak null yayar;
 * anahtar tanimlaninca script'i yukleyip gercek widget'i cizer.
 */
@Component({
  selector: 'app-turnstile',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (siteKey) {
      <div #host class="turnstile-host"></div>
    }
  `,
})
export class TurnstileComponent implements AfterViewInit, OnDestroy {
  private readonly host = viewChild<ElementRef<HTMLDivElement>>('host');

  /** Gecerli dogrulama token'i; widget yoksa/suresi dolduysa null. */
  readonly token = output<string | null>();

  protected readonly siteKey = environment.turnstileSiteKey;
  private widgetId: string | null = null;

  ngAfterViewInit(): void {
    if (!this.siteKey) {
      this.token.emit(null);
      return;
    }
    loadTurnstileScript()
      .then(() => {
        const container = this.host()?.nativeElement;
        const turnstile = window.turnstile;
        if (!container || !turnstile) {
          this.token.emit(null);
          return;
        }
        this.widgetId = turnstile.render(container, {
          sitekey: this.siteKey,
          theme: 'light',
          callback: (token) => this.token.emit(token),
          'expired-callback': () => this.token.emit(null),
          'error-callback': () => this.token.emit(null),
        });
      })
      .catch(() => this.token.emit(null));
  }

  ngOnDestroy(): void {
    if (this.widgetId) {
      window.turnstile?.remove(this.widgetId);
      this.widgetId = null;
    }
  }
}
