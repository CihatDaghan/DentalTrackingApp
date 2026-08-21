import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
  untracked,
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DialogModule } from 'primeng/dialog';
import { InputNumberModule } from 'primeng/inputnumber';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { SelectButtonModule } from 'primeng/selectbutton';
import { TableModule } from 'primeng/table';
import { TextareaModule } from 'primeng/textarea';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmationService, MessageService } from 'primeng/api';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { StockApiService } from '../../core/api/stock-api.service';
import {
  STOCK_DIRECTION_KEYS,
  STOCK_REF_TYPE_KEYS,
  STOCK_UNIT_KEYS,
  StockCategoryDto,
  StockItemDto,
  StockMovementDirection,
  StockMovementDto,
  StockMovementRefType,
} from '../../core/api/stock-api.models';
import { ClinicContext } from '../../core/auth/clinic-context';
import { HasPermissionDirective } from '../../core/auth/has-permission.directive';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { MoneyPipe } from '../../shared/pipes/money.pipe';
import { TrDatePipe } from '../../shared/pipes/tr-date.pipe';
import { injectTranslationSignal } from '../../shared/utils/transloco-signal';

/**
 * Stok sayfasi (/app/inventory): kategori yan paneli + malzeme tablosu
 * (kritik seviye rozeti) + hareket ekleme dialogu + satirda hareket gecmisi.
 */
@Component({
  selector: 'app-inventory-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    ButtonModule,
    CheckboxModule,
    DialogModule,
    InputNumberModule,
    InputTextModule,
    SelectModule,
    SelectButtonModule,
    TableModule,
    TextareaModule,
    TooltipModule,
    TranslocoPipe,
    HasPermissionDirective,
    PageHeaderComponent,
    MoneyPipe,
    TrDatePipe,
  ],
  templateUrl: './inventory-page.component.html',
  styleUrl: './inventory-page.component.scss',
})
export class InventoryPageComponent {
  private readonly api = inject(StockApiService);
  private readonly clinicContext = inject(ClinicContext);
  private readonly messageService = inject(MessageService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly transloco = inject(TranslocoService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  /** Ceviriler yuklendiginde/dil degistiginde secenek listeleri yeniden hesaplansin. */
  private readonly translation = injectTranslationSignal();

  protected readonly directionKeys = STOCK_DIRECTION_KEYS;
  protected readonly refTypeKeys = STOCK_REF_TYPE_KEYS;
  protected readonly StockMovementDirection = StockMovementDirection;

  protected readonly items = signal<StockItemDto[]>([]);
  protected readonly categories = signal<StockCategoryDto[]>([]);
  protected readonly lowItems = signal<StockItemDto[]>([]);
  protected readonly loading = signal(false);

  // Filtreler
  protected readonly search = signal('');
  protected readonly selectedCategoryId = signal<number | null>(null);
  protected readonly includeInactive = signal(false);
  protected readonly lowOnly = signal(false);

  // Kategori dialogu
  protected readonly categoryFormVisible = signal(false);
  protected readonly categorySaving = signal(false);
  protected readonly editingCategory = signal<StockCategoryDto | null>(null);
  protected readonly categoryName = signal('');

  // Malzeme dialogu
  protected readonly itemFormVisible = signal(false);
  protected readonly itemSaving = signal(false);
  protected readonly editingItem = signal<StockItemDto | null>(null);
  protected readonly itemName = signal('');
  protected readonly itemCategoryId = signal<number | null>(null);
  protected readonly itemBarcode = signal('');
  protected readonly itemUnit = signal<string | null>(null);
  protected readonly itemMinQty = signal<number | null>(0);
  protected readonly itemIsActive = signal(true);

  // Hareket dialogu
  protected readonly movementVisible = signal(false);
  protected readonly movementSaving = signal(false);
  protected readonly movementTarget = signal<StockItemDto | null>(null);
  protected readonly movementDirection = signal<StockMovementDirection>(StockMovementDirection.In);
  protected readonly movementQty = signal<number | null>(1);
  protected readonly movementUnitCost = signal<number | null>(null);
  protected readonly movementRefType = signal<StockMovementRefType>(StockMovementRefType.Purchase);
  protected readonly movementNote = signal('');

  /** Genisletilen satirin hareket gecmisi (malzeme id -> kayitlar). */
  protected readonly movements = signal<Record<number, StockMovementDto[]>>({});

  protected readonly unitOptions = computed(() => {
    this.translation();
    return STOCK_UNIT_KEYS.map((key) => this.transloco.translate('inventory.unit.' + key));
  });

  protected readonly categoryOptions = computed(() =>
    this.categories().map((c) => ({ label: c.name, value: c.id })),
  );

  protected readonly directionOptions = computed(() => {
    this.translation();
    return [
      StockMovementDirection.In,
      StockMovementDirection.Out,
      StockMovementDirection.Adjustment,
    ].map((value) => ({
      label: this.transloco.translate('inventory.direction.' + STOCK_DIRECTION_KEYS[value]),
      value,
    }));
  });

  protected readonly refTypeOptions = computed(() => {
    this.translation();
    return [
      StockMovementRefType.Purchase,
      StockMovementRefType.TreatmentUse,
      StockMovementRefType.Waste,
      StockMovementRefType.Count,
    ].map((value) => ({
      label: this.transloco.translate('inventory.refType.' + STOCK_REF_TYPE_KEYS[value]),
      value,
    }));
  });

  /** "Kritik seviyedekiler" filtresi acikken tabloda yalniz dusuk stok gorunur. */
  protected readonly visibleItems = computed(() =>
    this.lowOnly() ? this.items().filter((i) => i.isLow) : this.items(),
  );

  protected readonly lowCount = computed(() => this.lowItems().length);

  constructor() {
    // Dashboard'dan "dusuk stok" linki ?lowOnly=true ile gelir.
    effect(() => {
      const low = this.route.snapshot.queryParamMap.get('lowOnly') === 'true';
      untracked(() => {
        if (low) {
          this.lowOnly.set(true);
        }
        this.loadCategories();
        this.load();
      });
    });
  }

  private loadCategories(): void {
    this.api.categories().subscribe({
      next: (categories) => this.categories.set(categories),
      error: () => this.categories.set([]),
    });
  }

  protected load(): void {
    this.loading.set(true);
    this.api
      .items({
        search: this.search().trim() || null,
        categoryId: this.selectedCategoryId(),
        includeInactive: this.includeInactive() || null,
      })
      .subscribe({
        next: (items) => {
          this.items.set(items);
          this.movements.set({});
          this.loading.set(false);
        },
        error: () => {
          this.items.set([]);
          this.loading.set(false);
        },
      });
    this.api.lowItems().subscribe({
      next: (items) => this.lowItems.set(items),
      error: () => this.lowItems.set([]),
    });
  }

  protected selectCategory(id: number | null): void {
    this.selectedCategoryId.set(id);
    this.load();
  }

  protected toggleLowOnly(value: boolean): void {
    this.lowOnly.set(value);
    if (!value) {
      void this.router.navigate([], { relativeTo: this.route, queryParams: {} });
    }
  }

  // --- Kategori CRUD --------------------------------------------------------

  protected openCategoryForm(category: StockCategoryDto | null): void {
    this.editingCategory.set(category);
    this.categoryName.set(category?.name ?? '');
    this.categoryFormVisible.set(true);
  }

  protected saveCategory(): void {
    const name = this.categoryName().trim();
    if (!name) {
      return;
    }
    this.categorySaving.set(true);
    const editing = this.editingCategory();
    const call = editing
      ? this.api.updateCategory(editing.id, { name })
      : this.api.createCategory({ name });
    call.subscribe({
      next: () => {
        this.categorySaving.set(false);
        this.categoryFormVisible.set(false);
        this.messageService.add({
          severity: 'success',
          summary: this.transloco.translate('inventory.categorySaved'),
          life: 3000,
        });
        this.loadCategories();
        this.load();
      },
      error: () => this.categorySaving.set(false),
    });
  }

  protected removeCategory(category: StockCategoryDto): void {
    this.confirmation.confirm({
      header: this.transloco.translate('inventory.deleteCategoryTitle'),
      message: this.transloco.translate('inventory.deleteCategoryMessage'),
      icon: 'pi pi-exclamation-triangle',
      acceptButtonProps: { label: this.transloco.translate('common.delete'), severity: 'danger' },
      rejectButtonProps: {
        label: this.transloco.translate('common.cancel'),
        severity: 'secondary',
        outlined: true,
      },
      accept: () =>
        this.api.deleteCategory(category.id).subscribe({
          next: () => {
            if (this.selectedCategoryId() === category.id) {
              this.selectedCategoryId.set(null);
            }
            this.loadCategories();
            this.load();
          },
        }),
    });
  }

  // --- Malzeme CRUD ---------------------------------------------------------

  protected openItemForm(item: StockItemDto | null): void {
    this.editingItem.set(item);
    this.itemName.set(item?.name ?? '');
    this.itemCategoryId.set(item?.categoryId ?? this.selectedCategoryId());
    this.itemBarcode.set(item?.barcode ?? '');
    this.itemUnit.set(item?.unit ?? null);
    this.itemMinQty.set(item?.minQty ?? 0);
    this.itemIsActive.set(item?.isActive ?? true);
    this.itemFormVisible.set(true);
  }

  protected saveItem(): void {
    const name = this.itemName().trim();
    if (!name) {
      return;
    }
    const request = {
      name,
      categoryId: this.itemCategoryId(),
      barcode: this.itemBarcode().trim() || null,
      unit: this.itemUnit() || null,
      minQty: this.itemMinQty() ?? 0,
      isActive: this.itemIsActive(),
      clinicId: this.clinicContext.clinicId(),
    };
    this.itemSaving.set(true);
    const editing = this.editingItem();
    const call = editing ? this.api.updateItem(editing.id, request) : this.api.createItem(request);
    call.subscribe({
      next: () => {
        this.itemSaving.set(false);
        this.itemFormVisible.set(false);
        this.messageService.add({
          severity: 'success',
          summary: this.transloco.translate('inventory.itemSaved'),
          life: 3000,
        });
        this.load();
      },
      error: () => this.itemSaving.set(false),
    });
  }

  protected removeItem(item: StockItemDto): void {
    this.confirmation.confirm({
      header: this.transloco.translate('inventory.deleteItemTitle'),
      message: this.transloco.translate('inventory.deleteItemMessage'),
      icon: 'pi pi-exclamation-triangle',
      acceptButtonProps: { label: this.transloco.translate('common.delete'), severity: 'danger' },
      rejectButtonProps: {
        label: this.transloco.translate('common.cancel'),
        severity: 'secondary',
        outlined: true,
      },
      accept: () => this.api.deleteItem(item.id).subscribe({ next: () => this.load() }),
    });
  }

  // --- Hareket --------------------------------------------------------------

  protected openMovement(item: StockItemDto): void {
    this.movementTarget.set(item);
    this.movementDirection.set(StockMovementDirection.In);
    this.movementQty.set(1);
    this.movementUnitCost.set(item.lastPurchasePrice);
    this.movementRefType.set(StockMovementRefType.Purchase);
    this.movementNote.set('');
    this.movementVisible.set(true);
  }

  /** Yon degisince varsayilan kaynak turunu esle (Giris->Alis, Cikis->Tedavi, Sayim->Sayim). */
  protected onDirectionChange(direction: StockMovementDirection): void {
    this.movementDirection.set(direction);
    this.movementRefType.set(
      direction === StockMovementDirection.In
        ? StockMovementRefType.Purchase
        : direction === StockMovementDirection.Out
          ? StockMovementRefType.TreatmentUse
          : StockMovementRefType.Count,
    );
  }

  protected saveMovement(): void {
    const item = this.movementTarget();
    const qty = this.movementQty();
    if (!item || qty == null || qty <= 0) {
      return;
    }
    this.movementSaving.set(true);
    this.api
      .addMovement(item.id, {
        direction: this.movementDirection(),
        qty,
        unitCost: this.movementUnitCost(),
        refType: this.movementRefType(),
        note: this.movementNote().trim() || null,
      })
      .subscribe({
        next: (updated) => {
          this.movementSaving.set(false);
          this.movementVisible.set(false);
          this.messageService.add({
            severity: 'success',
            summary: this.transloco.translate('inventory.movementSaved', {
              qty,
              name: updated.name,
            }),
            life: 3000,
          });
          this.items.update((list) => list.map((i) => (i.id === updated.id ? updated : i)));
          this.movements.update((map) => {
            const next = { ...map };
            delete next[updated.id];
            return next;
          });
          this.api.lowItems().subscribe({ next: (items) => this.lowItems.set(items) });
        },
        error: () => this.movementSaving.set(false),
      });
  }

  protected onRowExpand(item: StockItemDto): void {
    if (this.movements()[item.id]) {
      return;
    }
    this.api.movements(item.id).subscribe({
      next: (list) =>
        this.movements.update((map) => ({
          ...map,
          [item.id]: [...list].sort((a, b) => b.movedAtUtc.localeCompare(a.movedAtUtc)),
        })),
    });
  }

  protected movementsOf(id: number): StockMovementDto[] {
    return this.movements()[id] ?? [];
  }
}
