import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Subject, debounceTime, merge } from 'rxjs';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { SelectButtonModule } from 'primeng/selectbutton';
import { TableModule } from 'primeng/table';
import { TextareaModule } from 'primeng/textarea';
import { TooltipModule } from 'primeng/tooltip';
import { AutoCompleteCompleteEvent, AutoCompleteModule } from 'primeng/autocomplete';
import { ConfirmationService, MessageService } from 'primeng/api';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';

import { AppointmentsApiService } from '../../../../../core/api/appointments-api.service';
import { CatalogApiService } from '../../../../../core/api/catalog-api.service';
import { TreatmentsApiService } from '../../../../../core/api/treatments-api.service';
import { UserType } from '../../../../../core/api/auth-api.models';
import { AuthStore } from '../../../../../core/auth/auth.store';
import { HasPermissionDirective } from '../../../../../core/auth/has-permission.directive';
import { DoctorDto } from '../../../../../core/api/api.models';
import {
  IcdCodeDto,
  surfacesLabel,
  surfacesToFlags,
  ToothScope,
  ToothStatusEffect,
  TreatmentAddItem,
  TreatmentDefinitionDto,
  TreatmentCategoryDto,
  TreatmentRecordDto,
  TreatmentRecordStatus,
} from '../../../../../core/api/treatment-api.models';
import { ToothChartComponent } from '../../../../../shared/components/tooth-chart/tooth-chart.component';
import {
  Dentition,
  Surface,
  SurfaceClickEvent,
  ToothChartLegendItem,
  ToothLayer,
  ToothState,
} from '../../../../../shared/components/tooth-chart/tooth-chart.models';
import { MoneyPipe } from '../../../../../shared/pipes/money.pipe';
import { TrDatePipe } from '../../../../../shared/pipes/tr-date.pipe';
import { StatusTagComponent } from '../../../../../shared/components/status-tag/status-tag.component';
import { PatientDetailStore } from '../../patient-detail.store';
import { mapTeethToStates, statusToLayer } from './teeth-mapper';

interface SelectionItem {
  toothNo: string;
  surfaces: Surface[];
}

interface BulkPriceRow {
  record: TreatmentRecordDto;
  price: number;
  discountAmount: number;
}

const ALL_SURFACES: Surface[] = ['M', 'O', 'D', 'B', 'L'];

/**
 * Tedavi sekmesi — urunun kalbi (design-1.md §2.5 madde 2):
 * sol katalog paneli + orta dis semasi + sag kayit paneli + alt tedavi gecmisi tablosu.
 */
@Component({
  selector: 'app-treatment-tab',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    ReactiveFormsModule,
    ButtonModule,
    DialogModule,
    InputNumberModule,
    InputTextModule,
    SelectModule,
    SelectButtonModule,
    TableModule,
    TextareaModule,
    TooltipModule,
    AutoCompleteModule,
    TranslocoPipe,
    HasPermissionDirective,
    ToothChartComponent,
    MoneyPipe,
    TrDatePipe,
    StatusTagComponent,
  ],
  templateUrl: './treatment-tab.component.html',
  styleUrl: './treatment-tab.component.scss',
})
export class TreatmentTabComponent {
  private readonly store = inject(PatientDetailStore);
  private readonly catalogApi = inject(CatalogApiService);
  private readonly treatmentsApi = inject(TreatmentsApiService);
  private readonly appointmentsApi = inject(AppointmentsApiService);
  private readonly authStore = inject(AuthStore);
  private readonly messageService = inject(MessageService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly transloco = inject(TranslocoService);
  private readonly fb = inject(FormBuilder);

  protected readonly TreatmentRecordStatus = TreatmentRecordStatus;
  protected readonly ToothScope = ToothScope;
  protected readonly surfacesLabel = surfacesLabel;

  private readonly chart = viewChild(ToothChartComponent);

  // --- Sol panel: katalog ---------------------------------------------------
  protected readonly categories = signal<TreatmentCategoryDto[]>([]);
  protected readonly selectedCategoryId = signal<number | null>(null);
  protected readonly searchTerm = signal('');
  protected readonly catalogItems = signal<TreatmentDefinitionDto[]>([]);
  protected readonly catalogLoading = signal(false);
  protected readonly activeTreatment = signal<TreatmentDefinitionDto | null>(null);

  /** Varsayilan tarifeden cozumlenen fiyatlar (defId -> fiyat). */
  private readonly priceMap = signal<ReadonlyMap<number, number>>(new Map());

  private readonly searchDebounce = new Subject<void>();

  // --- Orta: dis semasi -----------------------------------------------------
  protected readonly dentition = signal<Dentition>('adult');
  protected readonly teethStates = signal<ToothState[]>([]);
  protected readonly mouthWideCount = signal(0);

  protected readonly dentitionOptions = computed(() => {
    this.langTick();
    return [
      { label: this.transloco.translate('toothChart.dentition.adult'), value: 'adult' as Dentition },
      {
        label: this.transloco.translate('toothChart.dentition.primary'),
        value: 'primary' as Dentition,
      },
    ];
  });

  protected readonly legendItems = computed<ToothChartLegendItem[]>(() =>
    this.categories()
      .filter((c) => !!c.colorHex)
      .map((c) => ({ color: c.colorHex!, label: c.name })),
  );

  // --- Sag panel ------------------------------------------------------------
  protected readonly doctors = signal<DoctorDto[]>([]);
  protected readonly selectedDoctorId = signal<number | null>(null);
  /** Hekim rolundeki kullanici icin secici kilitli (userType sayisal enum). */
  protected readonly doctorLocked = computed(() => this.authStore.user()?.userType === UserType.Dentist);

  protected readonly activeStatus = signal<TreatmentRecordStatus>(TreatmentRecordStatus.Planned);
  protected readonly activeLayer = computed<ToothLayer>(() => statusToLayer(this.activeStatus()));

  protected readonly statusOptions = computed(() => {
    this.langTick();
    return [
      {
        label: this.transloco.translate('toothChart.layers.diagnosis'),
        value: TreatmentRecordStatus.Diagnosis,
      },
      {
        label: this.transloco.translate('toothChart.layers.plan'),
        value: TreatmentRecordStatus.Planned,
      },
      {
        label: this.transloco.translate('toothChart.layers.treatment'),
        value: TreatmentRecordStatus.Done,
      },
    ];
  });

  protected readonly selectedTeeth = signal<string[]>([]);
  protected readonly surfacesByTooth = signal<Record<string, Surface[]>>({});

  protected readonly selectionItems = computed<SelectionItem[]>(() => {
    const surfaceMap = this.surfacesByTooth();
    const set = new Set<string>(this.selectedTeeth());
    for (const [tooth, surfaces] of Object.entries(surfaceMap)) {
      if (surfaces.length) {
        set.add(tooth);
      }
    }
    return [...set]
      .sort((a, b) => a.localeCompare(b))
      .map((toothNo) => ({ toothNo, surfaces: surfaceMap[toothNo] ?? [] }));
  });

  protected readonly icdSuggestions = signal<IcdCodeDto[]>([]);
  protected selectedIcd: IcdCodeDto | null = null;
  protected description = '';
  protected priceOverride: number | null = null;
  protected readonly adding = signal(false);

  // --- Alt tablo: tedavi gecmisi -------------------------------------------
  protected readonly treatments = signal<TreatmentRecordDto[]>([]);
  protected readonly treatmentsLoading = signal(false);
  protected readonly historyFilter = signal<TreatmentRecordStatus | null>(null);
  protected selectedRows: TreatmentRecordDto[] = [];

  protected readonly historyFilterOptions = computed(() => {
    this.langTick();
    return [
      { label: this.transloco.translate('treatment.history.all'), value: null },
      {
        label: this.transloco.translate('treatmentStatus.diagnosis'),
        value: TreatmentRecordStatus.Diagnosis,
      },
      {
        label: this.transloco.translate('treatmentStatus.planned'),
        value: TreatmentRecordStatus.Planned,
      },
      {
        label: this.transloco.translate('treatmentStatus.done'),
        value: TreatmentRecordStatus.Done,
      },
    ];
  });

  protected readonly filteredTreatments = computed(() => {
    const filter = this.historyFilter();
    const rows = this.treatments();
    return filter == null ? rows : rows.filter((t) => t.status === filter);
  });

  // --- Dialoglar ------------------------------------------------------------
  protected readonly editTarget = signal<TreatmentRecordDto | null>(null);
  protected readonly editSaving = signal(false);
  protected readonly editForm = this.fb.group({
    surfaces: this.fb.nonNullable.control<Surface[]>([]),
    price: this.fb.control<number | null>(null, [Validators.required, Validators.min(0)]),
    discountAmount: this.fb.nonNullable.control<number>(0, [Validators.min(0)]),
    description: this.fb.control<string | null>(null),
  });
  protected readonly surfaceOptions = ALL_SURFACES.map((s) => ({ label: s, value: s }));

  protected readonly cancelTarget = signal<TreatmentRecordDto | null>(null);
  protected readonly cancelSaving = signal(false);
  protected cancelReason = '';

  protected readonly bulkPriceVisible = signal(false);
  protected readonly bulkPriceSaving = signal(false);
  protected bulkPriceRows: BulkPriceRow[] = [];

  protected readonly addGeneralVisible = signal(false);
  protected readonly addGeneralSaving = signal(false);
  protected readonly generalSuggestions = signal<TreatmentDefinitionDto[]>([]);
  protected generalTreatment: TreatmentDefinitionDto | null = null;
  protected generalStatus: TreatmentRecordStatus = TreatmentRecordStatus.Planned;
  protected generalPrice: number | null = null;
  protected generalDescription = '';

  private lastLoadedPatientId: number | null = null;

  /** Dil degisiminde VE ceviri dosyasi yuklendiginde option etiketlerini yeniden uret. */
  private readonly langTick = toSignal(
    merge(this.transloco.langChanges$, this.transloco.events$),
    { initialValue: null },
  );

  constructor() {
    effect(() => {
      const patient = this.store.patient();
      if (patient && patient.id !== this.lastLoadedPatientId) {
        this.lastLoadedPatientId = patient.id;
        untracked(() => this.loadAll(patient.id));
      }
    });

    this.searchDebounce
      .pipe(debounceTime(250), takeUntilDestroyed())
      .subscribe(() => this.loadCatalog());
  }

  private get patientId(): number | null {
    return this.store.patient()?.id ?? null;
  }

  // ==========================================================================
  // Yukleme
  // ==========================================================================

  private loadAll(patientId: number): void {
    this.catalogApi.categories().subscribe({
      next: (categories) =>
        this.categories.set([...categories].sort((a, b) => a.sortOrder - b.sortOrder)),
    });
    this.loadPriceMap();
    this.loadCatalog();
    this.appointmentsApi.doctors().subscribe({
      next: (doctors) => {
        this.doctors.set(doctors);
        if (this.doctorLocked()) {
          const ownId = Number(this.authStore.user()?.id);
          this.selectedDoctorId.set(Number.isFinite(ownId) ? ownId : null);
        } else if (this.selectedDoctorId() == null && doctors.length) {
          this.selectedDoctorId.set(doctors[0].id);
        }
      },
      error: () => this.doctors.set([]),
    });
    this.loadTeeth(patientId);
    this.loadTreatments(patientId);
  }

  private loadPriceMap(): void {
    this.catalogApi.priceLists().subscribe({
      next: (lists) => {
        const target = lists.find((l) => l.isDefault) ?? lists[0];
        if (!target) {
          return;
        }
        this.catalogApi.priceListItems(target.id).subscribe({
          next: (items) =>
            this.priceMap.set(new Map(items.map((i) => [i.treatmentDefinitionId, i.price]))),
        });
      },
    });
  }

  protected loadCatalog(): void {
    this.catalogLoading.set(true);
    this.catalogApi
      .definitions({
        search: this.searchTerm() || undefined,
        categoryId: this.selectedCategoryId(),
        isActive: true,
        page: 1,
        pageSize: 200,
      })
      .subscribe({
        next: (result) => {
          this.catalogItems.set(result.items);
          this.catalogLoading.set(false);
        },
        error: () => {
          this.catalogItems.set([]);
          this.catalogLoading.set(false);
        },
      });
  }

  private loadTeeth(patientId: number): void {
    this.treatmentsApi.teeth(patientId).subscribe({
      next: (dto) => {
        this.teethStates.set(mapTeethToStates(dto));
        this.mouthWideCount.set(dto.mouthWideMarks.length);
      },
      error: () => this.teethStates.set([]),
    });
  }

  private loadTreatments(patientId: number): void {
    this.treatmentsLoading.set(true);
    this.treatmentsApi.list(patientId).subscribe({
      next: (rows) => {
        this.treatments.set(rows);
        this.selectedRows = [];
        this.treatmentsLoading.set(false);
      },
      error: () => {
        this.treatments.set([]);
        this.treatmentsLoading.set(false);
      },
    });
  }

  /** Sema + tablo + baslik bakiyesini tazeler. */
  private refreshAfterMutation(refreshBalance: boolean): void {
    const patientId = this.patientId;
    if (patientId == null) {
      return;
    }
    this.loadTeeth(patientId);
    this.loadTreatments(patientId);
    if (refreshBalance) {
      this.store.refresh();
    }
  }

  // ==========================================================================
  // Sol panel
  // ==========================================================================

  protected onSearchInput(value: string): void {
    this.searchTerm.set(value);
    this.searchDebounce.next();
  }

  protected onCategoryChange(categoryId: number | null): void {
    this.selectedCategoryId.set(categoryId);
    this.loadCatalog();
  }

  protected resolvedPrice(def: TreatmentDefinitionDto): number {
    return this.priceMap().get(def.id) ?? def.defaultPrice;
  }

  protected selectTreatment(def: TreatmentDefinitionDto): void {
    this.activeTreatment.set(this.activeTreatment()?.id === def.id ? null : def);
  }

  protected categoryById(id: number): TreatmentCategoryDto | undefined {
    return this.categories().find((c) => c.id === id);
  }

  // ==========================================================================
  // Orta: sema etkilesimi
  // ==========================================================================

  protected setDentition(value: Dentition): void {
    if (value && value !== this.dentition()) {
      this.dentition.set(value);
      this.clearSelection();
    }
  }

  protected onSelectionChange(selection: string[]): void {
    this.selectedTeeth.set(selection);
    // Secimden dusen dislerin yuzeyleri de temizlensin.
    const map = { ...this.surfacesByTooth() };
    let changed = false;
    for (const tooth of Object.keys(map)) {
      if (!selection.includes(tooth)) {
        delete map[tooth];
        changed = true;
      }
    }
    if (changed) {
      this.surfacesByTooth.set(map);
    }
  }

  protected onSurfaceClick(event: SurfaceClickEvent): void {
    const map = { ...this.surfacesByTooth() };
    const current = map[event.toothNo] ?? [];
    map[event.toothNo] = current.includes(event.surface)
      ? current.filter((s) => s !== event.surface)
      : [...ALL_SURFACES.filter((s) => s === event.surface || current.includes(s))];
    this.surfacesByTooth.set(map);
    // Yuzeyine tiklanan dis secime dahil olsun.
    if (!this.selectedTeeth().includes(event.toothNo)) {
      this.selectedTeeth.update((list) => [...list, event.toothNo]);
    }
  }

  protected removeSelection(toothNo: string): void {
    this.selectedTeeth.update((list) => list.filter((t) => t !== toothNo));
    const map = { ...this.surfacesByTooth() };
    delete map[toothNo];
    this.surfacesByTooth.set(map);
  }

  protected clearSelection(): void {
    this.chart()?.clearSelection();
    this.selectedTeeth.set([]);
    this.surfacesByTooth.set({});
  }

  // ==========================================================================
  // Sag panel: Ekle
  // ==========================================================================

  protected searchIcd(event: AutoCompleteCompleteEvent): void {
    const query = event.query.trim();
    if (query.length < 2) {
      this.icdSuggestions.set([]);
      return;
    }
    this.catalogApi.icdCodes(query).subscribe({
      next: (codes) => this.icdSuggestions.set(codes),
      error: () => this.icdSuggestions.set([]),
    });
  }

  protected add(): void {
    const def = this.activeTreatment();
    const patientId = this.patientId;
    if (!def || patientId == null) {
      this.toast('warn', 'treatment.selectTreatmentFirst');
      return;
    }

    if (def.toothScope === ToothScope.WholeMouth) {
      // Agiz geneli islem: dis secimi olmadan (varsa yok sayilarak) eklenir — onayli.
      this.confirmation.confirm({
        header: this.transloco.translate('treatment.wholeMouthTitle'),
        message: this.transloco.translate('treatment.wholeMouthMessage', { name: def.name }),
        icon: 'pi pi-info-circle',
        acceptButtonProps: { label: this.transloco.translate('treatment.add') },
        rejectButtonProps: {
          label: this.transloco.translate('common.cancel'),
          severity: 'secondary',
          outlined: true,
        },
        accept: () => this.postItems(patientId, [this.buildItem(def, null, [])], false),
      });
      return;
    }

    const selection = this.selectionItems();
    if (!selection.length) {
      if (def.toothScope === ToothScope.PerJaw) {
        // Cene bazli islem dissiz de eklenebilir.
        this.postItems(patientId, [this.buildItem(def, null, [])], false);
        return;
      }
      this.toast('warn', 'treatment.selectToothFirst');
      return;
    }

    if (def.requiresSurface) {
      const missing = selection.filter((s) => !s.surfaces.length).map((s) => s.toothNo);
      if (missing.length) {
        this.messageService.add({
          severity: 'warn',
          summary: this.transloco.translate('treatment.surfaceRequired'),
          detail: this.transloco.translate('treatment.surfaceRequiredDetail', {
            teeth: missing.join(', '),
          }),
          life: 4000,
        });
        return;
      }
    }

    this.postItems(
      patientId,
      selection.map((s) => this.buildItem(def, s.toothNo, s.surfaces)),
      false,
    );
  }

  private buildItem(
    def: TreatmentDefinitionDto,
    toothNo: string | null,
    surfaces: Surface[],
  ): TreatmentAddItem {
    const status = this.activeStatus();
    return {
      treatmentDefinitionId: def.id,
      toothNumber: toothNo,
      surfaces: surfacesToFlags(surfaces),
      status,
      doctorUserId: this.selectedDoctorId(),
      price: this.priceOverride,
      description: this.description.trim() || null,
      diagnosisIcdCode:
        status === TreatmentRecordStatus.Diagnosis ? (this.selectedIcd?.code ?? null) : null,
      rootCanalCount: def.toothStatusEffect === ToothStatusEffect.RootCanal ? 1 : null,
    };
  }

  private postItems(patientId: number, items: TreatmentAddItem[], fromDialog: boolean): void {
    const saving = fromDialog ? this.addGeneralSaving : this.adding;
    saving.set(true);
    this.treatmentsApi.add(patientId, { items }).subscribe({
      next: (created) => {
        saving.set(false);
        this.toast('success', 'treatment.addSuccess', { count: created.length });
        if (fromDialog) {
          this.addGeneralVisible.set(false);
        } else {
          this.clearSelection();
          this.description = '';
          this.priceOverride = null;
          this.selectedIcd = null;
        }
        this.refreshAfterMutation(items.some((i) => i.status === TreatmentRecordStatus.Done));
      },
      error: () => saving.set(false),
    });
  }

  // ==========================================================================
  // Alt tablo: satir aksiyonlari
  // ==========================================================================

  protected openEdit(row: TreatmentRecordDto): void {
    this.editForm.reset({
      surfaces: [...surfacesLabel(row.surfaces)] as Surface[],
      price: row.price,
      discountAmount: row.discountAmount,
      description: row.description,
    });
    this.editTarget.set(row);
  }

  protected saveEdit(): void {
    const row = this.editTarget();
    if (!row || this.editForm.invalid) {
      this.editForm.markAllAsTouched();
      return;
    }
    const v = this.editForm.getRawValue();
    this.editSaving.set(true);
    this.treatmentsApi
      .update(row.id, {
        surfaces: surfacesToFlags(v.surfaces),
        price: v.price,
        discountAmount: v.discountAmount,
        description: v.description,
      })
      .subscribe({
        next: () => {
          this.editSaving.set(false);
          this.editTarget.set(null);
          this.toast('success', 'treatment.updateSuccess');
          this.refreshAfterMutation(false);
        },
        error: () => this.editSaving.set(false),
      });
  }

  /** Diagnosis -> Planned / Planned -> Done gecisleri (onayli). */
  protected convert(row: TreatmentRecordDto): void {
    const toDone = row.status === TreatmentRecordStatus.Planned;
    const target = toDone ? TreatmentRecordStatus.Done : TreatmentRecordStatus.Planned;
    this.confirmation.confirm({
      header: this.transloco.translate(toDone ? 'treatment.convertTitle' : 'treatment.toPlanTitle'),
      message: this.transloco.translate(
        toDone ? 'treatment.convertMessage' : 'treatment.toPlanMessage',
        { name: row.treatmentName, tooth: row.toothNumber ?? '—' },
      ),
      icon: 'pi pi-check-circle',
      acceptButtonProps: { label: this.transloco.translate('common.yes') },
      rejectButtonProps: {
        label: this.transloco.translate('common.no'),
        severity: 'secondary',
        outlined: true,
      },
      accept: () => {
        this.treatmentsApi.updateStatus(row.id, { status: target }).subscribe({
          next: () => {
            this.toast(
              'success',
              toDone ? 'treatment.convertSuccess' : 'treatment.toPlanSuccess',
            );
            this.refreshAfterMutation(toDone);
          },
        });
      },
    });
  }

  protected openCancel(row: TreatmentRecordDto): void {
    this.cancelReason = '';
    this.cancelTarget.set(row);
  }

  protected saveCancel(): void {
    const row = this.cancelTarget();
    if (!row) {
      return;
    }
    this.cancelSaving.set(true);
    const reason = this.cancelReason.trim();
    const finish = () => {
      this.treatmentsApi
        .updateStatus(row.id, { status: TreatmentRecordStatus.Cancelled })
        .subscribe({
          next: () => {
            this.cancelSaving.set(false);
            this.cancelTarget.set(null);
            this.toast('success', 'treatment.cancelSuccess');
            this.refreshAfterMutation(row.status === TreatmentRecordStatus.Done);
          },
          error: () => this.cancelSaving.set(false),
        });
    };
    if (reason) {
      // Durum ucunda neden alani yok — neden aciklamaya islenir (iptalden ONCE, cunku
      // iptal edilmis kayit guncellenemez).
      const description = [row.description, `${this.transloco.translate('treatment.cancelReasonPrefix')}: ${reason}`]
        .filter(Boolean)
        .join(' — ');
      this.treatmentsApi.update(row.id, { description }).subscribe({
        next: finish,
        error: () => this.cancelSaving.set(false),
      });
    } else {
      finish();
    }
  }

  protected remove(row: TreatmentRecordDto): void {
    this.confirmation.confirm({
      header: this.transloco.translate('treatment.deleteTitle'),
      message: this.transloco.translate('treatment.deleteMessage', { name: row.treatmentName }),
      icon: 'pi pi-exclamation-triangle',
      acceptButtonProps: {
        label: this.transloco.translate('common.delete'),
        severity: 'danger',
      },
      rejectButtonProps: {
        label: this.transloco.translate('common.cancel'),
        severity: 'secondary',
        outlined: true,
      },
      accept: () => {
        this.treatmentsApi.delete(row.id).subscribe({
          next: () => {
            this.toast('success', 'treatment.deleteSuccess');
            this.refreshAfterMutation(false);
          },
        });
      },
    });
  }

  // ==========================================================================
  // Edit Price (toplu) + Add Treatment (genel)
  // ==========================================================================

  protected openBulkPrice(): void {
    const rows = this.selectedRows.filter((r) => r.status !== TreatmentRecordStatus.Cancelled);
    if (!rows.length) {
      this.toast('warn', 'treatment.bulkPriceNoSelection');
      return;
    }
    this.bulkPriceRows = rows.map((record) => ({
      record,
      price: record.price,
      discountAmount: record.discountAmount,
    }));
    this.bulkPriceVisible.set(true);
  }

  protected saveBulkPrice(): void {
    this.bulkPriceSaving.set(true);
    this.treatmentsApi
      .bulkPrice({
        items: this.bulkPriceRows.map((r) => ({
          id: r.record.id,
          price: r.price ?? 0,
          discountAmount: r.discountAmount ?? 0,
        })),
      })
      .subscribe({
        next: () => {
          this.bulkPriceSaving.set(false);
          this.bulkPriceVisible.set(false);
          this.toast('success', 'treatment.bulkPriceSuccess');
          this.refreshAfterMutation(false);
        },
        error: () => this.bulkPriceSaving.set(false),
      });
  }

  protected openAddGeneral(): void {
    this.generalTreatment = null;
    this.generalStatus = TreatmentRecordStatus.Planned;
    this.generalPrice = null;
    this.generalDescription = '';
    this.addGeneralVisible.set(true);
  }

  protected searchGeneral(event: AutoCompleteCompleteEvent): void {
    this.catalogApi
      .definitions({ search: event.query, isActive: true, page: 1, pageSize: 20 })
      .subscribe({
        next: (result) =>
          this.generalSuggestions.set(
            result.items.filter((d) => d.toothScope !== ToothScope.PerTooth),
          ),
        error: () => this.generalSuggestions.set([]),
      });
  }

  protected saveAddGeneral(): void {
    const def = this.generalTreatment;
    const patientId = this.patientId;
    if (!def || patientId == null) {
      this.toast('warn', 'treatment.selectTreatmentFirst');
      return;
    }
    this.postItems(
      patientId,
      [
        {
          treatmentDefinitionId: def.id,
          toothNumber: null,
          status: this.generalStatus,
          doctorUserId: this.selectedDoctorId(),
          price: this.generalPrice,
          description: this.generalDescription.trim() || null,
        },
      ],
      true,
    );
  }

  // ==========================================================================
  // Yardimcilar
  // ==========================================================================

  protected doctorName(id: number): string {
    const doctor = this.doctors().find((d) => d.id === id);
    if (doctor) {
      return `${doctor.firstName} ${doctor.lastName}`;
    }
    const row = this.treatments().find((t) => t.doctorUserId === id);
    return row?.doctorName || '—';
  }

  protected netAmount(row: TreatmentRecordDto): number {
    return Math.max(0, row.price - row.discountAmount);
  }

  private toast(
    severity: 'success' | 'warn',
    key: string,
    params?: Record<string, unknown>,
  ): void {
    this.messageService.add({
      severity,
      summary: this.transloco.translate(key, params),
      life: 3000,
    });
  }
}
