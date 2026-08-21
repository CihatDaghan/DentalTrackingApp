import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  input,
  OnDestroy,
  signal,
} from '@angular/core';
import { ClinicalApiService } from '../../../core/api/clinical-api.service';

/**
 * Yetkili (Authorization header'li) medya goruntuleyici:
 * dosyayi blob olarak ceker, object URL ile <img>'e baglar.
 * Duz <img src="/api/..."> bearer token tasiyamadigi icin bu bilesen kullanilir.
 */
@Component({
  selector: 'app-media-image',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (url(); as u) {
      <img [src]="u" [alt]="alt()" class="media-img" [style.object-fit]="fit()" />
    } @else if (failed()) {
      <div class="media-fallback"><i [class]="fallbackIcon()" aria-hidden="true"></i></div>
    } @else {
      <div class="media-fallback media-fallback--loading">
        <i class="fa-solid fa-spinner fa-spin" aria-hidden="true"></i>
      </div>
    }
  `,
  styles: `
    :host {
      display: block;
      width: 100%;
      height: 100%;
    }
    .media-img {
      width: 100%;
      height: 100%;
      display: block;
    }
    .media-fallback {
      width: 100%;
      height: 100%;
      display: flex;
      align-items: center;
      justify-content: center;
      color: #94a3b8;
      font-size: 1.75rem;
      background: #f1f5f9;
    }
  `,
})
export class MediaImageComponent implements OnDestroy {
  private readonly api = inject(ClinicalApiService);

  readonly mediaId = input.required<number>();
  /** thumbnail: kucuk onizleme; full: orijinal dosya (lightbox). */
  readonly kind = input<'thumbnail' | 'full'>('thumbnail');
  readonly alt = input('');
  readonly fit = input<'cover' | 'contain'>('cover');
  readonly fallbackIcon = input('fa-regular fa-file-lines');

  protected readonly url = signal<string | null>(null);
  protected readonly failed = signal(false);

  private objectUrl: string | null = null;

  constructor() {
    effect((onCleanup) => {
      const id = this.mediaId();
      const kind = this.kind();
      this.failed.set(false);
      this.url.set(null);
      const sub = (kind === 'thumbnail' ? this.api.thumbnailBlob(id) : this.api.downloadBlob(id)).subscribe({
        next: (blob) => {
          this.revoke();
          this.objectUrl = URL.createObjectURL(blob);
          this.url.set(this.objectUrl);
        },
        error: () => this.failed.set(true),
      });
      onCleanup(() => sub.unsubscribe());
    });
  }

  ngOnDestroy(): void {
    this.revoke();
  }

  private revoke(): void {
    if (this.objectUrl) {
      URL.revokeObjectURL(this.objectUrl);
      this.objectUrl = null;
    }
  }
}
