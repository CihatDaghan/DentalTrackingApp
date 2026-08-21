import { ChangeDetectionStrategy, Component, inject, signal, viewChild } from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { MessageService } from 'primeng/api';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { PatientsApiService } from '../../../../core/api/patients-api.service';
import { HasPendingChanges } from '../../../../core/guards/pending-changes.guard';
import { HasPermissionDirective } from '../../../../core/auth/has-permission.directive';
import { PatientFormComponent } from '../../patient-form/patient-form.component';
import { PatientDetailStore } from '../patient-detail.store';

/** Profil sekmesi: tam demografi formu; kirli formda route cikisi onaya baglidir. */
@Component({
  selector: 'app-profile-tab',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ButtonModule, TranslocoPipe, HasPermissionDirective, PatientFormComponent],
  template: `
    @if (store.patient(); as patient) {
      <div class="dt-card p-5">
        <app-patient-form #patientForm [patient]="patient" />
        <ng-container *hasPermission="'patient.update'">
          <div class="flex justify-end gap-2 pt-5">
            <p-button
              [label]="'common.discard' | transloco"
              severity="secondary"
              [outlined]="true"
              (onClick)="discard()"
            />
            <p-button
              [label]="'common.save' | transloco"
              data-testid="profile-save-btn"
              [loading]="saving()"
              (onClick)="save()"
            />
          </div>
        </ng-container>
      </div>
    }
  `,
})
export class ProfileTabComponent implements HasPendingChanges {
  protected readonly store = inject(PatientDetailStore);
  private readonly patientsApi = inject(PatientsApiService);
  private readonly messageService = inject(MessageService);
  private readonly transloco = inject(TranslocoService);

  protected readonly patientForm = viewChild<PatientFormComponent>('patientForm');
  protected readonly saving = signal(false);

  hasPendingChanges(): boolean {
    return this.patientForm()?.hasPendingChanges() ?? false;
  }

  protected discard(): void {
    const patient = this.store.patient();
    const form = this.patientForm();
    if (patient && form) {
      form.patchFrom(patient);
    }
  }

  protected save(): void {
    const patient = this.store.patient();
    const form = this.patientForm();
    if (!patient || !form) {
      return;
    }
    const summary = form.validationSummary();
    if (summary) {
      this.messageService.add({ severity: 'warn', summary, life: 4000 });
      return;
    }
    this.saving.set(true);
    this.patientsApi.update(patient.id, form.toRequest()).subscribe({
      next: (updated) => {
        this.saving.set(false);
        this.store.setPatient(updated);
        form.patchFrom(updated);
        this.messageService.add({
          severity: 'success',
          summary: this.transloco.translate('patients.updateSuccess'),
          life: 3000,
        });
      },
      error: () => this.saving.set(false),
    });
  }
}
