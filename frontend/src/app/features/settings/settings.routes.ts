import { Routes } from '@angular/router';
import { permissionGuard } from '../../core/auth/permission.guard';

/**
 * Klinik ayarlari alt route'lari. Mevcut `/app/settings/consent-templates`
 * derin baglantisi bu kabugun altinda calismaya devam eder.
 */
export const SETTINGS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./settings-page.component').then((m) => m.SettingsPageComponent),
    data: { titleKey: 'menu.settings' },
    title: 'DentalTrack',
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'clinic' },
      {
        path: 'clinic',
        loadComponent: () =>
          import('./clinic/clinic-settings.component').then((m) => m.ClinicSettingsComponent),
        data: { titleKey: 'menu.settings' },
      },
      {
        path: 'working-hours',
        loadComponent: () =>
          import('./working-hours/working-hours-settings.component').then(
            (m) => m.WorkingHoursSettingsComponent,
          ),
        data: { titleKey: 'menu.settings' },
      },
      {
        path: 'staff',
        canMatch: [permissionGuard('settings.staff')],
        loadComponent: () =>
          import('./staff/staff-settings.component').then((m) => m.StaffSettingsComponent),
        data: { titleKey: 'menu.settings' },
      },
      {
        path: 'roles',
        canMatch: [permissionGuard('settings.staff')],
        loadComponent: () =>
          import('./roles/permission-matrix.component').then((m) => m.PermissionMatrixComponent),
        data: { titleKey: 'menu.settings' },
      },
      {
        path: 'integrations',
        canMatch: [permissionGuard('settings.integrations')],
        loadComponent: () =>
          import('./integrations/integrations-settings.component').then(
            (m) => m.IntegrationsSettingsComponent,
          ),
        data: { titleKey: 'menu.settings' },
      },
      {
        path: 'consent-templates',
        loadComponent: () =>
          import('./consent-templates/consent-templates-page.component').then(
            (m) => m.ConsentTemplatesPageComponent,
          ),
        data: { titleKey: 'menu.consentTemplates' },
      },
    ],
  },
];
