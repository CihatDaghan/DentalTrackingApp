import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Sayfa basligi + sag aksiyon alani (projeksiyon). */
@Component({
  selector: 'app-page-header',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page-header">
      <div class="page-header__text">
        <h2 class="page-header__title">{{ title() }}</h2>
        @if (subtitle()) {
          <p class="page-header__subtitle">{{ subtitle() }}</p>
        }
      </div>
      <div class="page-header__actions">
        <ng-content />
      </div>
    </div>
  `,
  styles: `
    .page-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 1rem;
      margin-bottom: 1.25rem;
      flex-wrap: wrap;
    }
    .page-header__title {
      margin: 0;
      font-size: 1.35rem;
      font-weight: 600;
      color: #0f172a;
    }
    .page-header__subtitle {
      margin: 0.15rem 0 0;
      color: #64748b;
      font-size: 0.85rem;
    }
    .page-header__actions {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      flex-wrap: wrap;
    }
  `,
})
export class PageHeaderComponent {
  readonly title = input.required<string>();
  readonly subtitle = input<string>('');
}
