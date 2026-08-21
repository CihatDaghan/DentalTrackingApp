import { inject, Injectable, signal } from '@angular/core';
import { PatientsApiService } from '../../../core/api/patients-api.service';
import { PatientDto } from '../../../core/api/api.models';
import { ClinicalApiService } from '../../../core/api/clinical-api.service';
import { CriticalFlagDto } from '../../../core/api/clinical-api.models';

/** Hasta karti kapsami: basliktaki ozet + sekmelerin paylastigi hasta verisi. */
@Injectable()
export class PatientDetailStore {
  private readonly api = inject(PatientsApiService);
  private readonly clinicalApi = inject(ClinicalApiService);

  readonly patient = signal<PatientDto | null>(null);
  readonly loading = signal(false);
  readonly notFound = signal(false);
  /** Kritik anamnez cevaplari (alerji vb.) — basliktaki kirmizi uyari rozeti buradan beslenir. */
  readonly criticalFlags = signal<CriticalFlagDto[]>([]);

  private currentId: number | null = null;

  load(id: number): void {
    if (this.currentId === id && this.patient()) {
      return;
    }
    this.currentId = id;
    this.loading.set(true);
    this.notFound.set(false);
    this.api.get(id).subscribe({
      next: (patient) => {
        this.patient.set(patient);
        this.loading.set(false);
      },
      error: () => {
        this.patient.set(null);
        this.notFound.set(true);
        this.loading.set(false);
      },
    });
    this.reloadCriticalFlags(id);
  }

  refresh(): void {
    if (this.currentId != null) {
      const id = this.currentId;
      this.currentId = null;
      this.load(id);
    }
  }

  /** Anamnez kaydedildikten sonra rozetin guncellenmesi icin cagrilir. */
  reloadCriticalFlags(id?: number): void {
    const patientId = id ?? this.currentId;
    if (patientId == null) {
      return;
    }
    this.clinicalApi.criticalFlags(patientId).subscribe({
      next: (flags) => this.criticalFlags.set(flags),
      error: () => this.criticalFlags.set([]),
    });
  }

  /** PUT yaniti sonrasi yerel gunceleme (yeniden cekmeden). */
  setPatient(patient: PatientDto): void {
    this.patient.set(patient);
    this.currentId = patient.id;
  }
}
