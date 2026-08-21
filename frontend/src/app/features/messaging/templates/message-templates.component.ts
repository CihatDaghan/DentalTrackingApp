import {
  ChangeDetectionStrategy,
  Component,
  computed,
  ElementRef,
  inject,
  OnInit,
  signal,
  viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { toSignal } from '@angular/core/rxjs-interop';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { SelectButtonModule } from 'primeng/selectbutton';
import { TableModule } from 'primeng/table';
import { TextareaModule } from 'primeng/textarea';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmationService, MessageService } from 'primeng/api';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { MessagingApiService } from '../../../core/api/messaging-api.service';
import {
  MESSAGE_PLACEHOLDERS,
  MessageChannel,
  MessageKind,
  MessageTemplateDto,
  WaTemplateStatus,
  WhatsAppTemplateDto,
} from '../../../core/api/messaging-api.models';
import { HasPermissionDirective } from '../../../core/auth/has-permission.directive';
import { StatusTagComponent } from '../../../shared/components/status-tag/status-tag.component';
import { injectTranslationSignal } from '../../../shared/utils/transloco-signal';
import { smsParts } from '../sms-parts';
import { CHANNEL_ICONS, CHANNEL_LABEL_KEYS, MESSAGE_CHANNELS } from '../messaging-options';

interface TemplateDraft {
  id: number | null;
  templateKey: string;
  channel: MessageChannel;
  locale: string;
  kind: MessageKind;
  isActive: boolean;
  body: string;
}

interface WaDraft {
  id: number | null;
  templateName: string;
  language: string;
  category: string;
  bodySpec: string;
  metaStatus: WaTemplateStatus;
  templateKey: string | null;
}

const EMPTY_DRAFT: TemplateDraft = {
  id: null,
  templateKey: '',
  channel: MessageChannel.Sms,
  locale: 'tr',
  kind: MessageKind.Transactional,
  isActive: true,
  body: '',
};

const EMPTY_WA_DRAFT: WaDraft = {
  id: null,
  templateName: '',
  language: 'tr',
  category: 'UTILITY',
  bodySpec: '',
  metaStatus: WaTemplateStatus.Draft,
  templateKey: null,
};

/**
 * Sablon sekmesi: metin sablonu listesi + editor (degisken chip'leri, SMS parca sayaci,
 * ticari tip uyarisi) ve altta Meta onay durumlu WhatsApp sablonlari.
 */
@Component({
  selector: 'app-message-templates',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    ButtonModule,
    DialogModule,
    InputTextModule,
    SelectModule,
    SelectButtonModule,
    TableModule,
    TextareaModule,
    ToggleSwitchModule,
    TooltipModule,
    TranslocoPipe,
    HasPermissionDirective,
    StatusTagComponent,
  ],
  templateUrl: './message-templates.component.html',
})
export class MessageTemplatesComponent implements OnInit {
  private readonly api = inject(MessagingApiService);
  private readonly transloco = inject(TranslocoService);
  private readonly messageService = inject(MessageService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly translation = injectTranslationSignal();

  protected readonly MessageChannel = MessageChannel;
  protected readonly MessageKind = MessageKind;
  protected readonly channelIcons = CHANNEL_ICONS;
  protected readonly placeholders = MESSAGE_PLACEHOLDERS;

  private readonly bodyRef = viewChild<ElementRef<HTMLTextAreaElement>>('bodyInput');

  protected readonly templates = signal<MessageTemplateDto[]>([]);
  protected readonly waTemplates = signal<WhatsAppTemplateDto[]>([]);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly draft = signal<TemplateDraft>({ ...EMPTY_DRAFT });
  protected readonly editorOpen = signal(false);

  protected readonly waDialogOpen = signal(false);
  protected readonly waSaving = signal(false);
  protected readonly waDraft = signal<WaDraft>({ ...EMPTY_WA_DRAFT });

  /** Aktif dil — sablon listesi bu dile gore gruplanmaz, yalniz siralanir. */
  private readonly activeLang = toSignal(this.transloco.langChanges$, {
    initialValue: this.transloco.getActiveLang(),
  });

  protected readonly channelOptions = computed(() => {
    this.translation();
    return MESSAGE_CHANNELS.map((value) => ({
      label: this.transloco.translate(CHANNEL_LABEL_KEYS[value]),
      value,
    }));
  });

  protected readonly localeOptions = [
    { label: 'TR', value: 'tr' },
    { label: 'EN', value: 'en' },
  ];

  protected readonly kindOptions = computed(() => {
    this.translation();
    return [
      {
        label: this.transloco.translate('messageKind.transactional'),
        value: MessageKind.Transactional,
      },
      { label: this.transloco.translate('messageKind.commercial'), value: MessageKind.Commercial },
    ];
  });

  protected readonly waStatusOptions = computed(() => {
    this.translation();
    return [
      { label: this.transloco.translate('waTemplateStatus.draft'), value: WaTemplateStatus.Draft },
      {
        label: this.transloco.translate('waTemplateStatus.submitted'),
        value: WaTemplateStatus.Submitted,
      },
      {
        label: this.transloco.translate('waTemplateStatus.approved'),
        value: WaTemplateStatus.Approved,
      },
      {
        label: this.transloco.translate('waTemplateStatus.rejected'),
        value: WaTemplateStatus.Rejected,
      },
    ];
  });

  protected readonly sortedTemplates = computed(() => {
    this.activeLang();
    return [...this.templates()].sort(
      (a, b) =>
        a.templateKey.localeCompare(b.templateKey, 'tr') ||
        a.channel - b.channel ||
        a.locale.localeCompare(b.locale),
    );
  });

  /** SMS'te karakter/parca sayaci — Turkce (GSM-7 disi) karakter varsa 70/parca. */
  protected readonly parts = computed(() => smsParts(this.draft().body));

  protected readonly isCommercial = computed(() => this.draft().kind === MessageKind.Commercial);
  protected readonly isSms = computed(() => this.draft().channel === MessageChannel.Sms);

  ngOnInit(): void {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.api.templates(true).subscribe({
      next: (items) => {
        this.templates.set(items);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
    this.api.whatsAppTemplates().subscribe({
      next: (items) => this.waTemplates.set(items),
      error: () => this.waTemplates.set([]),
    });
  }

  // --- Metin sablonu editoru ------------------------------------------------

  protected newTemplate(): void {
    this.draft.set({ ...EMPTY_DRAFT });
    this.editorOpen.set(true);
  }

  protected edit(template: MessageTemplateDto): void {
    this.draft.set({
      id: template.id,
      templateKey: template.templateKey,
      channel: template.channel,
      locale: template.locale,
      kind: template.kind,
      isActive: template.isActive,
      body: template.body,
    });
    this.editorOpen.set(true);
  }

  protected patch<K extends keyof TemplateDraft>(key: K, value: TemplateDraft[K]): void {
    this.draft.update((d) => ({ ...d, [key]: value }));
  }

  /** Degisken chip'i imlecin oldugu yere eklenir. */
  protected insertPlaceholder(token: string): void {
    const el = this.bodyRef()?.nativeElement;
    const body = this.draft().body;
    if (!el) {
      this.patch('body', body + token);
      return;
    }
    const start = el.selectionStart ?? body.length;
    const end = el.selectionEnd ?? body.length;
    const next = body.slice(0, start) + token + body.slice(end);
    this.patch('body', next);
    queueMicrotask(() => {
      el.focus();
      el.setSelectionRange(start + token.length, start + token.length);
    });
  }

  protected save(): void {
    const d = this.draft();
    if (!d.templateKey.trim() || !d.body.trim()) {
      this.messageService.add({
        severity: 'warn',
        summary: this.transloco.translate('messaging.templates.requiredFields'),
        life: 4000,
      });
      return;
    }
    const request = {
      templateKey: d.templateKey.trim(),
      channel: d.channel,
      locale: d.locale,
      body: d.body,
      kind: d.kind,
      isActive: d.isActive,
    };
    this.saving.set(true);
    const call = d.id
      ? this.api.updateTemplate(d.id, request)
      : this.api.createTemplate(request);
    call.subscribe({
      next: () => {
        this.saving.set(false);
        this.editorOpen.set(false);
        this.messageService.add({
          severity: 'success',
          summary: this.transloco.translate('messaging.templates.saved'),
          life: 3000,
        });
        this.load();
      },
      error: () => this.saving.set(false),
    });
  }

  protected remove(template: MessageTemplateDto, event: Event): void {
    event.stopPropagation();
    this.confirmation.confirm({
      header: this.transloco.translate('messaging.templates.deleteTitle'),
      message: this.transloco.translate('messaging.templates.deleteMessage'),
      icon: 'pi pi-exclamation-triangle',
      acceptButtonProps: {
        label: this.transloco.translate('common.delete'),
        severity: 'danger',
      },
      rejectButtonProps: {
        label: this.transloco.translate('common.cancel'),
        severity: 'secondary',
        outlined: true,
      },
      accept: () => {
        this.api.deleteTemplate(template.id).subscribe({ next: () => this.load() });
      },
    });
  }

  // --- WhatsApp sablonlari --------------------------------------------------

  protected newWaTemplate(): void {
    this.waDraft.set({ ...EMPTY_WA_DRAFT });
    this.waDialogOpen.set(true);
  }

  protected editWa(template: WhatsAppTemplateDto): void {
    this.waDraft.set({
      id: template.id,
      templateName: template.templateName,
      language: template.language,
      category: template.category,
      bodySpec: template.bodySpec,
      metaStatus: template.metaStatus,
      templateKey: template.templateKey,
    });
    this.waDialogOpen.set(true);
  }

  protected patchWa<K extends keyof WaDraft>(key: K, value: WaDraft[K]): void {
    this.waDraft.update((d) => ({ ...d, [key]: value }));
  }

  protected saveWa(): void {
    const d = this.waDraft();
    if (!d.templateName.trim() || !d.bodySpec.trim()) {
      this.messageService.add({
        severity: 'warn',
        summary: this.transloco.translate('messaging.templates.requiredFields'),
        life: 4000,
      });
      return;
    }
    const request = {
      templateName: d.templateName.trim(),
      language: d.language,
      category: d.category,
      bodySpec: d.bodySpec,
      metaStatus: d.metaStatus,
      templateKey: d.templateKey || null,
    };
    this.waSaving.set(true);
    const call = d.id
      ? this.api.updateWhatsAppTemplate(d.id, request)
      : this.api.createWhatsAppTemplate(request);
    call.subscribe({
      next: () => {
        this.waSaving.set(false);
        this.waDialogOpen.set(false);
        this.messageService.add({
          severity: 'success',
          summary: this.transloco.translate('messaging.templates.saved'),
          life: 3000,
        });
        this.load();
      },
      error: () => this.waSaving.set(false),
    });
  }

  protected channelLabel(channel: MessageChannel): string {
    return this.transloco.translate(CHANNEL_LABEL_KEYS[channel] ?? 'messaging.channel.sms');
  }
}
