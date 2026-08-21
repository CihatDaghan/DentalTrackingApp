import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  effect,
  ElementRef,
  input,
  OnDestroy,
  output,
  signal,
  viewChild,
} from '@angular/core';
import SignaturePad from 'signature_pad';

/**
 * signature_pad sarmalayicisi: ıslak imza tuvali.
 * Tablet onam dialogu ve public /p/consent/:token sayfasinda kullanilir.
 * `disabled` iken cizim kapalidir (public sayfada metin sonuna inilmeden acilmaz).
 */
@Component({
  selector: 'app-signature-pad',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="sig-wrap" [class.sig-wrap--disabled]="disabled()">
      <canvas #canvas class="sig-canvas" data-testid="signature-canvas"></canvas>
      @if (isEmpty() && !disabled()) {
        <span class="sig-hint">{{ hint() }}</span>
      }
    </div>
  `,
  styles: `
    .sig-wrap {
      position: relative;
      width: 100%;
      height: 100%;
      min-height: 160px;
      border: 2px dashed #cbd5e1;
      border-radius: 12px;
      background: #fff;
      overflow: hidden;
    }
    .sig-wrap--disabled {
      background: #f1f5f9;
      pointer-events: none;
      opacity: 0.6;
    }
    .sig-canvas {
      display: block;
      width: 100%;
      height: 100%;
      touch-action: none;
    }
    .sig-hint {
      position: absolute;
      inset: 0;
      display: flex;
      align-items: center;
      justify-content: center;
      color: #94a3b8;
      font-size: 0.85rem;
      pointer-events: none;
    }
  `,
})
export class SignaturePadComponent implements AfterViewInit, OnDestroy {
  private readonly canvasRef = viewChild.required<ElementRef<HTMLCanvasElement>>('canvas');

  readonly disabled = input(false);
  readonly hint = input('');
  /** Ilk cizgi cizildiginde tetiklenir (Onayla butonunu aktiflestirmek icin). */
  readonly drawn = output<void>();

  protected readonly isEmpty = signal(true);

  private pad: SignaturePad | null = null;
  private resizeObserver: ResizeObserver | null = null;

  constructor() {
    effect(() => {
      const off = this.disabled();
      if (this.pad) {
        if (off) {
          this.pad.off();
        } else {
          this.pad.on();
        }
      }
    });
  }

  ngAfterViewInit(): void {
    const canvas = this.canvasRef().nativeElement;
    this.pad = new SignaturePad(canvas, {
      minWidth: 1,
      maxWidth: 2.5,
      penColor: '#1e293b',
    });
    if (this.disabled()) {
      this.pad.off();
    }
    this.pad.addEventListener('endStroke', () => {
      this.isEmpty.set(this.pad?.isEmpty() ?? true);
      this.drawn.emit();
    });
    // Kapsayici boyut degisince tuval piksel boyutu guncellenir (cizim sifirlanir).
    this.resizeObserver = new ResizeObserver(() => this.resizeCanvas());
    this.resizeObserver.observe(canvas.parentElement as HTMLElement);
    this.resizeCanvas();
  }

  ngOnDestroy(): void {
    this.resizeObserver?.disconnect();
    this.pad?.off();
  }

  clear(): void {
    this.pad?.clear();
    this.isEmpty.set(true);
  }

  empty(): boolean {
    return this.pad?.isEmpty() ?? true;
  }

  /** Imza PNG data URL'i ("data:image/png;base64,..."). */
  toDataUrl(): string | null {
    if (!this.pad || this.pad.isEmpty()) {
      return null;
    }
    return this.pad.toDataURL('image/png');
  }

  /** Sadece base64 govdesi (data URL on eki olmadan) — API sozlesmesi bunu bekler. */
  toBase64(): string | null {
    const url = this.toDataUrl();
    return url ? url.substring(url.indexOf(',') + 1) : null;
  }

  private resizeCanvas(): void {
    const canvas = this.canvasRef().nativeElement;
    const wrap = canvas.parentElement as HTMLElement;
    const ratio = Math.max(window.devicePixelRatio || 1, 1);
    const width = wrap.clientWidth;
    const height = wrap.clientHeight;
    if (width === 0 || height === 0) {
      return;
    }
    const data = this.pad?.toData();
    canvas.width = width * ratio;
    canvas.height = height * ratio;
    canvas.getContext('2d')?.scale(ratio, ratio);
    // Boyut degisiminde mevcut cizim korunur.
    if (this.pad && data && data.length) {
      this.pad.fromData(data);
    } else {
      this.pad?.clear();
    }
  }
}
