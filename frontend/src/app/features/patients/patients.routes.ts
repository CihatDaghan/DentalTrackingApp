import { Routes } from '@angular/router';
import { pendingChangesGuard } from '../../core/guards/pending-changes.guard';
import { TabPlaceholderComponent } from './detail/tabs/tab-placeholder.component';

/** Yer tutucu sekme route'u uretici (E asamasinda gercek iceriklerle degisecek). */
function placeholderTab(path: string, labelKey: string, icon: string, phase: string) {
  return {
    path,
    component: TabPlaceholderComponent,
    data: { titleKey: 'menu.patients', labelKey, icon, phase },
  };
}

export const PATIENTS_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./list/patient-list.component').then((m) => m.PatientListComponent),
    data: { titleKey: 'menu.patients' },
    title: 'DentalTrack',
  },
  {
    path: ':id',
    loadComponent: () =>
      import('./detail/patient-detail.component').then((m) => m.PatientDetailComponent),
    data: { titleKey: 'menu.patients' },
    title: 'DentalTrack',
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'profile' },
      {
        path: 'profile',
        loadComponent: () =>
          import('./detail/tabs/profile-tab.component').then((m) => m.ProfileTabComponent),
        canDeactivate: [pendingChangesGuard],
        data: { titleKey: 'menu.patients' },
      },
      {
        path: 'controls',
        loadComponent: () =>
          import('./detail/tabs/controls-tab.component').then((m) => m.ControlsTabComponent),
        data: { titleKey: 'menu.patients' },
      },
      {
        path: 'treatment',
        loadComponent: () =>
          import('./detail/tabs/treatment-tab/treatment-tab.component').then(
            (m) => m.TreatmentTabComponent,
          ),
        data: { titleKey: 'menu.patients' },
      },
      {
        path: 'payment',
        loadComponent: () =>
          import('./detail/tabs/payment-tab/payment-tab.component').then(
            (m) => m.PaymentTabComponent,
          ),
        data: { titleKey: 'menu.patients' },
      },
      {
        path: 'anamnesis',
        loadComponent: () =>
          import('./detail/tabs/anamnesis-tab/anamnesis-tab.component').then(
            (m) => m.AnamnesisTabComponent,
          ),
        data: { titleKey: 'menu.patients' },
      },
      {
        path: 'notes',
        loadComponent: () =>
          import('./detail/tabs/notes-tab/notes-tab.component').then((m) => m.NotesTabComponent),
        data: { titleKey: 'menu.patients' },
      },
      {
        path: 'images',
        loadComponent: () =>
          import('./detail/tabs/images-tab/images-tab.component').then(
            (m) => m.ImagesTabComponent,
          ),
        data: { titleKey: 'menu.patients' },
      },
      {
        path: 'prescriptions',
        loadComponent: () =>
          import('./detail/tabs/prescriptions-tab/prescriptions-tab.component').then(
            (m) => m.PrescriptionsTabComponent,
          ),
        data: { titleKey: 'menu.patients' },
      },
      {
        path: 'laboratory',
        loadComponent: () =>
          import('./detail/tabs/laboratory-tab/laboratory-tab.component').then(
            (m) => m.LaboratoryTabComponent,
          ),
        data: { titleKey: 'menu.patients' },
      },
      {
        path: 'epicrisis',
        loadComponent: () =>
          import('./detail/tabs/epicrisis-tab/epicrisis-tab.component').then(
            (m) => m.EpicrisisTabComponent,
          ),
        data: { titleKey: 'menu.patients' },
      },
      {
        path: 'reports',
        loadComponent: () =>
          import('./detail/tabs/reports-tab/reports-tab.component').then(
            (m) => m.ReportsTabComponent,
          ),
        data: { titleKey: 'menu.patients' },
      },
    ],
  },
];
