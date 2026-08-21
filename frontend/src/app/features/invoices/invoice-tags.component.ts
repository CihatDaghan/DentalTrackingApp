import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import {
  INVOICE_IN_FLIGHT_STATUSES,
  INVOICE_KIND_KEYS,
  INVOICE_STATUS_KEYS,
  InvoiceDocumentKind,
  InvoiceStatus,
} from '../../core/api/invoice-api.models';

interface Palette {
  bg: string;
  fg: string;
  strike?: boolean;
}

/** Belge tipi: e-Fatura mavi / e-Arsiv mor / e-SMM turuncu. */
const KIND_PALETTE: Record<number, Palette> = {
  [InvoiceDocumentKind.EFatura]: { bg: '#dbeafe', fg: '#1d4ed8' },
  [InvoiceDocumentKind.EArsiv]: { bg: '#ede9fe', fg: '#6d28d9' },
  [InvoiceDocumentKind.ESmm]: { bg: '#ffedd5', fg: '#c2410c' },
};

/**
 * Durum rozeti paleti: taslak gri, UBL hazir mavi, yolda sari (nabizli),
 * kabul yesil, hata/red kirmizi, iptal ustu cizili.
 */
const STATUS_PALETTE: Record<number, Palette> = {
  [InvoiceStatus.Draft]: { bg: '#f1f5f9', fg: '#475569' },
  [InvoiceStatus.UblGenerated]: { bg: '#dbeafe', fg: '#1d4ed8' },
  [InvoiceStatus.Queued]: { bg: '#fef3c7', fg: '#b45309' },
  [InvoiceStatus.SentToIntegrator]: { bg: '#fef3c7', fg: '#b45309' },
  [InvoiceStatus.GibProcessing]: { bg: '#fef3c7', fg: '#b45309' },
  [InvoiceStatus.Succeeded]: { bg: '#dcfce7', fg: '#15803d' },
  [InvoiceStatus.GibRejected]: { bg: '#fee2e2', fg: '#b91c1c' },
  [InvoiceStatus.BuyerRejected]: { bg: '#fee2e2', fg: '#b91c1c' },
  [InvoiceStatus.Error]: { bg: '#fee2e2', fg: '#b91c1c' },
  [InvoiceStatus.ManualReview]: { bg: '#fee2e2', fg: '#b91c1c' },
  [InvoiceStatus.Cancelled]: { bg: '#f1f5f9', fg: '#64748b', strike: true },
};

/** Senaryo (typeCode) rozeti — UBL tip kodlari sabit, ceviri anahtari kucuk harf. */
const SCENARIO_PALETTE: Record<string, Palette> = {
  SATIS: { bg: '#e0f2fe', fg: '#0369a1' },
  IADE: { bg: '#ffe4e6', fg: '#be123c' },
  TEVKIFAT: { bg: '#fae8ff', fg: '#a21caf' },
  ISTISNA: { bg: '#ccfbf1', fg: '#0f766e' },
};

const BASE_TAG = `
  .e-tag {
    display: inline-block;
    padding: 0.2rem 0.6rem;
    border-radius: 999px;
    font-size: 0.75rem;
    font-weight: 600;
    white-space: nowrap;
    line-height: 1.2;
  }
`;

/** Belge tipi rozeti (e-Fatura / e-Arsiv / e-SMM). */
@Component({
  selector: 'app-invoice-kind-tag',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe],
  template: `
    <span class="e-tag" [style.background]="palette().bg" [style.color]="palette().fg">
      {{ 'invoices.kind.' + keySuffix() | transloco }}
    </span>
  `,
  styles: BASE_TAG,
})
export class InvoiceKindTagComponent {
  readonly value = input.required<InvoiceDocumentKind>();

  protected readonly palette = computed(
    () => KIND_PALETTE[this.value()] ?? { bg: '#f1f5f9', fg: '#475569' },
  );
  protected readonly keySuffix = computed(() => INVOICE_KIND_KEYS[this.value()] ?? 'unknown');
}

/** Senaryo rozeti (SATIS / IADE / TEVKIFAT / ISTISNA). */
@Component({
  selector: 'app-invoice-scenario-tag',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="e-tag" [style.background]="palette().bg" [style.color]="palette().fg">
      {{ value() }}
    </span>
  `,
  styles: BASE_TAG,
})
export class InvoiceScenarioTagComponent {
  readonly value = input.required<string>();

  protected readonly palette = computed(
    () => SCENARIO_PALETTE[this.value()] ?? { bg: '#f1f5f9', fg: '#475569' },
  );
}

/** Durum rozeti — "yolda" durumlarda nabiz animasyonlu nokta esligi. */
@Component({
  selector: 'app-invoice-status-tag',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe],
  template: `
    <span
      class="e-tag e-tag--status"
      [style.background]="palette().bg"
      [style.color]="palette().fg"
      [style.text-decoration]="palette().strike ? 'line-through' : 'none'"
      [attr.data-status]="value()"
    >
      @if (inFlight()) {
        <span class="e-tag__pulse" [style.background]="palette().fg"></span>
      }
      {{ 'invoices.status.' + keySuffix() | transloco }}
    </span>
  `,
  styles: `
    ${BASE_TAG}
    .e-tag--status {
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
    }
    .e-tag__pulse {
      width: 0.45rem;
      height: 0.45rem;
      border-radius: 999px;
      animation: e-tag-pulse 1.2s ease-in-out infinite;
    }
    @keyframes e-tag-pulse {
      0%,
      100% {
        opacity: 1;
        transform: scale(1);
      }
      50% {
        opacity: 0.35;
        transform: scale(0.6);
      }
    }
    @media (prefers-reduced-motion: reduce) {
      .e-tag__pulse {
        animation: none;
      }
    }
  `,
})
export class InvoiceStatusTagComponent {
  readonly value = input.required<InvoiceStatus>();

  protected readonly palette = computed(
    () => STATUS_PALETTE[this.value()] ?? { bg: '#f1f5f9', fg: '#475569' },
  );
  protected readonly keySuffix = computed(() => INVOICE_STATUS_KEYS[this.value()] ?? 'unknown');
  protected readonly inFlight = computed(() =>
    INVOICE_IN_FLIGHT_STATUSES.includes(this.value()),
  );
}
