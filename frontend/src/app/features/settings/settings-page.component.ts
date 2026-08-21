import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { TranslocoPipe } from '@jsverse/transloco';
import { AuthStore } from '../../core/auth/auth.store';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';

interface SettingsTab {
  path: string;
  labelKey: string;
  icon: string;
  /** Bos ise herkese acik. */
  permission: string;
}

/** Klinik ayarlari kabugu: alt sekmeler route'ludur (derin baglanti korunur). */
@Component({
  selector: 'app-settings-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, RouterLinkActive, RouterOutlet, TranslocoPipe, PageHeaderComponent],
  template: `
    <app-page-header [title]="'settings.title' | transloco" />

    <nav class="settings-tabs" data-testid="settings-tabs">
      @for (tab of visibleTabs(); track tab.path) {
        <a
          class="settings-tab"
          [routerLink]="tab.path"
          routerLinkActive="settings-tab--active"
          [attr.data-testid]="'settings-tab-' + tab.path"
        >
          <i [class]="tab.icon" aria-hidden="true"></i>
          <span>{{ 'settings.tabs.' + tab.labelKey | transloco }}</span>
        </a>
      }
    </nav>

    <router-outlet />
  `,
  styles: `
    .settings-tabs {
      display: flex;
      gap: 0.25rem;
      border-bottom: 1px solid #e2e8f0;
      margin-bottom: 1rem;
      overflow-x: auto;
    }
    .settings-tab {
      display: inline-flex;
      align-items: center;
      gap: 0.45rem;
      padding: 0.6rem 0.9rem;
      border-bottom: 2px solid transparent;
      color: #64748b;
      font-size: 0.875rem;
      text-decoration: none;
      white-space: nowrap;

      i {
        color: #94a3b8;
      }

      &:hover {
        color: #334155;
      }

      &--active {
        color: #1d4ed8;
        font-weight: 600;
        border-bottom-color: #3b82f6;

        i {
          color: #3b82f6;
        }
      }
    }
  `,
})
export class SettingsPageComponent {
  private readonly authStore = inject(AuthStore);

  private readonly tabs: SettingsTab[] = [
    { path: 'clinic', labelKey: 'clinic', icon: 'fa-solid fa-hospital', permission: 'settings.view' },
    {
      path: 'working-hours',
      labelKey: 'workingHours',
      icon: 'fa-regular fa-clock',
      permission: 'settings.view',
    },
    { path: 'staff', labelKey: 'staff', icon: 'fa-solid fa-users', permission: 'settings.staff' },
    {
      path: 'roles',
      labelKey: 'roles',
      icon: 'fa-solid fa-user-shield',
      permission: 'settings.staff',
    },
    {
      path: 'integrations',
      labelKey: 'integrations',
      icon: 'fa-solid fa-plug',
      permission: 'settings.integrations',
    },
    {
      path: 'consent-templates',
      labelKey: 'consentTemplates',
      icon: 'fa-solid fa-file-signature',
      permission: 'consent.read',
    },
  ];

  protected visibleTabs(): SettingsTab[] {
    return this.tabs.filter((t) => !t.permission || this.authStore.hasPermission(t.permission));
  }
}
