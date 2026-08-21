import {
  ChangeDetectionStrategy,
  Component,
  effect,
  inject,
  model,
  signal,
  untracked,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { ConsentsApiService } from '../../../../core/api/consents-api.service';
import {
  ConsentFormDto,
  ConsentFormStatus,
  ConsentSendSmsResult,
  ConsentTemplateListItemDto,
} from '../../../../core/api/clinical-api.models';
import { HasPermissionDirective } from '../../../../core/auth/has-permission.directive';
import { StatusTagComponent } from '../../../../shared/components/status-tag/status-tag.component';
import { TrDatePipe } from '../../../../shared/pipes/tr-date.pipe';
import { PatientDetailStore } from '../patient-detail.store';
import { TabletSignDialogComponent } from './tablet-sign-dialog.component';

/**
 * "Onam Olustur" dialogu: sablon sec -> olustur -> onizleme (renderedHtml)
 * -> Tablette Imzalat VEYA SMS ile Gonder. Altta hastanin onam gecmisi
 * (tarih, sablon, durum rozeti, PDF indir).
 */
@Component({
  selector: 'app-consent-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    ButtonModule,
    DialogModule,
    SelectModule,
    TableModule,
    TooltipModule,
    TranslocoPipe,
    HasPermissionDirective,
    StatusTagComponent,
    TrDatePipe,
    TabletSignDialogComponent,
  ],
  templateUrl: './consent-dialog.component.html',
})
export class ConsentDialogComponent {
  private readonly store = inject(PatientDetailStore);
  private readonly api = inject(ConsentsApiService);
  private readonly messageService = inject(MessageService);
  private readonly transloco = inject(TranslocoService);

  protected readonly ConsentFormStatus = ConsentFormStatus;

  readonly visible = model(false);

  protected readonly templates = signal<ConsentTemplateListItemDto[]>([]);
  protected readonly selectedTemplateId = signal<number | null>(null);
  protected readonly creating = signal(false);
  /** Olusturulan/gorunen onam — onizleme + aksiyon alani bunu gosterir. */
  protected readonly activeConsent = signal<ConsentFormDto | null>(null);
  protected readonly history = signal<ConsentFormDto[]>([]);
  protected readonly historyLoading = signal(false);
  protected readonly sendingSms = signal(false);
  protected readonly smsResult = signal<ConsentSendSmsResult | null>(null);

  protected readonly tabletVisible = signal(false);

  constructor() {
    effect(() => {
      if (this.visible()) {
        untracked(() => {
          this.activeConsent.set(null);
          this.smsResult.set(null);
          this.loadTemplates();
          this.loadHistory();
        });
      }
    });
  }

  private loadTemplates(): void {
    this.api.templates().subscribe({
      next: (templates) => {
        const active = templates.filter((t) => t.isActive);
        this.templates.set(active);
        if (this.selectedTemplateId() == null && active.length) {
          this.selectedTemplateId.set(active[0].id);
        }
      },
      error: () => this.templates.set([]),
    });
  }

  private loadHistory(): void {
    const patientId = this.store.patient()?.id;
    if (!patientId) {
      return;
    }
    this.historyLoading.set(true);
    this.api.patientConsents(patientId).subscribe({
      next: (history) => {
        this.history.set(
          [...history].sort((a, b) => b.createdAtUtc.localeCompare(a.createdAtUtc)),
        );
        this.historyLoading.set(false);
      },
      error: () => {
        this.history.set([]);
        this.historyLoading.set(false);
      },
    });
  }

  protected create(): void {
    const patientId = this.store.patient()?.id;
    const templateId = this.selectedTemplateId();
    if (!patientId || templateId == null) {
      return;
    }
    this.creating.set(true);
    this.smsResult.set(null);
    this.api.createConsent(patientId, { templateId }).subscribe({
      next: (consent) => {
        this.creating.set(false);
        this.activeConsent.set(consent);
        this.loadHistory();
      },
      error: () => this.creating.set(false),
    });
  }

  /** Gecmisteki bir onami onizlemede acar. */
  protected preview(consent: ConsentFormDto): void {
    this.activeConsent.set(consent);
    this.smsResult.set(null);
  }

  protected openTabletSign(): void {
    this.tabletVisible.set(true);
  }

  protected onSigned(updated: ConsentFormDto): void {
    this.activeConsent.set(updated);
    this.loadHistory();
  }

  protected sendSms(): void {
    const consent = this.activeConsent();
    if (!consent) {
      return;
    }
    this.sendingSms.set(true);
    this.api.sendSms(consent.id).subscribe({
      next: (result) => {
        this.sendingSms.set(false);
        this.smsResult.set(result);
        this.messageService.add({
          severity: 'success',
          summary: this.transloco.translate('consent.smsSent'),
          detail: result.sentToPhone,
          life: 4000,
        });
        // Onizlemedeki durum rozeti de "SMS Gonderildi"ye donsun.
        this.api.consent(consent.id).subscribe({
          next: (updated) => this.activeConsent.set(updated),
        });
        this.loadHistory();
      },
      error: () => this.sendingSms.set(false),
    });
  }

  protected downloadPdf(consent: ConsentFormDto): void {
    this.api.pdfBlob(consent.id).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = `onam-${consent.id}.pdf`;
        a.click();
        URL.revokeObjectURL(url);
      },
    });
  }

  protected canTabletSign(consent: ConsentFormDto): boolean {
    return (
      consent.status === ConsentFormStatus.Draft || consent.status === ConsentFormStatus.SentBySms
    );
  }
}
