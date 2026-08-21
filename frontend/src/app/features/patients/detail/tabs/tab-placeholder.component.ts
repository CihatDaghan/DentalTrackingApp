import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { map } from 'rxjs';
import { TranslocoPipe } from '@jsverse/transloco';

/**
 * Henuz gelistirilmemis sekmeler icin sik bos-durum.
 * Route data: `{ labelKey, icon, phase }`.
 */
@Component({
  selector: 'app-tab-placeholder',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe],
  template: `
    @if (data(); as d) {
      <div class="dt-card placeholder">
        <span class="placeholder__icon">
          <i [class]="d.icon" aria-hidden="true"></i>
        </span>
        <h3 class="placeholder__title">{{ d.labelKey | transloco }}</h3>
        <p class="placeholder__text">
          {{ 'patients.placeholder.text' | transloco: { phase: d.phase } }}
        </p>
      </div>
    }
  `,
  styles: `
    .placeholder {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 0.5rem;
      padding: 4rem 2rem;
      text-align: center;
    }
    .placeholder__icon {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 64px;
      height: 64px;
      border-radius: 50%;
      background: #eff6ff;
      color: #3b82f6;
      font-size: 1.5rem;
      margin-bottom: 0.5rem;
    }
    .placeholder__title {
      margin: 0;
      font-size: 1.05rem;
      font-weight: 600;
      color: #334155;
    }
    .placeholder__text {
      margin: 0;
      color: #94a3b8;
      font-size: 0.9rem;
      max-width: 26rem;
    }
  `,
})
export class TabPlaceholderComponent {
  private readonly route = inject(ActivatedRoute);

  protected readonly data = toSignal(
    this.route.data.pipe(
      map((data) => ({
        labelKey: (data['labelKey'] as string) ?? '',
        icon: (data['icon'] as string) ?? 'fa-regular fa-folder-open',
        phase: (data['phase'] as string) ?? 'C',
      })),
    ),
  );
}
