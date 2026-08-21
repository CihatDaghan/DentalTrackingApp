import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
  untracked,
} from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { CdkDrag, CdkDragDrop, CdkDropList, CdkDropListGroup } from '@angular/cdk/drag-drop';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { SelectButtonModule } from 'primeng/selectbutton';
import { TableModule } from 'primeng/table';
import { TextareaModule } from 'primeng/textarea';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmationService, MessageService } from 'primeng/api';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { LaboratoryApiService } from '../../core/api/laboratory-api.service';
import {
  LAB_ALL_STATUSES,
  LAB_KANBAN_STATUSES,
  LAB_STATUS_KEYS,
  LabCaseDto,
  LabCaseHistoryDto,
  LabCaseStatus,
  LaboratoryDto,
} from '../../core/api/laboratory-api.models';
import { AppointmentsApiService } from '../../core/api/appointments-api.service';
import { DoctorDto } from '../../core/api/api.models';
import { HasPermissionDirective } from '../../core/auth/has-permission.directive';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { StatusTagComponent } from '../../shared/components/status-tag/status-tag.component';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { TrDatePipe } from '../../shared/pipes/tr-date.pipe';
import { injectTranslationSignal } from '../../shared/utils/transloco-signal';
import { LabCaseDialogComponent } from './lab-case-dialog.component';
import { LabStatusDialogComponent } from './lab-status-dialog.component';

type LabView = 'table' | 'kanban';

/**
 * Klinik geneli laboratuvar sayfasi (/app/laboratory):
 * tablo (filtreli, gecikmis satir kirmizi) <-> kanban (surukle-birak durum degisimi)
 * gorunum secici + lab firmalari CRUD.
 */
@Component({
  selector: 'app-laboratory-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    FormsModule,
    CdkDrag,
    CdkDropList,
    CdkDropListGroup,
    ButtonModule,
    CheckboxModule,
    DialogModule,
    InputTextModule,
    SelectModule,
    SelectButtonModule,
    TableModule,
    TextareaModule,
    TooltipModule,
    TranslocoPipe,
    HasPermissionDirective,
    PageHeaderComponent,
    StatusTagComponent,
    MoneyPipe,
    TrDatePipe,
    LabCaseDialogComponent,
    LabStatusDialogComponent,
  ],
  templateUrl: './laboratory-page.component.html',
  styleUrl: './laboratory-page.component.scss',
})
export class LaboratoryPageComponent {
  private readonly api = inject(LaboratoryApiService);
  private readonly appointmentsApi = inject(AppointmentsApiService);
  private readonly messageService = inject(MessageService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly transloco = inject(TranslocoService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  /** Ceviriler yuklendiginde/dil degistiginde secenek listeleri yeniden hesaplansin. */
  private readonly translation = injectTranslationSignal();

  protected readonly kanbanStatuses = LAB_KANBAN_STATUSES;

  protected readonly view = signal<LabView>('table');
  protected readonly cases = signal<LabCaseDto[]>([]);
  protected readonly loading = signal(false);

  // Filtreler
  protected readonly filterStatus = signal<LabCaseStatus | null>(null);
  protected readonly filterLaboratoryId = signal<number | null>(null);
  protected readonly filterDoctorId = signal<number | null>(null);
  protected readonly filterOverdueOnly = signal(false);

  protected readonly laboratories = signal<LaboratoryDto[]>([]);
  protected readonly doctors = signal<DoctorDto[]>([]);

  // Dialoglar
  protected readonly caseDialogVisible = signal(false);
  protected readonly editingCase = signal<LabCaseDto | null>(null);
  protected readonly statusDialogVisible = signal(false);
  protected readonly statusTarget = signal<LabCaseDto | null>(null);
  protected readonly labsDialogVisible = signal(false);
  protected readonly labFormVisible = signal(false);
  protected readonly labSaving = signal(false);
  protected readonly editingLab = signal<LaboratoryDto | null>(null);
  protected readonly labName = signal('');
  protected readonly labPhone = signal('');
  protected readonly labEmail = signal('');
  protected readonly labContact = signal('');
  protected readonly labAddress = signal('');

  protected readonly histories = signal<Record<number, LabCaseHistoryDto[]>>({});

  protected readonly viewOptions = computed(() => {
    this.translation();
    return [
      { label: this.transloco.translate('laboratory.view.table'), value: 'table' as LabView },
      { label: this.transloco.translate('laboratory.view.kanban'), value: 'kanban' as LabView },
    ];
  });

  protected readonly statusOptions = computed(() => {
    this.translation();
    return LAB_ALL_STATUSES.map((value) => ({
      label: this.transloco.translate('laboratory.status.' + LAB_STATUS_KEYS[value]),
      value,
    }));
  });

  protected readonly laboratoryOptions = computed(() =>
    this.laboratories().map((l) => ({ label: l.name, value: l.id })),
  );

  protected readonly doctorOptions = computed(() =>
    this.doctors().map((d) => ({ label: `${d.firstName} ${d.lastName}`, value: d.id })),
  );

  protected readonly overdueCount = computed(() => this.cases().filter((c) => c.isOverdue).length);

  constructor() {
    // Dashboard'dan "gecikmis lab isleri" linki ?overdueOnly=true ile gelir.
    effect(() => {
      const overdue = this.route.snapshot.queryParamMap.get('overdueOnly') === 'true';
      untracked(() => {
        if (overdue) {
          this.filterOverdueOnly.set(true);
        }
        this.load();
      });
    });

    this.api.laboratories().subscribe({
      next: (labs) => this.laboratories.set(labs),
      error: () => this.laboratories.set([]),
    });
    this.appointmentsApi.doctors().subscribe({
      next: (doctors) => this.doctors.set(doctors),
      error: () => this.doctors.set([]),
    });
  }

  protected load(): void {
    this.loading.set(true);
    this.api
      .cases({
        status: this.filterStatus(),
        laboratoryId: this.filterLaboratoryId(),
        doctorUserId: this.filterDoctorId(),
        overdueOnly: this.filterOverdueOnly() || null,
        page: 1,
        pageSize: 200,
      })
      .subscribe({
        next: (result) => {
          this.cases.set(result.items);
          this.histories.set({});
          this.loading.set(false);
        },
        error: () => {
          this.cases.set([]);
          this.loading.set(false);
        },
      });
  }

  protected clearFilters(): void {
    this.filterStatus.set(null);
    this.filterLaboratoryId.set(null);
    this.filterDoctorId.set(null);
    this.filterOverdueOnly.set(false);
    void this.router.navigate([], { relativeTo: this.route, queryParams: {} });
    this.load();
  }

  protected casesByStatus(status: LabCaseStatus): LabCaseDto[] {
    return this.cases().filter((c) => c.status === status);
  }

  protected columnId(status: LabCaseStatus): string {
    return `lab-col-${status}`;
  }

  // --- Kanban surukle-birak --------------------------------------------------

  protected onDrop(event: CdkDragDrop<LabCaseStatus>, target: LabCaseStatus): void {
    const labCase = event.item.data as LabCaseDto;
    if (!labCase || labCase.status === target) {
      return;
    }
    // Iyimser guncelleme; hata olursa listeyi sunucudan tazeleriz.
    this.cases.update((list) =>
      list.map((c) => (c.id === labCase.id ? { ...c, status: target } : c)),
    );
    this.api.changeStatus(labCase.id, { status: target, note: null }).subscribe({
      next: (updated) => {
        this.cases.update((list) => list.map((c) => (c.id === updated.id ? updated : c)));
        this.messageService.add({
          severity: 'success',
          summary: this.transloco.translate('laboratory.statusChanged'),
          life: 2500,
        });
      },
      error: () => this.load(),
    });
  }

  // --- Vaka dialoglari ------------------------------------------------------

  protected openNewCase(): void {
    this.editingCase.set(null);
    this.caseDialogVisible.set(true);
  }

  protected openEditCase(labCase: LabCaseDto): void {
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

  protected removeCase(labCase: LabCaseDto): void {
    this.confirmation.confirm({
      header: this.transloco.translate('laboratory.deleteTitle'),
      message: this.transloco.translate('laboratory.deleteMessage'),
      icon: 'pi pi-exclamation-triangle',
      acceptButtonProps: { label: this.transloco.translate('common.delete'), severity: 'danger' },
      rejectButtonProps: {
        label: this.transloco.translate('common.cancel'),
        severity: 'secondary',
        outlined: true,
      },
      accept: () => this.api.deleteCase(labCase.id).subscribe({ next: () => this.load() }),
    });
  }

  // --- Lab firmalari --------------------------------------------------------

  protected openLabs(): void {
    this.labsDialogVisible.set(true);
    this.reloadLaboratories();
  }

  private reloadLaboratories(): void {
    this.api.laboratories().subscribe({ next: (labs) => this.laboratories.set(labs) });
  }

  protected openLabForm(lab: LaboratoryDto | null): void {
    this.editingLab.set(lab);
    this.labName.set(lab?.name ?? '');
    this.labPhone.set(lab?.phone ?? '');
    this.labEmail.set(lab?.email ?? '');
    this.labContact.set(lab?.contactPerson ?? '');
    this.labAddress.set(lab?.address ?? '');
    this.labFormVisible.set(true);
  }

  protected saveLab(): void {
    if (!this.labName().trim()) {
      return;
    }
    const request = {
      name: this.labName().trim(),
      phone: this.labPhone().trim() || null,
      email: this.labEmail().trim() || null,
      contactPerson: this.labContact().trim() || null,
      address: this.labAddress().trim() || null,
    };
    this.labSaving.set(true);
    const editing = this.editingLab();
    const call = editing
      ? this.api.updateLaboratory(editing.id, request)
      : this.api.createLaboratory(request);
    call.subscribe({
      next: () => {
        this.labSaving.set(false);
        this.labFormVisible.set(false);
        this.messageService.add({
          severity: 'success',
          summary: this.transloco.translate('laboratory.labSaved'),
          life: 3000,
        });
        this.reloadLaboratories();
      },
      error: () => this.labSaving.set(false),
    });
  }

  protected removeLab(lab: LaboratoryDto): void {
    this.confirmation.confirm({
      header: this.transloco.translate('laboratory.deleteLabTitle'),
      message: this.transloco.translate('laboratory.deleteLabMessage'),
      icon: 'pi pi-exclamation-triangle',
      acceptButtonProps: { label: this.transloco.translate('common.delete'), severity: 'danger' },
      rejectButtonProps: {
        label: this.transloco.translate('common.cancel'),
        severity: 'secondary',
        outlined: true,
      },
      accept: () =>
        this.api.deleteLaboratory(lab.id).subscribe({ next: () => this.reloadLaboratories() }),
    });
  }
}
