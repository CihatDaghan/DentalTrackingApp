import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { permissionGuard } from './core/auth/permission.guard';
import { superAdminGuard } from './core/auth/super-admin.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () =>
      import('./layout/public-layout/public-layout.component').then(
        (m) => m.PublicLayoutComponent,
      ),
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./features/auth/login/login.component').then((m) => m.LoginComponent),
        title: 'DentalTrack',
      },
    ],
  },
  {
    path: 'app',
    canMatch: [authGuard],
    loadComponent: () =>
      import('./layout/main-layout/main-layout.component').then((m) => m.MainLayoutComponent),
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'dashboard' },
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./features/dashboard/dashboard.component').then((m) => m.DashboardComponent),
        data: { titleKey: 'menu.dashboard' },
        title: 'DentalTrack',
      },
      {
        path: 'calendar',
        loadComponent: () =>
          import('./features/calendar/calendar-page.component').then(
            (m) => m.CalendarPageComponent,
          ),
        data: { titleKey: 'menu.calendar' },
        title: 'DentalTrack',
      },
      {
        path: 'patients',
        loadChildren: () =>
          import('./features/patients/patients.routes').then((m) => m.PATIENTS_ROUTES),
      },
      {
        path: 'catalog',
        loadComponent: () =>
          import('./features/catalog/catalog-page.component').then((m) => m.CatalogPageComponent),
        data: { titleKey: 'menu.catalog' },
        title: 'DentalTrack',
      },
      {
        path: 'cash',
        loadComponent: () =>
          import('./features/cash/cash-page.component').then((m) => m.CashPageComponent),
        data: { titleKey: 'menu.cash' },
        title: 'DentalTrack',
      },
      {
        path: 'invoices',
        loadChildren: () =>
          import('./features/invoices/invoices.routes').then((m) => m.INVOICES_ROUTES),
      },
      {
        path: 'laboratory',
        loadComponent: () =>
          import('./features/laboratory/laboratory-page.component').then(
            (m) => m.LaboratoryPageComponent,
          ),
        data: { titleKey: 'menu.laboratory' },
        title: 'DentalTrack',
      },
      {
        path: 'inventory',
        loadComponent: () =>
          import('./features/inventory/inventory-page.component').then(
            (m) => m.InventoryPageComponent,
          ),
        data: { titleKey: 'menu.inventory' },
        title: 'DentalTrack',
      },
      {
        path: 'messaging',
        loadComponent: () =>
          import('./features/messaging/messaging-page.component').then(
            (m) => m.MessagingPageComponent,
          ),
        data: { titleKey: 'menu.messaging' },
        title: 'DentalTrack',
      },
      {
        path: 'reports',
        canMatch: [permissionGuard('report.view')],
        loadComponent: () =>
          import('./features/reports/reports-page.component').then((m) => m.ReportsPageComponent),
        data: { titleKey: 'menu.reports' },
        title: 'DentalTrack',
      },
      {
        path: 'settings',
        loadChildren: () =>
          import('./features/settings/settings.routes').then((m) => m.SETTINGS_ROUTES),
      },
      {
        // Dev onizleme: dis semasi — C asamasinda gercek tedavi ekranina evrilecek
        path: 'dev/tooth-chart',
        loadComponent: () =>
          import('./features/dev/tooth-chart-dev.page').then((m) => m.ToothChartDevPage),
        data: { titleKey: 'toothChart.title' },
        title: 'DentalTrack',
      },
    ],
  },
  {
    // Super admin paneli — ayri layout (sidebar yok), yalniz isSuperAdmin
    path: 'admin',
    canMatch: [superAdminGuard],
    loadComponent: () =>
      import('./features/admin/admin-layout.component').then((m) => m.AdminLayoutComponent),
    loadChildren: () => import('./features/admin/admin.routes').then((m) => m.ADMIN_ROUTES),
  },
  {
    // Public (auth'suz, token'li) sayfalar — hafif ayri chunk
    path: 'p',
    loadComponent: () =>
      import('./layout/public-layout/public-layout.component').then(
        (m) => m.PublicLayoutComponent,
      ),
    children: [
      {
        path: 'consent/:token',
        loadComponent: () =>
          import('./features/public/consent-sign/consent-sign-page.component').then(
            (m) => m.ConsentSignPageComponent,
          ),
        title: 'DentalTrack',
      },
      {
        // Saglayicidan donusteki sonuc sayfasi — daha ozel oldugu icin once tanimlanir.
        path: 'payment/:token/result',
        loadComponent: () =>
          import('./features/public/payment/payment-result-page.component').then(
            (m) => m.PublicPaymentResultPageComponent,
          ),
        title: 'DentalTrack',
      },
      {
        path: 'payment/:token',
        loadComponent: () =>
          import('./features/public/payment/payment-page.component').then(
            (m) => m.PublicPaymentPageComponent,
          ),
        title: 'DentalTrack',
      },
    ],
  },
  {
    // Gelistirme: "fake" odeme surucusunun hosted sayfa taklidi (auth'suz).
    // Gercek saglayicida bu adres bankanin 3D Secure sayfasidir.
    path: 'dev/fake-payment',
    loadComponent: () =>
      import('./features/dev/fake-payment.page').then((m) => m.FakePaymentPage),
    title: 'DentalTrack',
  },
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  { path: '**', redirectTo: 'login' },
];
