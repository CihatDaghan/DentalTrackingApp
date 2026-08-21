import {
  AfterViewInit,
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  ElementRef,
  inject,
  input,
  signal,
  viewChild,
} from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { TranslocoPipe } from '@jsverse/transloco';
import { ConsentsApiService } from '../../../core/api/consents-api.service';
import { PublicConsentViewDto } from '../../../core/api/clinical-api.models';
import { SignaturePadComponent } from '../../../shared/components/signature-pad/signature-pad.component';

type PageState = 'loading' | 'view' | 'signed' | 'declined' | 'gone' | 'error';

/**
 * Public onam imza sayfasi (/p/consent/:token — auth'suz, mobile-first).
 * Onam metni sonuna kadar kaydirilmadan imza alani acilmaz;
 * "Okudum, anladim" + imza -> POST sign. 410 -> kullanilmis/suresi dolmus sayfasi.
 */
@Component({
  selector: 'app-consent-sign-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, ButtonModule, CheckboxModule, TranslocoPipe, SignaturePadComponent],
  templateUrl: './consent-sign-page.component.html',
  styleUrl: './consent-sign-page.component.scss',
})
export class ConsentSignPageComponent implements AfterViewInit {
  private readonly api = inject(ConsentsApiService);

  /** Route parametresi (withComponentInputBinding). */
  readonly token = input.required<string>();

  protected readonly state = signal<PageState>('loading');
  protected readonly view = signal<PublicConsentViewDto | null>(null);
  protected readonly scrolledToEnd = signal(false);
  protected readonly accepted = signal(false);
  protected readonly hasSignature = signal(false);
  protected readonly submitting = signal(false);

  protected readonly canSign = computed(() => this.scrolledToEnd() && this.accepted());
  protected readonly canSubmit = computed(() => this.canSign() && this.hasSignature());

  private readonly bodyRef = viewChild<ElementRef<HTMLDivElement>>('consentBody');
  private readonly pad = viewChild(SignaturePadComponent);

  constructor() {
    effect(() => {
      const token = this.token();
      this.state.set('loading');
      this.api.publicView(token).subscribe({
        next: (view) => {
          this.view.set(view);
          this.state.set('view');
          // Icerik konteynerden kisaysa kaydirma sarti otomatik saglanmis olur.
          setTimeout(() => this.checkScrollEnd(), 50);
        },
        error: (error: unknown) => {
          if (error instanceof HttpErrorResponse && error.status === 410) {
            this.state.set('gone');
          } else if (error instanceof HttpErrorResponse && error.status === 404) {
            this.state.set('gone');
          } else {
            this.state.set('error');
          }
        },
      });
    });
  }

  ngAfterViewInit(): void {
    setTimeout(() => this.checkScrollEnd(), 100);
  }

  protected onBodyScroll(): void {
    this.checkScrollEnd();
  }

  private checkScrollEnd(): void {
    const el = this.bodyRef()?.nativeElement;
    if (!el) {
      return;
    }
    if (el.scrollTop + el.clientHeight >= el.scrollHeight - 12) {
      this.scrolledToEnd.set(true);
    }
  }

  protected onDrawn(): void {
    this.hasSignature.set(true);
  }

  protected clearSignature(): void {
    this.pad()?.clear();
    this.hasSignature.set(false);
  }

  protected submit(): void {
    const pad = this.pad();
    const base64 = pad?.toBase64();
    if (!base64 || !this.canSubmit()) {
      return;
    }
    this.submitting.set(true);
    this.api.publicSign(this.token(), { signaturePngBase64: base64, declined: false }).subscribe({
      next: () => {
        this.submitting.set(false);
        this.state.set('signed');
      },
      error: (error: unknown) => {
        this.submitting.set(false);
        if (error instanceof HttpErrorResponse && error.status === 410) {
          this.state.set('gone');
        }
      },
    });
  }

  protected decline(): void {
    this.submitting.set(true);
    this.api.publicSign(this.token(), { signaturePngBase64: null, declined: true }).subscribe({
      next: () => {
        this.submitting.set(false);
        this.state.set('declined');
      },
      error: (error: unknown) => {
        this.submitting.set(false);
        if (error instanceof HttpErrorResponse && error.status === 410) {
          this.state.set('gone');
        }
      },
    });
  }
}
