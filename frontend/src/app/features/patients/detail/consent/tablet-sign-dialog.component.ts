import {
  ChangeDetectionStrategy,
  Component,
  inject,
  input,
  model,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { MessageService } from 'primeng/api';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { ConsentsApiService } from '../../../../core/api/consents-api.service';
import { ConsentFormDto } from '../../../../core/api/clinical-api.models';
import { SignaturePadComponent } from '../../../../shared/components/signature-pad/signature-pad.component';

/**
 * Klinik ici "Tablette Imzalat" tam ekran dialogu:
 * onam metni (scroll) + signature_pad tuvali + Temizle/Onayla.
 * Onayla -> POST /consents/{id}/sign -> toast + liste yenilenir.
 */
@Component({
  selector: 'app-tablet-sign-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ButtonModule, DialogModule, TranslocoPipe, SignaturePadComponent],
  template: `
    <p-dialog
      [visible]="visible()"
      (visibleChange)="visible.set($event)"
      [modal]="true"
      [focusOnShow]="false"
      [maximizable]="false"
      [closable]="!signing()"
      [style]="{ width: '100vw', height: '100vh', maxHeight: '100vh' }"
      styleClass="tablet-sign-dialog"
      [header]="consent()?.templateName ?? ''"
    >
      @if (consent(); as c) {
        <div class="flex flex-col gap-3 h-full" data-testid="tablet-sign-dialog">
          <div
            class="flex-1 min-h-0 overflow-y-auto border border-slate-200 rounded-lg p-4 bg-white consent-body"
            [innerHTML]="c.renderedHtml"
          ></div>
          <div class="shrink-0 flex flex-col gap-2">
            <span class="text-sm font-medium text-slate-600">
              {{ 'consent.signatureLabel' | transloco }}
            </span>
            <div style="height: 180px">
              <app-signature-pad #pad [hint]="'consent.signatureHint' | transloco" />
            </div>
            <div class="flex justify-between gap-2">
              <p-button
                [label]="'consent.clearSignature' | transloco"
                icon="fa-solid fa-eraser"
                severity="secondary"
                [outlined]="true"
                data-testid="clear-signature"
                (onClick)="pad.clear()"
              />
              <div class="flex gap-2">
                <p-button
                  [label]="'common.cancel' | transloco"
                  severity="secondary"
                  [outlined]="true"
                  (onClick)="visible.set(false)"
                />
                <p-button
                  [label]="'consent.confirmSign' | transloco"
                  icon="fa-solid fa-signature"
                  data-testid="confirm-sign"
                  [loading]="signing()"
                  (onClick)="sign()"
                />
              </div>
            </div>
          </div>
        </div>
      }
    </p-dialog>
  `,
  styles: `
    :host ::ng-deep .tablet-sign-dialog {
      max-width: 100vw;
    }
    :host ::ng-deep .tablet-sign-dialog .p-dialog-content {
      height: 100%;
      display: flex;
      flex-direction: column;
    }
    .consent-body {
      line-height: 1.6;
      font-size: 0.9rem;
      color: #334155;
    }
  `,
})
export class TabletSignDialogComponent {
  private readonly api = inject(ConsentsApiService);
  private readonly messageService = inject(MessageService);
  private readonly transloco = inject(TranslocoService);

  readonly visible = model(false);
  readonly consent = input<ConsentFormDto | null>(null);
  /** Imza basariyla kaydedilince guncel onam ile tetiklenir. */
  readonly signed = output<ConsentFormDto>();

  protected readonly signing = signal(false);
  private readonly pad = viewChild(SignaturePadComponent);

  protected sign(): void {
    const consent = this.consent();
    const pad = this.pad();
    if (!consent || !pad) {
      return;
    }
    const base64 = pad.toBase64();
    if (!base64) {
      this.messageService.add({
        severity: 'warn',
        summary: this.transloco.translate('consent.signatureRequired'),
        life: 3000,
      });
      return;
    }
    this.signing.set(true);
    this.api.sign(consent.id, { signaturePngBase64: base64 }).subscribe({
      next: (updated) => {
        this.signing.set(false);
        this.visible.set(false);
        this.messageService.add({
          severity: 'success',
          summary: this.transloco.translate('consent.signSuccess'),
          life: 4000,
        });
        this.signed.emit(updated);
      },
      error: () => this.signing.set(false),
    });
  }
}
