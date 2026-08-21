import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  signal,
  untracked,
} from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';
import { TranslocoPipe } from '@jsverse/transloco';
import { LaboratoryApiService } from '../../../../../core/api/laboratory-api.service';
import { LabCaseDto, LabCaseHistoryDto } from '../../../../../core/api/laboratory-api.models';
import { HasPermissionDirective } from '../../../../../core/auth/has-permission.directive';
import { StatusTagComponent } from '../../../../../shared/components/status-tag/status-tag.component';
import { MoneyPipe } from '../../../../../shared/pipes/money.pipe';
import { TrDatePipe } from '../../../../../shared/pipes/tr-date.pipe';
import { LabCaseDialogComponent } from '../../../../laboratory/lab-case-dialog.component';
import { LabStatusDialogComponent } from '../../../../laboratory/lab-status-dialog.component';
import { PatientDetailStore } from '../../patient-detail.store';

/** Laboratuvar sekmesi: bu hastanin lab isleri + yeni vaka/durum degistirme. */
@Component({
  selector: 'app-laboratory-tab',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ButtonModule,
    TableModule,
    TooltipModule,
    TranslocoPipe,
    HasPermissionDirective,
    StatusTagComponent,
    MoneyPipe,
    TrDatePipe,
    LabCaseDialogComponent,
    LabStatusDialogComponent,
  ],
  templateUrl: './laboratory-tab.component.html',
  styles: `
    /* Gecikmis vaka satiri — hover'da da kirmizi kalir. */
    :host ::ng-deep tr.lab-row--overdue > td {
      background: #fef2f2;
    }
  `,
})
export class LaboratoryTabComponent {
  private readonly store = inject(PatientDetailStore);
  private readonly api = inject(LaboratoryApiService);

  protected readonly cases = signal<LabCaseDto[]>([]);
  protected readonly loading = signal(false);
  protected readonly patientId = signal<number | null>(null);

  protected readonly caseDialogVisible = signal(false);
  protected readonly editingCase = signal<LabCaseDto | null>(null);
  protected readonly statusDialogVisible = signal(false);
  protected readonly statusTarget = signal<LabCaseDto | null>(null);

  /** Genisletilen satirin durum gecmisi (vaka id -> kayitlar). */
  protected readonly histories = signal<Record<number, LabCaseHistoryDto[]>>({});

  constructor() {
    effect(() => {
      const patient = this.store.patient();
      if (patient) {
        untracked(() => {
          this.patientId.set(patient.id);
          this.load(patient.id);
        });
      }
    });
  }

  private load(patientId: number): void {
    this.loading.set(true);
    this.api.patientCases(patientId).subscribe({
      next: (cases) => {
        this.cases.set([...cases].sort((a, b) => b.createdAtUtc.localeCompare(a.createdAtUtc)));
        this.loading.set(false);
      },
      error: () => {
        this.cases.set([]);
        this.loading.set(false);
      },
    });
  }

  protected reload(): void {
    const id = this.patientId();
    if (id != null) {
      this.load(id);
    }
  }

  protected openNew(): void {
    this.editingCase.set(null);
    this.caseDialogVisible.set(true);
  }

  protected openEdit(labCase: LabCaseDto): void {
    this.editingCase.set(labCase);
    this.caseDialogVisible.set(true);
  }

  protected openStatus(labCase: LabCaseDto): void {
    this.statusTarget.set(labCase);
    this.statusDialogVisible.set(true);
  }

  protected onRowExpand(labCase: LabCaseDto): void {
    if (this.histories()[labCase.id]) {
      return;
    }
    this.api.history(labCase.id).subscribe({
      next: (history) =>
        this.histories.update((map) => ({
          ...map,
          [labCase.id]: [...history].sort((a, b) => b.changedAtUtc.localeCompare(a.changedAtUtc)),
        })),
    });
  }

  protected historyOf(id: number): LabCaseHistoryDto[] {
    return this.histories()[id] ?? [];
  }

  /** Durum degisince gecmis onbellegini de tazele. */
  protected onStatusChanged(): void {
    this.histories.set({});
    this.reload();
  }
}
