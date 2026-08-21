import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthStore } from '../../core/auth/auth.store';

/**
 * Kalici turuncu uyari bandi: super admin baska bir kiraci adina goruntulerken.
 * "Cik" super admin oturumuna doner. Token'in refresh'i yoktur; 15 dk sonunda
 * ilk 401'de oturum /login'e duser.
 */
@Component({
  selector: 'app-impersonation-banner',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe],
  template: `
    @if (authStore.impersonation(); as ctx) {
      <div class="imp" data-testid="impersonation-banner">
        <i class="fa-solid fa-user-secret" aria-hidden="true"></i>
        <span class="imp__text">
          {{ 'admin.impersonation.banner' | transloco: { tenant: ctx.tenantName } }}
          <span class="imp__email">({{ ctx.impersonatedUserEmail }})</span>
        </span>
        <button type="button" class="imp__exit" (click)="exit()" data-testid="impersonation-exit">
          <i class="fa-solid fa-right-from-bracket" aria-hidden="true"></i>
          {{ 'admin.impersonation.exit' | transloco }}
        </button>
      </div>
    }
  `,
  styles: `
    :host {
      display: block;
    }
    .imp {
      display: flex;
      align-items: center;
      gap: 0.6rem;
      padding: 0.5rem 1.25rem;
      background: #f97316;
      color: #ffffff;
      font-size: 0.85rem;
      font-weight: 500;
    }
    .imp__text {
      flex: 1 1 auto;
      min-width: 0;
    }
    .imp__email {
      opacity: 0.85;
      font-weight: 400;
    }
    .imp__exit {
      display: inline-flex;
      align-items: center;
      gap: 0.35rem;
      border: 1px solid rgba(255, 255, 255, 0.6);
      border-radius: 8px;
      background: rgba(255, 255, 255, 0.15);
      color: #ffffff;
      padding: 0.25rem 0.7rem;
      font-family: inherit;
      font-size: 0.8rem;
      font-weight: 600;
      cursor: pointer;

      &:hover {
        background: rgba(255, 255, 255, 0.28);
      }
    }
  `,
})
export class ImpersonationBannerComponent {
  protected readonly authStore = inject(AuthStore);

  protected exit(): void {
    this.authStore.stopImpersonation();
  }
}
