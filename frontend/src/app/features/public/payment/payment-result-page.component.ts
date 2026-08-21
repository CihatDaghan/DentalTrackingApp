import {
  ChangeDetectionStrategy,
  Component,
  computed,
  DestroyRef,
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
  PublicPaymentStatusDto,
  PublicPaymentViewDto,
} from '../../../core/api/messaging-api.models';

/** Poll araligi ve toplam sure: 2 sn'de bir, en fazla 30 sn. */
const POLL_INTERVAL_MS = 2000;
const POLL_TIMEOUT_MS = 30000;

type ResultState = 'polling' | 'success' | 'failed' | 'pending' | 'gone' | 'error';

/**
 * Odeme donus sayfasi (/p/payment/:token/result — auth'suz).
 * Saglayicidan donuste durum ucu 2 sn araliklarla en fazla 30 sn poll'lanir:
 * Paid -> makbuz ozeti, Failed -> tekrar dene, hala bekliyorsa bilgilendirme.
 */
@Component({
  selector: 'app-public-payment-result-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, ButtonModule, TranslocoPipe],
  templateUrl: './payment-result-page.component.html',
  styleUrl: './payment.scss',
})
export class PublicPaymentResultPageComponent {
  private readonly api = inject(PaymentLinksApiService);
  private readonly destroyRef = inject(DestroyRef);

  readonly token = input.required<string>();

  protected readonly state = signal<ResultState>('polling');
  protected readonly status = signal<PublicPaymentStatusDto | null>(null);
  protected readonly view = signal<PublicPaymentViewDto | null>(null);
  protected readonly elapsedMs = signal(0);
  protected readonly attempts = signal(0);

  private timer: ReturnType<typeof setTimeout> | null = null;

  protected readonly progress = computed(() =>
    Math.min(100, Math.round((this.elapsedMs() / POLL_TIMEOUT_MS) * 100)),
  );

  protected readonly remainingSeconds = computed(() =>
    Math.max(0, Math.ceil((POLL_TIMEOUT_MS - this.elapsedMs()) / 1000)),
  );

  protected readonly amountText = computed(() => {
    const v = this.view();
    const paid = this.status()?.paidAmount;
    const amount = paid ?? v?.amount;
    if (amount == null) {
      return '';
    }
    return new Intl.NumberFormat('tr-TR', {
      style: 'currency',
      currency: v?.currencyCode || 'TRY',
      minimumFractionDigits: 2,
    }).format(amount);
  });

  protected readonly paidAtText = computed(() => {
    const paidAt = this.status()?.paidAtUtc;
    if (!paidAt) {
      return '—';
    }
    const iso = /Z$|[+-]\d{2}:\d{2}$/.test(paidAt) ? paidAt : paidAt + 'Z';
    return new Date(iso).toLocaleString('tr-TR');
  });

  constructor() {
    this.destroyRef.onDestroy(() => this.stop());

    effect(() => {
      const token = this.token();
      this.stop();
      this.state.set('polling');
      this.elapsedMs.set(0);
      this.attempts.set(0);
      // Makbuz ozeti icin klinik/tutar bilgisi (durumdan bagimsiz).
      this.api.publicView(token).subscribe({
        next: (view) => this.view.set(view),
        error: () => this.view.set(null),
      });
      this.poll(token);
    });
  }

  private stop(): void {
    if (this.timer) {
      clearTimeout(this.timer);
      this.timer = null;
    }
  }

  private poll(token: string): void {
    this.attempts.update((n) => n + 1);
    this.api.publicStatus(token).subscribe({
      next: (status) => {
        this.status.set(status);
        if (status.status === PaymentIntentStatus.Paid) {
          this.state.set('success');
          this.stop();
          return;
        }
        if (
          status.status === PaymentIntentStatus.Failed ||
          status.status === PaymentIntentStatus.Expired
        ) {
          this.state.set('failed');
          this.stop();
          return;
        }
        this.scheduleNext(token);
      },
      error: (error: unknown) => {
        const httpStatus = error instanceof HttpErrorResponse ? error.status : 0;
        if (httpStatus === 404 || httpStatus === 410) {
          this.state.set('gone');
          this.stop();
          return;
        }
        this.scheduleNext(token);
      },
    });
  }

  private scheduleNext(token: string): void {
    if (this.elapsedMs() + POLL_INTERVAL_MS > POLL_TIMEOUT_MS) {
      // 30 sn doldu: banka onayi gecikmis olabilir, kullaniciyi bilgilendir.
      this.state.set('pending');
      this.stop();
      return;
    }
    this.timer = setTimeout(() => {
      this.elapsedMs.update((ms) => ms + POLL_INTERVAL_MS);
      this.poll(token);
    }, POLL_INTERVAL_MS);
  }

  protected retry(): void {
    this.stop();
    this.state.set('polling');
    this.elapsedMs.set(0);
    this.attempts.set(0);
    this.poll(this.token());
  }
}
