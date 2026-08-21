import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import {
  ActiveAnnouncementDto,
  AnnouncementSeverity,
  NotificationsApiService,
} from '../../core/api/notifications-api.service';

const DISMISSED_KEY = 'dentaltrack.announcements.dismissed';

function readDismissed(): number[] {
  try {
    const raw = localStorage.getItem(DISMISSED_KEY);
    return raw ? (JSON.parse(raw) as number[]) : [];
  } catch {
    return [];
  }
}

/**
 * Platform duyurusu bandi: `announcements/active` -> severity'ye gore mavi/sari.
 * Kapatma localStorage'da duyuru id'siyle hatirlanir.
 */
@Component({
  selector: 'app-announcement-banner',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @for (item of visible(); track item.id) {
      <div
        class="banner"
        [class.banner--warning]="item.severity === 2"
        [attr.data-testid]="'announcement-' + item.id"
      >
        <i
          class="banner__icon"
          [class.fa-solid]="true"
          [class.fa-circle-info]="item.severity === 1"
          [class.fa-triangle-exclamation]="item.severity === 2"
          aria-hidden="true"
        ></i>
        <span class="banner__text">
          <b>{{ item.title }}</b>
          <span>{{ item.body }}</span>
        </span>
        <button
          type="button"
          class="banner__close"
          (click)="dismiss(item.id)"
          [attr.data-testid]="'announcement-close-' + item.id"
          aria-label="close"
        >
          <i class="fa-solid fa-xmark" aria-hidden="true"></i>
        </button>
      </div>
    }
  `,
  styles: `
    :host {
      display: block;
    }
    .banner {
      display: flex;
      align-items: center;
      gap: 0.6rem;
      padding: 0.55rem 1.25rem;
      background: #eff6ff;
      color: #1e40af;
      border-bottom: 1px solid #bfdbfe;
      font-size: 0.85rem;

      &--warning {
        background: #fffbeb;
        color: #92400e;
        border-bottom-color: #fde68a;
      }
    }
    .banner__icon {
      flex: 0 0 auto;
    }
    .banner__text {
      display: flex;
      gap: 0.4rem;
      flex-wrap: wrap;
      flex: 1 1 auto;
      min-width: 0;
    }
    .banner__close {
      flex: 0 0 auto;
      border: 0;
      background: transparent;
      color: inherit;
      opacity: 0.65;
      cursor: pointer;
      padding: 0.2rem 0.35rem;
      border-radius: 6px;

      &:hover {
        opacity: 1;
        background: rgba(15, 23, 42, 0.06);
      }
    }
  `,
})
export class AnnouncementBannerComponent implements OnInit {
  private readonly api = inject(NotificationsApiService);

  protected readonly AnnouncementSeverity = AnnouncementSeverity;

  private readonly announcements = signal<ActiveAnnouncementDto[]>([]);
  private readonly dismissed = signal<number[]>(readDismissed());

  protected readonly visible = computed(() =>
    this.announcements().filter((a) => !this.dismissed().includes(a.id)),
  );

  ngOnInit(): void {
    this.api.activeAnnouncements().subscribe({
      next: (items) => this.announcements.set(items),
      error: () => this.announcements.set([]),
    });
  }

  protected dismiss(id: number): void {
    const next = [...new Set([...this.dismissed(), id])];
    this.dismissed.set(next);
    try {
      localStorage.setItem(DISMISSED_KEY, JSON.stringify(next));
    } catch {
      // Depolama kapaliysa yalniz bu oturumda gizli kalir.
    }
  }
}
