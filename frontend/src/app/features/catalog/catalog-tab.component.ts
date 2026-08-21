import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Subject, debounceTime, merge } from 'rxjs';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmationService, MessageService } from 'primeng/api';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';

import { CatalogApiService } from '../../core/api/catalog-api.service';
import { HasPermissionDirective } from '../../core/auth/has-permission.directive';
import {
  ToothScope,
  ToothStatusEffect,
  TreatmentCategoryDto,
  TreatmentDefinitionDto,
} from '../../core/api/treatment-api.models';
import { MoneyPipe } from '../../shared/pipes/money.pipe';

/** Kategori dialog renk paleti (tasarimdaki kategori renkleri). */
const COLOR_SWATCHES = [
  '#0ea5e9',
  '#3b82f6',
  '#ef4444',
  '#8b5cf6',
  '#6b7280',
  '#10b981',
  '#f59e0b',
  '#ec4899',
  '#14b8a6',
  '#64748b',
];

/** Katalog sekmesi: sol kategori listesi (CRUD) + sag tedavi tablosu (CRUD). */
@Component({
  selector: 'app-catalog-tab',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    ReactiveFormsModule,
    ButtonModule,
    CheckboxModule,
    DialogModule,
    InputNumberModule,
    InputTextModule,
    SelectModule,
    TableModule,
    TagModule,
    TooltipModule,
    TranslocoPipe,
    HasPermissionDirective,
    MoneyPipe,
  ],
  templateUrl: './catalog-tab.component.html',
  styleUrl: './catalog-tab.component.scss',
})
export class CatalogTabComponent {
  private readonly api = inject(CatalogApiService);
  private readonly messageService = inject(MessageService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly transloco = inject(TranslocoService);
  private readonly fb = inject(FormBuilder);

  protected readonly ToothScope = ToothScope;
  protected readonly colorSwatches = COLOR_SWATCHES;

  /** Dil degisiminde VE ceviri dosyasi yuklendiginde etiketleri yeniden uret. */
  private readonly langTick = toSignal(
    merge(this.transloco.langChanges$, this.transloco.events$),
    { initialValue: null },
  );

  protected readonly categories = signal<TreatmentCategoryDto[]>([]);
  protected readonly selectedCategoryId = signal<number | null>(null);
  protected readonly searchTerm = signal('');
  protected readonly items = signal<TreatmentDefinitionDto[]>([]);
  protected readonly itemsLoading = signal(false);

  private readonly searchDebounce = new Subject<void>();

  // --- Kategori dialogu -----------------------------------------------------
  protected readonly categoryDialogVisible = signal(false);
  protected readonly categorySaving = signal(false);
  protected editingCategory: TreatmentCategoryDto | null = null;
  protected readonly categoryForm = this.fb.group({
    name: this.fb.nonNullable.control('', [Validators.required]),
    nameEn: this.fb.control<string | null>(null),
    colorHex: this.fb.nonNullable.control('#3b82f6'),
    sortOrder: this.fb.nonNullable.control(0),
  });

  // --- Tedavi dialogu -------------------------------------------------------
  protected readonly defDialogVisible = signal(false);
  protected readonly defSaving = signal(false);
  protected editingDef: TreatmentDefinitionDto | null = null;
  protected readonly defForm = this.fb.group({
    categoryId: this.fb.control<number | null>(null, [Validators.required]),
    code: this.fb.nonNullable.control('', [Validators.required]),
    name: this.fb.nonNullable.control('', [Validators.required]),
    nameEn: this.fb.control<string | null>(null),
    sutCode: this.fb.control<string | null>(null),
    defaultPrice: this.fb.control<number | null>(null, [Validators.required, Validators.min(0)]),
    vatRate: this.fb.nonNullable.control(10),
    toothScope: this.fb.nonNullable.control<ToothScope>(ToothScope.PerTooth),
    requiresSurface: this.fb.nonNullable.control(false),
    toothStatusEffect: this.fb.nonNullable.control<ToothStatusEffect>(ToothStatusEffect.None),
    defaultDurationMinutes: this.fb.control<number | null>(null),
    isActive: this.fb.nonNullable.control(true),
  });

  protected readonly scopeOptions = computed(() => {
    this.langTick();
    return [
      { label: this.transloco.translate('catalog.scope.perTooth'), value: ToothScope.PerTooth },
      { label: this.transloco.translate('catalog.scope.perJaw'), value: ToothScope.PerJaw },
      { label: this.transloco.translate('catalog.scope.wholeMouth'), value: ToothScope.WholeMouth },
    ];
  });

  protected readonly effectOptions = computed(() => {
    this.langTick();
    return [
      { label: this.transloco.translate('catalog.effect.none'), value: ToothStatusEffect.None },
      {
        label: this.transloco.translate('catalog.effect.extracted'),
        value: ToothStatusEffect.Extracted,
      },
      {
        label: this.transloco.translate('catalog.effect.implant'),
        value: ToothStatusEffect.Implant,
      },
      { label: this.transloco.translate('catalog.effect.crown'), value: ToothStatusEffect.Crown },
      {
        label: this.transloco.translate('catalog.effect.rootCanal'),
        value: ToothStatusEffect.RootCanal,
      },
      { label: this.transloco.translate('catalog.effect.bridge'), value: ToothStatusEffect.Bridge },
    ];
  });

  constructor() {
    this.searchDebounce
      .pipe(debounceTime(250), takeUntilDestroyed())
      .subscribe(() => this.loadItems());
    this.loadCategories();
    this.loadItems();
  }

  // --- Yukleme --------------------------------------------------------------

  protected loadCategories(): void {
    this.api.categories().subscribe({
      next: (categories) =>
        this.categories.set([...categories].sort((a, b) => a.sortOrder - b.sortOrder)),
      error: () => this.categories.set([]),
    });
  }

  protected loadItems(): void {
    this.itemsLoading.set(true);
    this.api
      .definitions({
        search: this.searchTerm() || undefined,
        categoryId: this.selectedCategoryId(),
        page: 1,
        pageSize: 500,
      })
      .subscribe({
        next: (result) => {
          this.items.set(result.items);
          this.itemsLoading.set(false);
        },
        error: () => {
          this.items.set([]);
          this.itemsLoading.set(false);
        },
      });
  }

  protected onSearchInput(value: string): void {
    this.searchTerm.set(value);
    this.searchDebounce.next();
  }

  protected selectCategory(id: number | null): void {
    this.selectedCategoryId.set(this.selectedCategoryId() === id ? null : id);
    this.loadItems();
  }

  protected scopeLabel(scope: ToothScope): string {
    return this.scopeOptions().find((o) => o.value === scope)?.label ?? '—';
  }

  // --- Kategori CRUD --------------------------------------------------------

  protected openCategoryDialog(category: TreatmentCategoryDto | null): void {
    this.editingCategory = category;
    this.categoryForm.reset({
      name: category?.name ?? '',
      nameEn: category?.nameEn ?? null,
      colorHex: category?.colorHex ?? '#3b82f6',
      sortOrder: category?.sortOrder ?? (this.categories().length + 1),
    });
    this.categoryDialogVisible.set(true);
  }

  protected saveCategory(): void {
    if (this.categoryForm.invalid) {
      this.categoryForm.markAllAsTouched();
      return;
    }
    const v = this.categoryForm.getRawValue();
    const request = {
      name: v.name.trim(),
      nameEn: v.nameEn?.trim() || null,
      colorHex: v.colorHex,
      sortOrder: v.sortOrder,
    };
    this.categorySaving.set(true);
    const call = this.editingCategory
      ? this.api.updateCategory(this.editingCategory.id, request)
      : this.api.createCategory(request);
    call.subscribe({
      next: () => {
        this.categorySaving.set(false);
        this.categoryDialogVisible.set(false);
        this.toast('catalog.categorySaved');
        this.loadCategories();
        this.loadItems();
      },
      error: () => this.categorySaving.set(false),
    });
  }

  protected deleteCategory(category: TreatmentCategoryDto): void {
    this.confirmation.confirm({
      header: this.transloco.translate('catalog.deleteCategoryTitle'),
      message: this.transloco.translate('catalog.deleteCategoryMessage', { name: category.name }),
      icon: 'pi pi-exclamation-triangle',
      acceptButtonProps: { label: this.transloco.translate('common.delete'), severity: 'danger' },
      rejectButtonProps: {
        label: this.transloco.translate('common.cancel'),
        severity: 'secondary',
        outlined: true,
      },
      accept: () => {
        this.api.deleteCategory(category.id).subscribe({
          next: () => {
            this.toast('catalog.categoryDeleted');
            if (this.selectedCategoryId() === category.id) {
              this.selectedCategoryId.set(null);
            }
            this.loadCategories();
            this.loadItems();
          },
        });
      },
    });
  }

  // --- Tedavi CRUD ----------------------------------------------------------

  protected openDefDialog(def: TreatmentDefinitionDto | null): void {
    this.editingDef = def;
    this.defForm.reset({
      categoryId: def?.categoryId ?? this.selectedCategoryId(),
      code: def?.code ?? '',
      name: def?.name ?? '',
      nameEn: def?.nameEn ?? null,
      sutCode: def?.sutCode ?? null,
      defaultPrice: def?.defaultPrice ?? null,
      vatRate: def?.vatRate ?? 10,
      toothScope: def?.toothScope ?? ToothScope.PerTooth,
      requiresSurface: def?.requiresSurface ?? false,
      toothStatusEffect: def?.toothStatusEffect ?? ToothStatusEffect.None,
      defaultDurationMinutes: def?.defaultDurationMinutes ?? null,
      isActive: def?.isActive ?? true,
    });
    this.defDialogVisible.set(true);
  }

  protected saveDef(): void {
    if (this.defForm.invalid) {
      this.defForm.markAllAsTouched();
      return;
    }
    const v = this.defForm.getRawValue();
    const request = {
      categoryId: v.categoryId!,
      code: v.code.trim(),
      name: v.name.trim(),
      nameEn: v.nameEn?.trim() || null,
      sutCode: v.sutCode?.trim() || null,
      defaultPrice: v.defaultPrice ?? 0,
      vatRate: v.vatRate,
      toothScope: v.toothScope,
      requiresSurface: v.requiresSurface,
      toothStatusEffect: v.toothStatusEffect,
      defaultDurationMinutes: v.defaultDurationMinutes,
      isActive: v.isActive,
    };
    this.defSaving.set(true);
    const call = this.editingDef
      ? this.api.updateDefinition(this.editingDef.id, request)
      : this.api.createDefinition(request);
    call.subscribe({
      next: () => {
        this.defSaving.set(false);
        this.defDialogVisible.set(false);
        this.toast('catalog.treatmentSaved');
        this.loadCategories();
        this.loadItems();
      },
      error: () => this.defSaving.set(false),
    });
  }

  protected deleteDef(def: TreatmentDefinitionDto): void {
    this.confirmation.confirm({
      header: this.transloco.translate('catalog.deleteTreatmentTitle'),
      message: this.transloco.translate('catalog.deleteTreatmentMessage', { name: def.name }),
      icon: 'pi pi-exclamation-triangle',
      acceptButtonProps: { label: this.transloco.translate('common.delete'), severity: 'danger' },
      rejectButtonProps: {
        label: this.transloco.translate('common.cancel'),
        severity: 'secondary',
        outlined: true,
      },
      accept: () => {
        this.api.deleteDefinition(def.id).subscribe({
          next: () => {
            this.toast('catalog.treatmentDeleted');
            this.loadCategories();
            this.loadItems();
          },
        });
      },
    });
  }

  private toast(key: string): void {
    this.messageService.add({
      severity: 'success',
      summary: this.transloco.translate(key),
      life: 3000,
    });
  }
}
