import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { ButtonModule } from 'primeng/button';
import { SelectModule } from 'primeng/select';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AuthStore } from '../../core/auth/auth.store';

/**
 * Super admin kabugu (/admin): sidebar yok, sade koyu ust bar.
 * Yalniz `isSuperAdmin` kullanicilar erisir (superAdminGuard).
 */
@Component({
  selector: 'app-admin-layout',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
    ButtonModule,
    SelectModule,
    TranslocoPipe,
  ],
  template: `
    <div class="admin">
      <header class="admin__bar">
        <span class="admin__brand">
          <i class="fa-solid fa-tooth" aria-hidden="true"></i>
          DentalTrack
          <span class="admin__badge">{{ 'admin.title' | transloco }}</span>
        </span>

        <nav class="admin__nav">
          @for (tab of tabs; track tab.path) {
            <a
              class="admin__nav-item"
              [routerLink]="tab.path"
              routerLinkActive="admin__nav-item--active"
              [attr.data-testid]="'admin-nav-' + tab.path"
            >
              <i [class]="tab.icon" aria-hidden="true"></i>
              <span>{{ 'admin.nav.' + tab.labelKey | transloco }}</span>
            </a>
          }
        </nav>

        <div class="admin__right">
          <p-select
            size="small"
            [options]="languages"
            optionLabel="label"
            optionValue="value"
            [ngModel]="activeLang()"
            (onChange)="setLang($event.value)"
          />
          <span class="admin__user">{{ authStore.fullName() }}</span>
          <p-button
            severity="secondary"
            [text]="true"
            size="small"
            icon="pi pi-sign-out"
            [label]="'common.logout' | transloco"
            (onClick)="logout()"
            data-testid="admin-logout"
          />
        </div>
      </header>

      <main class="admin__content">
        <router-outlet />
      </main>
    </div>
  `,
  styles: `
    .admin {
      display: flex;
      flex-direction: column;
      height: 100vh;
      overflow: hidden;
      background: #f8fafc;
    }
    .admin__bar {
      display: flex;
      align-items: center;
      gap: 1.5rem;
      height: 58px;
      padding: 0 1.25rem;
      background: #0f172a;
      color: #e2e8f0;
    }
    .admin__brand {
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
      font-weight: 600;
      color: #ffffff;
    }
    .admin__badge {
      padding: 0.1rem 0.5rem;
      border-radius: 999px;
      background: #1d4ed8;
      color: #ffffff;
      font-size: 0.68rem;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }
    .admin__nav {
      display: flex;
      align-items: center;
      gap: 0.25rem;
      flex: 1 1 auto;
    }
    .admin__nav-item {
      display: inline-flex;
      align-items: center;
      gap: 0.45rem;
      padding: 0.4rem 0.75rem;
      border-radius: 8px;
      color: #cbd5e1;
      font-size: 0.85rem;
      text-decoration: none;

      &:hover {
        background: rgba(255, 255, 255, 0.08);
        color: #ffffff;
      }

      &--active {
        background: #1e293b;
        color: #ffffff;
        font-weight: 600;
      }
    }
    .admin__right {
      display: flex;
      align-items: center;
      gap: 0.75rem;
    }
    .admin__user {
      font-size: 0.85rem;
      color: #cbd5e1;
    }
    .admin__content {
      flex: 1 1 auto;
      overflow: auto;
      padding: 1.5rem;
    }
  `,
})
export class AdminLayoutComponent {
  protected readonly authStore = inject(AuthStore);
  private readonly transloco = inject(TranslocoService);

  protected readonly tabs = [
    { path: 'tenants', labelKey: 'tenants', icon: 'fa-solid fa-building' },
    { path: 'plans', labelKey: 'plans', icon: 'fa-solid fa-layer-group' },
    { path: 'announcements', labelKey: 'announcements', icon: 'fa-solid fa-bullhorn' },
    { path: 'integration-health', labelKey: 'integrationHealth', icon: 'fa-solid fa-heart-pulse' },
  ];

  protected readonly languages = [
    { label: 'TR', value: 'tr' },
    { label: 'EN', value: 'en' },
  ];

  protected readonly activeLang = toSignal(this.transloco.langChanges$, {
    initialValue: this.transloco.getActiveLang(),
  });

  protected readonly userEmail = computed(() => this.authStore.user()?.email ?? '');

  protected setLang(lang: string): void {
    this.transloco.setActiveLang(lang);
  }

  protected logout(): void {
    this.authStore.logout();
  }
}
