import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
  untracked,
  viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { forkJoin, map, Observable, of, switchMap } from 'rxjs';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { SelectModule } from 'primeng/select';
import { TooltipModule } from 'primeng/tooltip';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { MessagingApiService } from '../../../core/api/messaging-api.service';
import {
  MESSAGE_SKIP_REASON_KEYS,
  MESSAGE_STATE_KEYS,
  MessageChannel,
  OutboundMessageDto,
  OutboundMessageState,
  PAYMENT_LINK_TEMPLATE_KEY,
} from '../../../core/api/messaging-api.models';
import { PagedResult, TableQuery, toDateOnly } from '../../../core/api/api.models';
import {
  AppTableComponent,
  TableColumn,
} from '../../../shared/components/app-table/app-table.component';
import {
  PatientOption,
  PatientSearchSelectComponent,
} from '../../../shared/components/patient-search-select/patient-search-select.component';
import { StatusTagComponent } from '../../../shared/components/status-tag/status-tag.component';
import { MoneyPipe } from '../../../shared/pipes/money.pipe';
import { TrDatePipe } from '../../../shared/pipes/tr-date.pipe';
import { injectTranslationSignal } from '../../../shared/utils/transloco-signal';
import { CHANNEL_ICONS, CHANNEL_LABEL_KEYS, MESSAGE_CHANNELS } from '../messaging-options';

/** Arka uc `templateKey` filtresi sunmadigi icin hizli filtre en fazla bu kadar kayit tarar. */
const SCAN_PAGE_SIZE = 100;
const MAX_SCAN_PAGES = 5;

/**
 * Gonderim gecmisi: kanal/durum/tarih/hasta filtreleri + "Odeme linkleri" hizli filtresi.
 * Satir genisletince mesaj govdesi, hata, deneme sayisi, saglayici kimligi ve
 * WhatsApp -> SMS fallback zinciri gorunur.
 */
@Component({
  selector: 'app-message-history',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    ButtonModule,
    DatePickerModule,
    SelectModule,
    TooltipModule,
    TranslocoPipe,
    AppTableComponent,
    PatientSearchSelectComponent,
    StatusTagComponent,
    MoneyPipe,
    TrDatePipe,
  ],
  templateUrl: './message-history.component.html',
})
export class MessageHistoryComponent {
  private readonly api = inject(MessagingApiService);
  private readonly transloco = inject(TranslocoService);
  private readonly translation = injectTranslationSignal();

  /** Dashboard "Basarisiz mesajlar" sayacindan gelen derin baglanti. */
  readonly initialState = input<number | null>(null);
  readonly initialTemplateKey = input<string | null>(null);

  protected readonly MessageChannel = MessageChannel;
  protected readonly channelIcons = CHANNEL_ICONS;

  private readonly table = viewChild(AppTableComponent<OutboundMessageDto>);

  protected readonly filterChannel = signal<MessageChannel | null>(null);
  protected readonly filterState = signal<OutboundMessageState | null>(null);
  protected readonly filterFrom = signal<Date | null>(null);
  protected readonly filterTo = signal<Date | null>(null);
  protected readonly filterPatient = signal<PatientOption | null>(null);
  /** "Odeme linkleri" hizli filtresi (templateKey = payment_link). */
  protected readonly filterTemplateKey = signal<string | null>(null);

  protected readonly expandedRowKeys = signal<Record<string, boolean>>({});
  /** Genisletilen satirin fallback ustu (parent) mesaji — sadece gerektiginde cekilir. */
  protected readonly parents = signal<Record<number, OutboundMessageDto>>({});
  /** Sayfadaki satirlardan turetilen "bu mesajin SMS yedegi su mesaj" haritasi. */
  private readonly loadedRows = signal<OutboundMessageDto[]>([]);

  protected readonly columns: TableColumn[] = [
    { field: 'expander', headerKey: 'messaging.history.col.expander', width: '3rem' },
    { field: 'createdAtUtc', headerKey: 'messaging.history.col.date', width: '10rem' },
    { field: 'channel', headerKey: 'messaging.history.col.channel', width: '8rem' },
    { field: 'toAddress', headerKey: 'messaging.history.col.recipient' },
    { field: 'templateKey', headerKey: 'messaging.history.col.template', width: '12rem' },
    { field: 'kind', headerKey: 'messaging.history.col.kind', width: '9rem' },
    { field: 'state', headerKey: 'messaging.history.col.state', width: '9rem' },
    { field: 'creditCost', headerKey: 'messaging.history.col.cost', width: '7rem', align: 'right' },
  ];

  protected readonly channelOptions = computed(() => {
    this.translation();
    return MESSAGE_CHANNELS.map((value) => ({
      label: this.transloco.translate(CHANNEL_LABEL_KEYS[value]),
      value,
    }));
  });

  protected readonly stateOptions = computed(() => {
    this.translation();
    return Object.values(OutboundMessageState).map((value) => ({
      label: this.transloco.translate('messageState.' + MESSAGE_STATE_KEYS[value]),
      value,
    }));
  });

  protected readonly hasFilters = computed(
    () =>
      this.filterChannel() !== null ||
      this.filterState() !== null ||
      this.filterFrom() !== null ||
      this.filterTo() !== null ||
      this.filterPatient() !== null ||
      this.filterTemplateKey() !== null,
  );

  /**
   * Tablo ancak baslangic filtreleri uygulandiktan sonra olusturulur; boylece
   * app-table'in ilk lazy yuklemesi dogru filtreyle gider (sonradan reload gerekmez).
   */
  protected readonly ready = signal(false);

  /** En son uygulanan derin baglanti degerleri — ayni deger tekrar dayatilmaz. */
  private appliedState: number | null | undefined;
  private appliedTemplateKey: string | null | undefined;

  constructor() {
    // Derin baglanti (?state=5 / ?templateKey=payment_link) filtreye yansir.
    effect(() => {
      const state = this.initialState();
      const templateKey = this.initialTemplateKey();
      untracked(() => {
        // Yalniz gercekten degisen degerler uygulanir: kullanici filtreyi
        // temizledikten sonra sekme degistirince URL'deki eski deger geri gelmesin.
        let changed = false;
        if (state !== this.appliedState) {
          this.appliedState = state;
          this.filterState.set(state as OutboundMessageState | null);
          changed = true;
        }
        if (templateKey !== this.appliedTemplateKey) {
          this.appliedTemplateKey = templateKey;
          this.filterTemplateKey.set(templateKey);
          changed = true;
        }
        if (!this.ready()) {
          this.ready.set(true);
        } else if (changed) {
          this.table()?.reload(true);
        }
      });
    });
  }

  protected readonly loader = (query: TableQuery): Observable<PagedResult<OutboundMessageDto>> => {
    const base = {
      channel: this.filterChannel(),
      state: this.filterState(),
      patientId: this.filterPatient()?.id ?? null,
      from: this.filterFrom() ? toDateOnly(this.filterFrom() as Date) : null,
      to: this.filterTo() ? toDateOnly(this.filterTo() as Date) : null,
    };
    const templateKey = this.filterTemplateKey();

    const source: Observable<PagedResult<OutboundMessageDto>> = templateKey
      ? this.scanByTemplateKey(base, templateKey, query)
      : this.api.messages({ ...base, page: query.page, pageSize: query.pageSize });

    return source.pipe(
      map((result) => {
        this.loadedRows.set(result.items);
        return result;
      }),
    );
  };

  /**
   * Arka ucta templateKey filtresi yok: en fazla 500 kaydi tarayip istemcide suzer.
   * Daha eski odeme linkleri icin tarih araligi filtresi kullanilmalidir.
   */
  private scanByTemplateKey(
    base: Record<string, unknown>,
    templateKey: string,
    query: TableQuery,
  ): Observable<PagedResult<OutboundMessageDto>> {
    const scan = (page: number) =>
      this.api.messages({ ...base, page, pageSize: SCAN_PAGE_SIZE });

    return scan(1).pipe(
      switchMap((first) => {
        const pageCount = Math.min(
          Math.ceil(first.totalCount / SCAN_PAGE_SIZE) || 1,
          MAX_SCAN_PAGES,
        );
        if (pageCount <= 1) {
          return of([first]);
        }
        const rest = Array.from({ length: pageCount - 1 }, (_, i) => scan(i + 2));
        return forkJoin(rest).pipe(map((pages) => [first, ...pages]));
      }),
      map((pages) => {
        const filtered = pages
          .flatMap((p) => p.items)
          .filter((m) => m.templateKey === templateKey);
        const start = (query.page - 1) * query.pageSize;
        return {
          items: filtered.slice(start, start + query.pageSize),
          page: query.page,
          pageSize: query.pageSize,
          totalCount: filtered.length,
        };
      }),
    );
  }

  protected reload(): void {
    this.expandedRowKeys.set({});
    this.parents.set({});
    this.table()?.reload(true);
  }

  protected togglePaymentLinks(): void {
    this.filterTemplateKey.set(
      this.filterTemplateKey() ? null : PAYMENT_LINK_TEMPLATE_KEY,
    );
    this.reload();
  }

  protected clearFilters(): void {
    this.filterChannel.set(null);
    this.filterState.set(null);
    this.filterFrom.set(null);
    this.filterTo.set(null);
    this.filterPatient.set(null);
    this.filterTemplateKey.set(null);
    this.reload();
  }

  protected isExpanded(row: OutboundMessageDto): boolean {
    return !!this.expandedRowKeys()[String(row.id)];
  }

  protected toggleExpand(row: OutboundMessageDto, event?: Event): void {
    event?.stopPropagation();
    const key = String(row.id);
    this.expandedRowKeys.update((keys) => {
      const next = { ...keys };
      if (next[key]) {
        delete next[key];
      } else {
        next[key] = true;
      }
      return next;
    });
    // Fallback zincirini gosterebilmek icin ust mesaji (WhatsApp denemesi) getir.
    const parentId = row.fallbackOfMessageId;
    if (this.expandedRowKeys()[key] && parentId != null && !this.parents()[parentId]) {
      this.api.message(parentId).subscribe({
        next: (parent) => this.parents.update((map) => ({ ...map, [parentId]: parent })),
      });
    }
  }

  /** Bu mesaj icin uretilmis SMS yedegi (ayni sayfada yuklendiyse). */
  protected fallbackChild(row: OutboundMessageDto): OutboundMessageDto | null {
    return this.loadedRows().find((m) => m.fallbackOfMessageId === row.id) ?? null;
  }

  protected parentOf(row: OutboundMessageDto): OutboundMessageDto | null {
    const id = row.fallbackOfMessageId;
    return id == null ? null : (this.parents()[id] ?? null);
  }

  protected skipReasonLabel(row: OutboundMessageDto): string {
    if (row.skipReason == null) {
      return '';
    }
    const key = MESSAGE_SKIP_REASON_KEYS[row.skipReason] ?? 'unknown';
    return this.transloco.translate('messaging.skipReason.' + key);
  }

  protected channelLabel(channel: MessageChannel): string {
    return this.transloco.translate(CHANNEL_LABEL_KEYS[channel] ?? 'messaging.channel.sms');
  }
}
