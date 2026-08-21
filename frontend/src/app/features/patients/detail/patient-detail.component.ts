import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  numberAttribute,
  signal,
} from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { TooltipModule } from 'primeng/tooltip';
import { TranslocoPipe } from '@jsverse/transloco';
import { Gender } from '../../../core/api/api.models';
import { MoneyPipe } from '../../../shared/pipes/money.pipe';
import { PatientDetailStore } from './patient-detail.store';
import { ConsentDialogComponent } from './consent/consent-dialog.component';

interface PatientTab {
  path: string;
  labelKey: string;
  icon: string;
}

/** Hasta karti kabugu: baslik seridi + 11 sekmelik router tab cubugu + child outlet. */
@Component({
  selector: 'app-patient-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
    ButtonModule,
    TooltipModule,
    TranslocoPipe,
    MoneyPipe,
    ConsentDialogComponent,
  ],
  providers: [PatientDetailStore],
  templateUrl: './patient-detail.component.html',
  styleUrl: './patient-detail.component.scss',
})
export class PatientDetailComponent {
  protected readonly store = inject(PatientDetailStore);
  private readonly router = inject(Router);

  /** Route parametresi (withComponentInputBinding). */
  readonly id = input.required({ transform: numberAttribute });

  protected readonly tabs: PatientTab[] = [
    { path: 'profile', labelKey: 'patients.tabs.profile', icon: 'fa-regular fa-id-card' },
    { path: 'treatment', labelKey: 'patients.tabs.treatment', icon: 'fa-solid fa-tooth' },
    { path: 'payment', labelKey: 'patients.tabs.payment', icon: 'fa-solid fa-coins' },
    { path: 'anamnesis', labelKey: 'patients.tabs.anamnesis', icon: 'fa-solid fa-notes-medical' },
    { path: 'notes', labelKey: 'patients.tabs.notes', icon: 'fa-regular fa-note-sticky' },
    {
      path: 'prescriptions',
      labelKey: 'patients.tabs.prescriptions',
      icon: 'fa-solid fa-prescription',
    },
    { path: 'images', labelKey: 'patients.tabs.images', icon: 'fa-regular fa-images' },
    { path: 'controls', labelKey: 'patients.tabs.controls', icon: 'fa-solid fa-rotate' },
    { path: 'laboratory', labelKey: 'patients.tabs.laboratory', icon: 'fa-solid fa-flask' },
    { path: 'epicrisis', labelKey: 'patients.tabs.epicrisis', icon: 'fa-solid fa-file-medical' },
    { path: 'reports', labelKey: 'patients.tabs.reports', icon: 'fa-solid fa-chart-line' },
  ];

  protected readonly initials = computed(() => {
    const p = this.store.patient();
    return p ? `${p.firstName[0] ?? ''}${p.lastName[0] ?? ''}`.toUpperCase() : '';
  });

  protected readonly age = computed<number | null>(() => {
    const birth = this.store.patient()?.birthDate;
    if (!birth) {
      return null;
    }
    const [y, m, d] = birth.split('-').map(Number);
    const today = new Date();
    let age = today.getFullYear() - y;
    const monthDiff = today.getMonth() + 1 - m;
    if (monthDiff < 0 || (monthDiff === 0 && today.getDate() < d)) {
      age -= 1;
    }
    return age;
  });

  protected readonly genderKey = computed(() => {
    const gender = this.store.patient()?.gender;
    return gender === Gender.Male ? 'gender.male' : gender === Gender.Female ? 'gender.female' : null;
  });

  /** "Onam Olustur" dialogu gorunurlugu. */
  protected readonly consentDialogVisible = signal(false);

  /** Kritik anamnez cevaplarinin tooltip metni (soru + varsa detay). */
  protected readonly criticalTooltip = computed(() =>
    this.store
      .criticalFlags()
      .map((f) => (f.textValue ? `${f.questionText}: ${f.textValue}` : f.questionText))
      .join('\n'),
  );

  constructor() {
    effect(() => this.store.load(this.id()));
  }

  protected goToCalendar(): void {
    const patient = this.store.patient();
    if (!patient) {
      return;
    }
    void this.router.navigate(['/app/calendar'], {
      queryParams: {
        patientId: patient.id,
        patientName: `${patient.firstName} ${patient.lastName}`,
      },
    });
  }
}
