import { Routes } from '@angular/router';

/** Super admin paneli alt route'lari (/admin kabugu altinda). */
export const ADMIN_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'tenants' },
  {
    path: 'tenants',
    loadComponent: () =>
      import('./tenants/admin-tenants.component').then((m) => m.AdminTenantsComponent),
    title: 'DentalTrack Admin',
  },
  {
    path: 'plans',
    loadComponent: () => import('./plans/admin-plans.component').then((m) => m.AdminPlansComponent),
    title: 'DentalTrack Admin',
  },
  {
    path: 'announcements',
    loadComponent: () =>
      import('./announcements/admin-announcements.component').then(
        (m) => m.AdminAnnouncementsComponent,
      ),
    title: 'DentalTrack Admin',
  },
  {
    path: 'integration-health',
    loadComponent: () =>
      import('./health/admin-integration-health.component').then(
        (m) => m.AdminIntegrationHealthComponent,
      ),
    title: 'DentalTrack Admin',
  },
];
