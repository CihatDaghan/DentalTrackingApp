import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

import { ToothChartLegendItem } from './tooth-chart.models';

/**
 * Dis semasi lejanti: renk -> tedavi kategorisi eslemesi (katalogdan gelir)
 * + sabit katman/isaret stili orneklemeleri.
 */
@Component({
  selector: 'app-tooth-chart-legend',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe],
  template: `
    <div class="legend">
      @if (items().length) {
        <div class="legend__group">
          @for (item of items(); track item.color + (item.label ?? item.labelKey ?? '')) {
            <span class="legend__chip">
              <span class="legend__swatch" [style.background]="item.color"></span>
              {{ item.labelKey ? (item.labelKey | transloco) : item.label }}
            </span>
          }
        </div>
      }
      <div class="legend__group legend__group--styles">
        <span class="legend__chip">
          <svg viewBox="0 0 18 18" class="legend__icon" aria-hidden="true">
            <defs>
              <pattern
                id="tcl-hatch"
                patternUnits="userSpaceOnUse"
                width="5"
                height="5"
                patternTransform="rotate(45)"
              >
                <line x1="0" y1="0" x2="0" y2="5" stroke="#64748b" stroke-width="1.8" />
              </pattern>
            </defs>
            <rect x="1.5" y="1.5" width="15" height="15" rx="3" fill="url(#tcl-hatch)" stroke="#64748b" />
          </svg>
          {{ 'toothChart.legend.diagnosis' | transloco }}
        </span>
        <span class="legend__chip">
          <svg viewBox="0 0 18 18" class="legend__icon" aria-hidden="true">
            <rect
              x="1.5"
              y="1.5"
              width="15"
              height="15"
              rx="3"
              fill="#64748b"
              fill-opacity="0.45"
              stroke="#64748b"
              stroke-width="1.6"
              stroke-dasharray="3.5 2.5"
            />
          </svg>
          {{ 'toothChart.legend.plan' | transloco }}
        </span>
        <span class="legend__chip">
          <svg viewBox="0 0 18 18" class="legend__icon" aria-hidden="true">
            <rect x="1.5" y="1.5" width="15" height="15" rx="3" fill="#64748b" />
          </svg>
          {{ 'toothChart.legend.treatment' | transloco }}
        </span>
        <span class="legend__chip">
          <svg viewBox="0 0 18 18" class="legend__icon" aria-hidden="true">
            <rect x="1.5" y="1.5" width="15" height="15" rx="3" fill="#f1f5f9" stroke="#cbd5e1" />
            <path d="M 5 5 L 13 13 M 13 5 L 5 13" stroke="#94a3b8" stroke-width="2" stroke-linecap="round" />
          </svg>
          {{ 'toothChart.legend.missing' | transloco }}
        </span>
        <span class="legend__chip">
          <svg viewBox="0 0 18 18" class="legend__icon" aria-hidden="true">
            <path d="M 6 2 C 5.5 7 6 12 8 16 L 10 16 C 12 12 12.5 7 12 2 C 10 0.8 8 0.8 6 2 Z" fill="#ef4444" />
          </svg>
          {{ 'toothChart.legend.rootCanal' | transloco }}
        </span>
        <span class="legend__chip">
          <svg viewBox="0 0 18 18" class="legend__icon" aria-hidden="true">
            <path d="M 6 1.5 L 12 1.5 L 11 8 C 10.6 12 10 15 9 16.5 C 8 15 7.4 12 7 8 Z" fill="#10b981" />
            <path d="M 6.8 5 L 11.4 4 M 7.2 8.5 L 11 7.5 M 7.7 12 L 10.5 11" stroke="#ffffff" stroke-width="1.1" />
          </svg>
          {{ 'toothChart.legend.implant' | transloco }}
        </span>
      </div>
    </div>
  `,
  styles: `
    :host {
      display: block;
    }

    .legend {
      display: flex;
      flex-direction: column;
      gap: 0.6rem;
    }

    .legend__group {
      display: flex;
      flex-wrap: wrap;
      gap: 0.4rem 1.1rem;
      align-items: center;
    }

    .legend__group--styles {
      padding-top: 0.6rem;
      border-top: 1px dashed #e2e8f0;
    }

    .legend__chip {
      display: inline-flex;
      align-items: center;
      gap: 0.4rem;
      font-size: 0.8rem;
      color: #475569;
    }

    .legend__swatch {
      width: 14px;
      height: 14px;
      border-radius: 4px;
      flex: none;
    }

    .legend__icon {
      width: 16px;
      height: 16px;
      flex: none;
    }
  `,
})
export class ToothChartLegendComponent {
  /** Renk -> kategori eslemesi (gercek uygulamada tedavi katalogundan gelir). */
  readonly items = input<ToothChartLegendItem[]>([]);
}
