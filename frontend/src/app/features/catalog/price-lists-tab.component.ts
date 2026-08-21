import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { DatePickerModule } from 'primeng/datepicker';
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
import { PriceListDto, PriceListItemDto } from '../../core/api/treatment-api.models';
import { fromDateOnly, toDateOnly } from '../../core/api/api.models';
import { TrDatePipe } from '../../shared/pipes/tr-date.pipe';

const CURRENCIES = ['TRY', 'USD', 'EUR'];

interface EditableItem extends PriceListItemDto {
  originalPrice: number;
}

/** Fiyat listeleri sekmesi: tarife listesi (CRUD + varsayilan yap) + kalem tablosu (satir ici duzenleme). */
@Component({
  selector: 'app-price-lists-tab',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    ReactiveFormsModule,
    ButtonModule,
    CheckboxModule,
    DatePickerModule,
    DialogModule,
    InputNumberModule,
    InputTextModule,
    SelectModule,
    TableModule,
    TagModule,
    TooltipModule,
    TranslocoPipe,
    HasPermissionDirective,
    TrDatePipe,
  ],
  templateUrl: './price-lists-tab.component.html',
  styleUrl: './price-lists-tab.component.scss',
})
export class PriceListsTabComponent {
  private readonly api = inject(CatalogApiService);
  private readonly messageService = inject(MessageService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly transloco = inject(TranslocoService);
  private readonly fb = inject(FormBuilder);

  protected readonly currencyOptions = CURRENCIES.map((c) => ({ label: c, value: c }));

  protected readonly lists = signal<PriceListDto[]>([]);
  protected readonly selectedList = signal<PriceListDto | null>(null);
  protected readonly items = signal<EditableItem[]>([]);
  protected readonly itemsLoading = signal(false);
  protected readonly itemsSaving = signal(false);
  protected itemFilter = '';

  protected readonly dialogVisible = signal(false);
  protected readonly dialogSaving = signal(false);
  protected editingList: PriceListDto | null = null;
  protected readonly listForm = this.fb.group({
    name: this.fb.nonNullable.control('', [Validators.required]),
    currencyCode: this.fb.nonNullable.control('TRY'),
    validFrom: this.fb.control<Date | null>(null),
    isDefault: this.fb.nonNullable.control(false),
  });

  constructor() {
    this.loadLists(true);
  }

  protected loadLists(selectFirst = false): void {
    this.api.priceLists().subscribe({
      next: (lists) => {
        this.lists.set(lists);
        const current = this.selectedList();
        if (current) {
          this.selectedList.set(lists.find((l) => l.id === current.id) ?? null);
        } else if (selectFirst && lists.length) {
          this.selectList(lists.find((l) => l.isDefault) ?? lists[0]);
        }
      },
      error: () => this.lists.set([]),
    });
  }

  protected selectList(list: PriceListDto): void {
    this.selectedList.set(list);
    this.loadItems(list.id);
  }

  private loadItems(listId: number): void {
    this.itemsLoading.set(true);
    this.api.priceListItems(listId).subscribe({
      next: (items) => {
        this.items.set(items.map((i) => ({ ...i, originalPrice: i.price })));
        this.itemsLoading.set(false);
      },
      error: () => {
        this.items.set([]);
        this.itemsLoading.set(false);
      },
    });
  }

  protected filteredItems(): EditableItem[] {
    const filter = this.itemFilter.trim().toLocaleLowerCase('tr');
    const items = this.items();
    return filter
      ? items.filter(
          (i) =>
            i.treatmentName.toLocaleLowerCase('tr').includes(filter) ||
            i.treatmentCode.toLocaleLowerCase('tr').includes(filter),
        )
      : items;
  }

  protected dirtyCount(): number {
    return this.items().filter((i) => i.price !== i.originalPrice).length;
  }

  protected saveItems(): void {
    const list = this.selectedList();
    if (!list) {
      return;
    }
    this.itemsSaving.set(true);
    this.api
      .savePriceListItems(list.id, {
        items: this.items().map((i) => ({
          treatmentDefinitionId: i.treatmentDefinitionId,
          price: i.price ?? 0,
        })),
      })
      .subscribe({
        next: (items) => {
          this.items.set(items.map((i) => ({ ...i, originalPrice: i.price })));
          this.itemsSaving.set(false);
          this.toast('catalog.priceItemsSaved');
          this.loadLists();
        },
        error: () => this.itemsSaving.set(false),
      });
  }

  // --- Tarife CRUD ----------------------------------------------------------

  protected openDialog(list: PriceListDto | null): void {
    this.editingList = list;
    this.listForm.reset({
      name: list?.name ?? '',
      currencyCode: list?.currencyCode?.trim() || 'TRY',
      validFrom: fromDateOnly(list?.validFrom ?? null),
      isDefault: list?.isDefault ?? false,
    });
    this.dialogVisible.set(true);
  }

  protected saveList(): void {
    if (this.listForm.invalid) {
      this.listForm.markAllAsTouched();
      return;
    }
    const v = this.listForm.getRawValue();
    const request = {
      name: v.name.trim(),
      currencyCode: v.currencyCode,
      validFrom: v.validFrom ? toDateOnly(v.validFrom) : null,
      isDefault: v.isDefault,
    };
    this.dialogSaving.set(true);
    const call = this.editingList
      ? this.api.updatePriceList(this.editingList.id, request)
      : this.api.createPriceList(request);
    call.subscribe({
      next: (saved) => {
        this.dialogSaving.set(false);
        this.dialogVisible.set(false);
        this.toast('catalog.priceListSaved');
        if (!this.editingList) {
          this.selectedList.set(saved);
          this.loadItems(saved.id);
        }
        this.loadLists();
      },
      error: () => this.dialogSaving.set(false),
    });
  }

  protected makeDefault(list: PriceListDto): void {
    this.api
      .updatePriceList(list.id, {
        name: list.name,
        currencyCode: list.currencyCode?.trim() || 'TRY',
        validFrom: list.validFrom,
        isDefault: true,
      })
      .subscribe({
        next: () => {
          this.toast('catalog.defaultSet');
          this.loadLists();
        },
      });
  }

  protected deleteList(list: PriceListDto): void {
    this.confirmation.confirm({
      header: this.transloco.translate('catalog.deletePriceListTitle'),
      message: this.transloco.translate('catalog.deletePriceListMessage', { name: list.name }),
      icon: 'pi pi-exclamation-triangle',
      acceptButtonProps: { label: this.transloco.translate('common.delete'), severity: 'danger' },
      rejectButtonProps: {
        label: this.transloco.translate('common.cancel'),
        severity: 'secondary',
        outlined: true,
      },
      accept: () => {
        this.api.deletePriceList(list.id).subscribe({
          next: () => {
            this.toast('catalog.priceListDeleted');
            if (this.selectedList()?.id === list.id) {
              this.selectedList.set(null);
              this.items.set([]);
            }
            this.loadLists(true);
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
