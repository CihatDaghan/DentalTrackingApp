import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { ButtonModule } from 'primeng/button';

/**
 * Gelistirme sagrayicisinin ("fake") hosted odeme sayfasi taklidi.
 * Gercek saglayicida bu sayfa bankanin 3D Secure sayfasidir; burada yalniz
 * callback'i tetikleyerek odeme akisinin ucunu uca denenebilmesini saglar.
 * Yalnizca `fake` odeme surucusu kullanildiginda bu adrese yonlendirilir.
 */
@Component({
  selector: 'app-fake-payment-page',
  standalone: true,
  imports: [ButtonModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="mx-auto flex min-h-dvh max-w-md flex-col justify-center gap-4 p-6">
      <div class="rounded-2xl border border-amber-300 bg-amber-50 p-3 text-sm text-amber-800">
        <i class="fa-solid fa-triangle-exclamation mr-2"></i>
        Bu sayfa yalnızca geliştirme içindir — gerçek bir ödeme sayfası değildir.
      </div>

      <div class="rounded-2xl bg-white p-6 shadow-sm ring-1 ring-slate-200">
        <div class="mb-6 flex items-center gap-3">
          <span class="grid size-10 place-items-center rounded-xl bg-slate-900 text-white">
            <i class="fa-solid fa-building-columns"></i>
          </span>
          <div>
            <div class="font-semibold text-slate-900">Test Bankası</div>
            <div class="text-xs text-slate-500">Güvenli Ödeme Simülasyonu</div>
          </div>
        </div>

        @if (token()) {
          <dl class="mb-6 space-y-2 text-sm">
            <div class="flex justify-between">
              <dt class="text-slate-500">İşlem numarası</dt>
              <dd class="font-mono text-xs text-slate-700">{{ token() }}</dd>
            </div>
          </dl>

          <div class="flex flex-col gap-2">
            <p-button
              label="Ödemeyi Onayla"
              icon="fa-solid fa-check"
              styleClass="w-full"
              [disabled]="busy()"
              [loading]="busy()"
              (onClick)="complete()"
            />
            <p-button
              label="Vazgeç"
              severity="secondary"
              [text]="true"
              styleClass="w-full"
              [disabled]="busy()"
              (onClick)="cancel()"
            />
          </div>
        } @else {
          <p class="text-sm text-slate-600">Geçersiz ödeme bağlantısı.</p>
        }
      </div>
    </div>
  `,
})
export class FakePaymentPage {
  private readonly route = inject(ActivatedRoute);

  protected readonly busy = signal(false);
  private readonly params = toSignalParams(this.route);

  protected readonly token = computed(() => this.params().get('token'));

  protected complete(): void {
    const callback = this.params().get('callback');
    if (!callback) {
      return;
    }
    this.busy.set(true);
    // Sunucu callback'i dogrular (re-verify) ve sonuc sayfasina yonlendirir.
    const url = new URL(callback, window.location.origin);
    url.searchParams.set('token', this.token() ?? '');
    window.location.href = url.toString();
  }

  protected cancel(): void {
    window.history.back();
  }
}

function toSignalParams(route: ActivatedRoute) {
  const params = signal(new URLSearchParams(window.location.search));
  route.queryParams.subscribe(() => params.set(new URLSearchParams(window.location.search)));
  return params;
}
