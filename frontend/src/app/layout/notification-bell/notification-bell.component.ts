import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { PopoverModule } from 'primeng/popover';
import { TranslocoPipe } from '@jsverse/transloco';
import {
  NotificationDto,
  NotificationsApiService,
  notificationLink,
} from '../../core/api/notifications-api.service';
import { TrDatePipe } from '../../shared/pipes/tr-date.pipe';

/** Otomatik yenileme araligi — sekme gizliyken durur. */
const REFRESH_INTERVAL_MS = 60_000;

/**
 * Ust bardaki bildirim zili: okunmamis rozeti, son 10 bildirim paneli,
 * tikla -> ilgili sayfaya git + okundu isaretle, "Tumunu okundu isaretle".
 */
@Component({
  selector: 'app-notification-bell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ButtonModule, PopoverModule, TranslocoPipe, TrDatePipe],
  template: `
    <button
      type="button"
      class="bell"
      (click)="panel.toggle($event)"
      [attr.aria-label]="'notifications.title' | transloco"
      data-testid="notification-bell"
    >
      <i class="fa-regular fa-bell" aria-hidden="true"></i>
      @if (unreadCount() > 0) {
        <span class="bell__badge" data-testid="notification-badge">
          {{ unreadCount() > 99 ? '99+' : unreadCount() }}
        </span>
      }
    </button>

    <p-popover #panel (onShow)="refresh()">
      <div class="bell__panel" data-testid="notification-panel">
        <div class="bell__panel-head">
          <span class="bell__panel-title">{{ 'notifications.title' | transloco }}</span>
          @if (unreadCount() > 0) {
            <p-button
              [text]="true"
              size="small"
              severity="secondary"
              [label]="'notifications.markAllRead' | transloco"
              (onClick)="markAllRead()"
              data-testid="notification-mark-all"
            />
          }
        </div>

        @if (items().length === 0) {
          <p class="bell__empty">{{ 'notifications.empty' | transloco }}</p>
        } @else {
          <ul class="bell__list">
            @for (item of items(); track item.id) {
              <li>
                <button
                  type="button"
                  class="bell__item"
                  [class.bell__item--unread]="!item.readAtUtc"
                  (click)="open(item, panel)"
                  [attr.data-testid]="'notification-item-' + item.id"
                >
                  <span class="bell__item-dot" [class.bell__item-dot--on]="!item.readAtUtc"></span>
                  <span class="bell__item-body">
                    <span class="bell__item-title">{{ item.title }}</span>
                    @if (item.body) {
                      <span class="bell__item-text">{{ item.body }}</span>
                    }
                    <span class="bell__item-time">{{ item.createdAtUtc | trDate: 'dd.MM.yyyy HH:mm' }}</span>
                  </span>
                </button>
              </li>
            }
          </ul>
        }
      </div>
    </p-popover>
  `,
  styles: `
    :host {
      display: inline-flex;
    }
    .bell {
      position: relative;
      display: inline-flex;
      align-items: center;
      justify-content: center;
      width: 36px;
      height: 36px;
      border: 0;
      border-radius: 10px;
      background: transparent;
      color: #475569;
      cursor: pointer;
      font-size: 1rem;

      &:hover {
        background: #f1f5f9;
      }
    }
    .bell__badge {
      position: absolute;
      top: 2px;
      right: 1px;
      min-width: 17px;
      height: 17px;
      padding: 0 4px;
      border-radius: 999px;
      background: #ef4444;
      color: #ffffff;
      font-size: 0.65rem;
      font-weight: 700;
      line-height: 17px;
      text-align: center;
    }
    .bell__panel {
      width: 22rem;
      max-width: 90vw;
    }
    .bell__panel-head {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 0.5rem;
      padding-bottom: 0.5rem;
      border-bottom: 1px solid #e2e8f0;
    }
    .bell__panel-title {
      font-weight: 600;
      color: #0f172a;
    }
    .bell__empty {
      margin: 0;
      padding: 1.5rem 0;
      text-align: center;
      color: #94a3b8;
      font-size: 0.85rem;
    }
    .bell__list {
      list-style: none;
      margin: 0;
      padding: 0;
      max-height: 22rem;
      overflow: auto;
    }
    .bell__item {
      display: flex;
      gap: 0.55rem;
      width: 100%;
      padding: 0.6rem 0.4rem;
      border: 0;
      border-bottom: 1px solid #f1f5f9;
      background: transparent;
      text-align: left;
      cursor: pointer;
      font-family: inherit;

      &:hover {
        background: #f8fafc;
      }

      &--unread {
        background: #eff6ff;

        &:hover {
          background: #dbeafe;
        }
      }
    }
    .bell__item-dot {
      flex: 0 0 auto;
      width: 7px;
      height: 7px;
      margin-top: 0.4rem;
      border-radius: 50%;
      background: transparent;

      &--on {
        background: #3b82f6;
      }
    }
    .bell__item-body {
      display: flex;
      flex-direction: column;
      gap: 0.1rem;
      min-width: 0;
    }
    .bell__item-title {
      font-size: 0.85rem;
      font-weight: 600;
      color: #1e293b;
    }
    .bell__item-text {
      font-size: 0.78rem;
      color: #64748b;
    }
    .bell__item-time {
      font-size: 0.7rem;
      color: #94a3b8;
    }
  `,
})
export class NotificationBellComponent implements OnInit {
  private readonly api = inject(NotificationsApiService);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly items = signal<NotificationDto[]>([]);
  protected readonly unreadCount = signal(0);

  private timer: ReturnType<typeof setInterval> | null = null;

  ngOnInit(): void {
    this.refresh();
    this.startTimer();
    document.addEventListener('visibilitychange', this.onVisibilityChange);
    this.destroyRef.onDestroy(() => {
      this.stopTimer();
      document.removeEventListener('visibilitychange', this.onVisibilityChange);
    });
  }

  protected refresh(): void {
    this.api.list(false, 1, 10).subscribe({
      next: (result) => {
        this.items.set(result.page.items);
        this.unreadCount.set(result.unreadCount);
      },
      error: () => undefined,
    });
  }

  protected markAllRead(): void {
    this.api.markAllRead().subscribe({
      next: () => {
        const now = new Date().toISOString();
        this.items.update((items) => items.map((i) => ({ ...i, readAtUtc: i.readAtUtc ?? now })));
        this.unreadCount.set(0);
      },
      error: () => undefined,
    });
  }

  protected open(item: NotificationDto, panel: { hide: () => void }): void {
    if (!item.readAtUtc) {
      this.api.markRead(item.id).subscribe({ error: () => undefined });
      const now = new Date().toISOString();
      this.items.update((items) =>
        items.map((i) => (i.id === item.id ? { ...i, readAtUtc: now } : i)),
      );
      this.unreadCount.update((count) => Math.max(0, count - 1));
    }
    const target = notificationLink(item.linkPath);
    panel.hide();
    if (target) {
      void this.router.navigate([target.path], { queryParams: target.queryParams });
    }
  }

  /** Sekme gizliyken gereksiz istek atilmaz. */
  private readonly onVisibilityChange = (): void => {
    if (document.hidden) {
      this.stopTimer();
    } else {
      this.refresh();
      this.startTimer();
    }
  };

  private startTimer(): void {
    this.stopTimer();
    this.timer = setInterval(() => this.refresh(), REFRESH_INTERVAL_MS);
  }

  private stopTimer(): void {
    if (this.timer) {
      clearInterval(this.timer);
      this.timer = null;
    }
  }
}
