import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { Router } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { IconFieldModule } from 'primeng/iconfield';
import { InputIconModule } from 'primeng/inputicon';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { MessageService } from 'primeng/api';
import { PatientsApiService } from '../../../core/api/patients-api.service';
import { PatientListItemDto, TableQuery } from '../../../core/api/api.models';
import { HasPermissionDirective } from '../../../core/auth/has-permission.directive';
import {
  AppTableComponent,
  TableColumn,
} from '../../../shared/components/app-table/app-table.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { MoneyPipe } from '../../../shared/pipes/money.pipe';
import { TrDatePipe } from '../../../shared/pipes/tr-date.pipe';
import { PatientFormComponent } from '../patient-form/patient-form.component';

/** Hasta listesi: lazy p-table + global arama (300 ms debounce) + "Yeni Hasta" dialogu. */
@Component({
  selector: 'app-patient-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ButtonModule,
    DialogModule,
    InputTextModule,
    IconFieldModule,
    InputIconModule,
    TranslocoPipe,
    HasPermissionDirective,
    AppTableComponent,
    PageHeaderComponent,
    MoneyPipe,
    TrDatePipe,
    PatientFormComponent,
  ],
  templateUrl: './patient-list.component.html',
})
export class PatientListComponent {
  private readonly patientsApi = inject(PatientsApiService);
  private readonly router = inject(Router);
  private readonly transloco = inject(TranslocoService);
  private readonly messageService = inject(MessageService);

  protected readonly table = viewChild<AppTableComponent<PatientListItemDto>>('table');
  protected readonly patientForm = viewChild<PatientFormComponent>('patientForm');

  protected readonly search = signal('');
  protected readonly createVisible = signal(false);
  protected readonly saving = signal(false);

  private readonly searchInput$ = new Subject<string>();

  protected readonly columns: TableColumn[] = [
    { field: 'fileNo', headerKey: 'patients.columns.fileNo', sortField: 'fileno', width: '7rem' },
    { field: 'name', headerKey: 'patients.columns.name', sortField: 'name' },
    { field: 'phone', headerKey: 'patients.columns.phone', width: '12rem' },
    { field: 'city', headerKey: 'patients.columns.city', width: '10rem' },
    {
      field: 'createdAtUtc',
      headerKey: 'patients.columns.createdAt',
      sortField: 'createdat',
      width: '10rem',
    },
    { field: 'balance', headerKey: 'patients.columns.balance', width: '10rem' },
  ];

  protected readonly loader = (query: TableQuery) => this.patientsApi.list(query);

  constructor() {
    this.searchInput$
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe((value) => this.search.set(value));
  }

  protected onSearchInput(event: Event): void {
    this.searchInput$.next((event.target as HTMLInputElement).value.trim());
  }

  protected openRow(row: PatientListItemDto): void {
    void this.router.navigate(['/app/patients', row.id]);
  }

  protected openCreate(): void {
    this.createVisible.set(true);
  }

  protected saveCreate(): void {
    const form = this.patientForm();
    if (!form) {
      return;
    }
    const summary = form.validationSummary();
    if (summary) {
      this.messageService.add({ severity: 'warn', summary, life: 4000 });
      return;
    }
    this.saving.set(true);
    this.patientsApi.create(form.toRequest()).subscribe({
      next: (patient) => {
        this.saving.set(false);
        this.createVisible.set(false);
        this.messageService.add({
          severity: 'success',
          summary: this.transloco.translate('patients.createSuccess'),
          life: 3000,
        });
        void this.router.navigate(['/app/patients', patient.id]);
      },
      error: () => this.saving.set(false),
    });
  }
}
