import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { TranslocoPipe } from '@jsverse/transloco';
import { PaymentLinksApiService } from '../../../core/api/payment-links-api.service';
import {
  PaymentIntentStatus,
  PublicPaymentViewDto,
} from '../../../core/api/messaging-api.models';

type PageState = 'loading' | 'view' | 'paid' | 'gone' | 'error';

/**
 * Public odeme sayfasi (/p/payment/:token — auth'suz, mobile-first).
 * Kart bilgisi burada ISTENMEZ: "Ode" butonu saglayicinin barindirdigi 3DS sayfasina yonlendirir.
 */
@Component({
  selector: 'app-public-payment-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, ButtonModule, TranslocoPipe],
  templateUrl: './payment-page.component.html',
  styleUrl: './payment.scss',
})
export class PublicPaymentPageComponent {
  private readonly api = inject(PaymentLinksApiService);

  /** Route parametresi (withComponentInputBinding). */
  readonly token = input.required<string>();

  protected readonly state = signal<PageState>('loading');
  protected readonly view = signal<PublicPaymentViewDto | null>(null);

  protected readonly canPay = computed(() => {
    const v = this.view();
    return (
      !!v?.payUrl &&
      (v.status === PaymentIntentStatus.Created || v.status === PaymentIntentStatus.LinkSent)
    );
  });

  protected readonly amountText = computed(() => {
    const v = this.view();
    if (!v) {
      return '';
    }
    return new Intl.NumberFormat('tr-TR', {
      style: 'currency',
      currency: v.currencyCode || 'TRY',
      minimumFractionDigits: 2,
    }).format(v.amount);
  });

  constructor() {
    effect(() => {
      const token = this.token();
      this.state.set('loading');
      this.api.publicView(token).subscribe({
        next: (view) => {
          this.view.set(view);
          if (view.status === PaymentIntentStatus.Paid) {
            this.state.set('paid');
          } else if (
            view.status === PaymentIntentStatus.Expired ||
            view.status === PaymentIntentStatus.Refunded
          ) {
            this.state.set('gone');
          } else {
            this.state.set('view');
          }
        },
        error: (error: unknown) => {
          const status = error instanceof HttpErrorResponse ? error.status : 0;
          this.state.set(status === 404 || status === 410 ? 'gone' : 'error');
        },
      });
    });
  }

  /** Saglayicinin hosted 3DS sayfasina gecis — kart bilgisi orada alinir. */
  protected pay(): void {
    const url = this.view()?.payUrl;
    if (url) {
      window.location.href = url;
    }
  }
}
