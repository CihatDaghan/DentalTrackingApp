import {
  ChangeDetectionStrategy,
  Component,
  contentChild,
  effect,
  input,
  output,
  signal,
  TemplateRef,
  untracked,
} from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { Observable, Subscription } from 'rxjs';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { inject } from '@angular/core';
import { PagedResult, TableQuery } from '../../../core/api/api.models';

export interface TableColumn {
  field: string;
  headerKey: string;
  /** Doluysa kolon siralanabilir; deger API'nin sort alan adidir. */
  sortField?: string;
  width?: string;
  /** Baslik hizasi — sayisal/aksiyon kolonlarinda hucrelerle ayni tarafa alinir. */
  align?: 'left' | 'right' | 'center';
}

/**
 * p-table sarmalayicisi: server-side sayfalama/siralama, TR yerellestirme, bos durum.
 * Satir hucreleri `#rowCells` sablonuyla projekte edilir (context.$implicit = satir).
 */
@Component({
  selector: 'app-table',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TableModule, NgTemplateOutlet, TranslocoPipe],
  templateUrl: './app-table.component.html',
  styleUrl: './app-table.component.scss',
})
export class AppTableComponent<T> {
  private readonly transloco = inject(TranslocoService);

  readonly columns = input.required<TableColumn[]>();
  readonly loader = input.required<(query: TableQuery) => Observable<PagedResult<T>>>();
  readonly pageSize = input(25);
  /** Global arama metni — degisince ilk sayfaya donup yeniden yukler (debounce cagiranin isi). */
  readonly search = input<string>('');
  /** Satir genisletme icin benzersiz alan adi (yalniz `#rowExpansion` verildiginde anlamli). */
  readonly dataKey = input<string>('id');
  /** Acik satirlarin anahtar haritasi — genisletmeyi cagiran bilesen yonetir. */
  readonly expandedRowKeys = input<Record<string, boolean>>({});
  readonly rowClick = output<T>();

  protected readonly rowCells = contentChild<TemplateRef<{ $implicit: T }>>('rowCells');
  /** Opsiyonel: satir altinda tam genislikte acilan detay sablonu. */
  protected readonly rowExpansion = contentChild<TemplateRef<{ $implicit: T }>>('rowExpansion');

  protected readonly rows = signal<T[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly loading = signal(false);
  protected readonly first = signal(0);

  protected readonly reportTemplate = () =>
    this.transloco.translate('table.pageReport');

  private lastEvent: TableLazyLoadEvent = { first: 0, rows: this.pageSize() };
  private pending?: Subscription;
  private initialized = false;

  constructor() {
    effect(() => {
      this.search(); // izlenen tek sinyal
      if (!this.initialized) {
        return;
      }
      untracked(() => this.reload(true));
    });
  }

  /** Parent'in yeniden yukleme tetigi (or. kayit sonrasi). */
  reload(resetPage = false): void {
    if (resetPage) {
      this.lastEvent = { ...this.lastEvent, first: 0 };
      this.first.set(0);
    }
    this.fetch(this.lastEvent);
  }

  protected onLazyLoad(event: TableLazyLoadEvent): void {
    this.initialized = true;
    this.lastEvent = event;
    this.fetch(event);
  }

  private fetch(event: TableLazyLoadEvent): void {
    const pageSize = event.rows ?? this.pageSize();
    const page = Math.floor((event.first ?? 0) / pageSize) + 1;
    const sortField = Array.isArray(event.sortField) ? event.sortField[0] : event.sortField;
    const query: TableQuery = {
      page,
      pageSize,
      sort: sortField ? `${event.sortOrder === -1 ? '-' : ''}${sortField}` : undefined,
      search: this.search() || undefined,
    };
    this.pending?.unsubscribe();
    this.loading.set(true);
    this.pending = this.loader()(query).subscribe({
      next: (result) => {
        this.rows.set(result.items);
        this.totalCount.set(result.totalCount);
        this.loading.set(false);
      },
      error: () => {
        this.rows.set([]);
        this.totalCount.set(0);
        this.loading.set(false);
      },
    });
  }
}
