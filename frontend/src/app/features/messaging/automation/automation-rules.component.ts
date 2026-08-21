import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { InputNumberModule } from 'primeng/inputnumber';
import { SelectModule } from 'primeng/select';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { TooltipModule } from 'primeng/tooltip';
import { MessageService } from 'primeng/api';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { MessagingApiService } from '../../../core/api/messaging-api.service';
import {
  AUTOMATION_RULE_KEYS,
  AutomationRuleDto,
  AutomationRuleType,
  CHANNEL_POLICY_KEYS,
  ChannelPolicy,
  MessageTemplateDto,
} from '../../../core/api/messaging-api.models';
import { HasPermissionDirective } from '../../../core/auth/has-permission.directive';
import { injectTranslationSignal } from '../../../shared/utils/transloco-signal';

/** Ekranda gosterilen dort kural — arka uc kiracidaki her turden en fazla bir kural tutar. */
const RULE_ORDER: AutomationRuleType[] = [
  AutomationRuleType.AppointmentReminder,
  AutomationRuleType.Birthday,
  AutomationRuleType.PaymentOverdue,
  AutomationRuleType.Recall,
];

const RULE_ICONS: Record<number, string> = {
  [AutomationRuleType.AppointmentReminder]: 'fa-solid fa-calendar-check',
  [AutomationRuleType.Birthday]: 'fa-solid fa-cake-candles',
  [AutomationRuleType.PaymentOverdue]: 'fa-solid fa-hand-holding-dollar',
  [AutomationRuleType.Recall]: 'fa-solid fa-tooth',
};

/** Varsayilan sablon anahtarlari (kural yeni olusturuluyorsa). */
const DEFAULT_TEMPLATE_KEYS: Record<number, string> = {
  [AutomationRuleType.AppointmentReminder]: 'appointment_reminder',
  [AutomationRuleType.Birthday]: 'birthday',
  [AutomationRuleType.PaymentOverdue]: 'payment_overdue',
  [AutomationRuleType.Recall]: 'recall',
};

/** Randevu hatirlatmasi disindaki kurallar gunluk calisir; saat alani gosterilir. */
function isDaily(ruleType: AutomationRuleType): boolean {
  return ruleType !== AutomationRuleType.AppointmentReminder;
}

interface RuleCard {
  ruleType: AutomationRuleType;
  icon: string;
  id: number | null;
  isEnabled: boolean;
  offsetHours: number;
  channelPolicy: ChannelPolicy;
  templateKey: string;
  sendAt: Date | null;
  daily: boolean;
  dirty: boolean;
  saving: boolean;
}

/** "HH:mm:ss" -> bugunun o saatindeki Date. */
function parseTime(value: string | null): Date | null {
  if (!value) {
    return null;
  }
  const [h, m] = value.split(':').map(Number);
  const date = new Date();
  date.setHours(h ?? 9, m ?? 0, 0, 0);
  return date;
}

function formatTime(date: Date | null): string | null {
  if (!date) {
    return null;
  }
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${pad(date.getHours())}:${pad(date.getMinutes())}:00`;
}

/**
 * Otomasyon kurallari: dort kart (randevu hatirlatma, dogum gunu, odeme hatirlatma,
 * kontrol hatirlatma) — acik/kapali, kanal politikasi, saat/offset ve sablon secimi.
 */
@Component({
  selector: 'app-automation-rules',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    ButtonModule,
    DatePickerModule,
    InputNumberModule,
    SelectModule,
    ToggleSwitchModule,
    TooltipModule,
    TranslocoPipe,
    HasPermissionDirective,
  ],
  templateUrl: './automation-rules.component.html',
})
export class AutomationRulesComponent implements OnInit {
  private readonly api = inject(MessagingApiService);
  private readonly transloco = inject(TranslocoService);
  private readonly messageService = inject(MessageService);
  private readonly translation = injectTranslationSignal();

  protected readonly ruleKeys = AUTOMATION_RULE_KEYS;
  protected readonly cards = signal<RuleCard[]>([]);
  protected readonly loading = signal(true);
  protected readonly templates = signal<MessageTemplateDto[]>([]);

  protected readonly policyOptions = computed(() => {
    this.translation();
    return Object.values(ChannelPolicy).map((value) => ({
      label: this.transloco.translate('messaging.channelPolicy.' + CHANNEL_POLICY_KEYS[value]),
      value,
    }));
  });

  protected readonly templateOptions = computed(() => {
    const keys = [...new Set(this.templates().map((t) => t.templateKey))].sort((a, b) =>
      a.localeCompare(b, 'tr'),
    );
    return keys.map((key) => ({ label: key, value: key }));
  });

  ngOnInit(): void {
    this.api.templates(false).subscribe({
      next: (items) => this.templates.set(items),
      error: () => this.templates.set([]),
    });
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.api.automationRules().subscribe({
      next: (rules) => {
        this.cards.set(RULE_ORDER.map((type) => this.toCard(type, rules)));
        this.loading.set(false);
      },
      error: () => {
        this.cards.set(RULE_ORDER.map((type) => this.toCard(type, [])));
        this.loading.set(false);
      },
    });
  }

  private toCard(ruleType: AutomationRuleType, rules: AutomationRuleDto[]): RuleCard {
    const rule = rules.find((r) => r.ruleType === ruleType) ?? null;
    const daily = isDaily(ruleType);
    return {
      ruleType,
      icon: RULE_ICONS[ruleType],
      id: rule?.id ?? null,
      isEnabled: rule?.isEnabled ?? false,
      offsetHours: rule?.offsetHours ?? 24,
      channelPolicy: rule?.channelPolicy ?? ChannelPolicy.WhatsAppFirstThenSms,
      templateKey: rule?.templateKey || DEFAULT_TEMPLATE_KEYS[ruleType],
      sendAt: parseTime(rule?.sendAtLocalTime ?? null) ?? (daily ? parseTime('10:00:00') : null),
      daily,
      dirty: false,
      saving: false,
    };
  }

  protected patch<K extends keyof RuleCard>(
    ruleType: AutomationRuleType,
    key: K,
    value: RuleCard[K],
  ): void {
    this.cards.update((cards) =>
      cards.map((c) => (c.ruleType === ruleType ? { ...c, [key]: value, dirty: true } : c)),
    );
  }

  protected save(card: RuleCard): void {
    const request = {
      ruleType: card.ruleType,
      isEnabled: card.isEnabled,
      offsetHours: card.offsetHours,
      channelPolicy: card.channelPolicy,
      templateKey: card.templateKey || null,
      sendAtLocalTime: card.daily ? formatTime(card.sendAt) : null,
    };
    this.setSaving(card.ruleType, true);
    const call = card.id
      ? this.api.updateAutomationRule(card.id, request)
      : this.api.createAutomationRule(request);
    call.subscribe({
      next: (saved) => {
        this.cards.update((cards) =>
          cards.map((c) =>
            c.ruleType === card.ruleType
              ? { ...c, id: saved.id, dirty: false, saving: false }
              : c,
          ),
        );
        this.messageService.add({
          severity: 'success',
          summary: this.transloco.translate('messaging.automation.saved'),
          life: 3000,
        });
      },
      error: () => this.setSaving(card.ruleType, false),
    });
  }

  private setSaving(ruleType: AutomationRuleType, saving: boolean): void {
    this.cards.update((cards) =>
      cards.map((c) => (c.ruleType === ruleType ? { ...c, saving } : c)),
    );
  }
}
