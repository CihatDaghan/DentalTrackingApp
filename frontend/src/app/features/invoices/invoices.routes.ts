import { Routes } from '@angular/router';

/** e-Belge modulu: liste, sihirbaz, detay (hepsi lazy). */
export const INVOICES_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./invoice-list.component').then((m) => m.InvoiceListComponent),
    data: { titleKey: 'menu.invoices' },
    title: 'DentalTrack',
  },
  {
    // ':id' oncesinde tanimli olmali.
    path: 'new',
    loadComponent: () =>
      import('./invoice-wizard.component').then((m) => m.InvoiceWizardComponent),
    data: { titleKey: 'invoices.wizard.title' },
    title: 'DentalTrack',
  },
  {
    path: ':id',
    loadComponent: () =>
      import('./invoice-detail.component').then((m) => m.InvoiceDetailComponent),
    data: { titleKey: 'invoices.detailTitle' },
    title: 'DentalTrack',
  },
];
