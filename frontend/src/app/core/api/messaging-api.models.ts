/**
 * Mesajlasma + odeme linki sozlesmesi (openapi/v1.json ile dogrulandi).
 * Enum degerleri arka uctaki `Dental.Domain.Enums.MessagingEnums` ile birebir aynidir.
 */

// ---------------------------------------------------------------------------
// Enumlar (arka uc: byte)
// ---------------------------------------------------------------------------

export const MessageChannel = {
  Sms: 1,
  WhatsApp: 2,
  Email: 3,
} as const;
export type MessageChannel = (typeof MessageChannel)[keyof typeof MessageChannel];

/** IYS/KVKK ayrimi: Transactional izin gerektirmez, Commercial izin kontrolune tabidir. */
export const MessageKind = {
  Transactional: 1,
  Commercial: 2,
} as const;
export type MessageKind = (typeof MessageKind)[keyof typeof MessageKind];

export const OutboundMessageState = {
  Pending: 1,
  Sending: 2,
  Sent: 3,
  Delivered: 4,
  Failed: 5,
  Skipped: 6,
} as const;
export type OutboundMessageState =
  (typeof OutboundMessageState)[keyof typeof OutboundMessageState];

export const MessageSkipReason = {
  NoConsent: 1,
  InvalidNumber: 2,
  NoTemplate: 3,
  ChannelDisabled: 4,
  Duplicate: 5,
} as const;
export type MessageSkipReason = (typeof MessageSkipReason)[keyof typeof MessageSkipReason];

export const WaTemplateStatus = {
  Draft: 1,
  Submitted: 2,
  Approved: 3,
  Rejected: 4,
} as const;
export type WaTemplateStatus = (typeof WaTemplateStatus)[keyof typeof WaTemplateStatus];

export const AutomationRuleType = {
  AppointmentReminder: 1,
  Birthday: 2,
  PaymentOverdue: 3,
  Recall: 4,
} as const;
export type AutomationRuleType = (typeof AutomationRuleType)[keyof typeof AutomationRuleType];

export const ChannelPolicy = {
  WhatsAppFirstThenSms: 1,
  SmsOnly: 2,
  WhatsAppOnly: 3,
} as const;
export type ChannelPolicy = (typeof ChannelPolicy)[keyof typeof ChannelPolicy];

export const PaymentIntentStatus = {
  Created: 1,
  LinkSent: 2,
  Paid: 3,
  Failed: 4,
  Expired: 5,
  Refunded: 6,
} as const;
export type PaymentIntentStatus = (typeof PaymentIntentStatus)[keyof typeof PaymentIntentStatus];

// ---------------------------------------------------------------------------
// i18n anahtar haritalari (status-tag + secim listeleri tek yerden)
// ---------------------------------------------------------------------------

export const MESSAGE_STATE_KEYS: Record<number, string> = {
  [OutboundMessageState.Pending]: 'pending',
  [OutboundMessageState.Sending]: 'sending',
  [OutboundMessageState.Sent]: 'sent',
  [OutboundMessageState.Delivered]: 'delivered',
  [OutboundMessageState.Failed]: 'failed',
  [OutboundMessageState.Skipped]: 'skipped',
};

export const MESSAGE_KIND_KEYS: Record<number, string> = {
  [MessageKind.Transactional]: 'transactional',
  [MessageKind.Commercial]: 'commercial',
};

export const MESSAGE_SKIP_REASON_KEYS: Record<number, string> = {
  [MessageSkipReason.NoConsent]: 'noConsent',
  [MessageSkipReason.InvalidNumber]: 'invalidNumber',
  [MessageSkipReason.NoTemplate]: 'noTemplate',
  [MessageSkipReason.ChannelDisabled]: 'channelDisabled',
  [MessageSkipReason.Duplicate]: 'duplicate',
};

export const WA_TEMPLATE_STATUS_KEYS: Record<number, string> = {
  [WaTemplateStatus.Draft]: 'draft',
  [WaTemplateStatus.Submitted]: 'submitted',
  [WaTemplateStatus.Approved]: 'approved',
  [WaTemplateStatus.Rejected]: 'rejected',
};

export const AUTOMATION_RULE_KEYS: Record<number, string> = {
  [AutomationRuleType.AppointmentReminder]: 'appointmentReminder',
  [AutomationRuleType.Birthday]: 'birthday',
  [AutomationRuleType.PaymentOverdue]: 'paymentOverdue',
  [AutomationRuleType.Recall]: 'recall',
};

export const CHANNEL_POLICY_KEYS: Record<number, string> = {
  [ChannelPolicy.WhatsAppFirstThenSms]: 'whatsAppFirstThenSms',
  [ChannelPolicy.SmsOnly]: 'smsOnly',
  [ChannelPolicy.WhatsAppOnly]: 'whatsAppOnly',
};

export const PAYMENT_INTENT_STATUS_KEYS: Record<number, string> = {
  [PaymentIntentStatus.Created]: 'created',
  [PaymentIntentStatus.LinkSent]: 'linkSent',
  [PaymentIntentStatus.Paid]: 'paid',
  [PaymentIntentStatus.Failed]: 'failed',
  [PaymentIntentStatus.Expired]: 'expired',
  [PaymentIntentStatus.Refunded]: 'refunded',
};

/** Sablon govdesine tikla-ekle ile basilan yer tutucular (arka uc MessagePlaceholders). */
export const MESSAGE_PLACEHOLDERS = [
  '{hasta_adi}',
  '{randevu_tarihi}',
  '{randevu_saati}',
  '{klinik_adi}',
  '{bakiye}',
  '{odeme_linki}',
  '{onam_linki}',
  '{hekim_adi}',
] as const;

/** Odeme linki mesajlarinin sablon anahtari — gecmiste hizli filtre olarak kullanilir. */
export const PAYMENT_LINK_TEMPLATE_KEY = 'payment_link';

// ---------------------------------------------------------------------------
// Giden mesaj (outbox)
// ---------------------------------------------------------------------------

export interface OutboundMessageDto {
  id: number;
  patientId: number | null;
  patientName: string | null;
  channel: MessageChannel;
  kind: MessageKind;
  templateKey: string;
  renderedBody: string;
  toAddress: string;
  state: OutboundMessageState;
  skipReason: MessageSkipReason | null;
  providerKey: string | null;
  providerMessageId: string | null;
  scheduledAtUtc: string;
  sentAtUtc: string | null;
  deliveredAtUtc: string | null;
  error: string | null;
  attemptCount: number;
  nextAttemptAtUtc: string | null;
  /** Doluysa bu mesaj, belirtilen mesajin (WhatsApp) SMS yedegidir. */
  fallbackOfMessageId: number | null;
  refType: string | null;
  refId: number | null;
  creditCost: number | null;
  correlationId: string;
  createdAtUtc: string;
}

export interface MessageListQuery {
  channel?: MessageChannel | null;
  state?: OutboundMessageState | null;
  patientId?: number | null;
  /** "yyyy-MM-dd" */
  from?: string | null;
  to?: string | null;
  page?: number;
  pageSize?: number;
}

export interface MessageSendRequest {
  patientId: number;
  templateKey?: string;
  channel?: MessageChannel | null;
  kind?: MessageKind;
  bodyOverride?: string | null;
  params?: Record<string, string> | null;
  scheduledAtUtc?: string | null;
}

// ---------------------------------------------------------------------------
// Toplu gonderim
// ---------------------------------------------------------------------------

export interface BulkAudienceFilter {
  lastVisitFrom?: string | null;
  lastVisitTo?: string | null;
  doctorUserId?: number | null;
  hasDebt?: boolean | null;
  /** 1-12 */
  birthMonth?: number | null;
  tagId?: number | null;
}

export interface BulkMessageRequest {
  templateKey: string;
  filter: BulkAudienceFilter;
  channel?: MessageChannel | null;
  kind?: MessageKind;
  bodyOverride?: string | null;
  scheduledAtUtc?: string | null;
}

export interface BulkMessageResult {
  targeted: number;
  enqueued: number;
  skippedNoConsent: number;
  skippedNoPhone: number;
  messageIds: number[];
}

// ---------------------------------------------------------------------------
// Sablonlar
// ---------------------------------------------------------------------------

export interface MessageTemplateDto {
  id: number;
  templateKey: string;
  channel: MessageChannel;
  locale: string;
  body: string;
  kind: MessageKind;
  isActive: boolean;
}

export interface MessageTemplateUpsertRequest {
  templateKey: string;
  channel: MessageChannel;
  locale: string;
  body: string;
  kind?: MessageKind;
  isActive?: boolean;
}

export interface WhatsAppTemplateDto {
  id: number;
  templateName: string;
  language: string;
  category: string;
  bodySpec: string;
  paramMapJson: string | null;
  metaStatus: WaTemplateStatus;
  metaUpdatedAtUtc: string | null;
  templateKey: string | null;
}

export interface WhatsAppTemplateUpsertRequest {
  templateName: string;
  language: string;
  category: string;
  bodySpec: string;
  paramMapJson?: string | null;
  metaStatus?: WaTemplateStatus;
  templateKey?: string | null;
}

// ---------------------------------------------------------------------------
// Otomasyon kurallari
// ---------------------------------------------------------------------------

export interface AutomationRuleDto {
  id: number;
  ruleType: AutomationRuleType;
  isEnabled: boolean;
  offsetHours: number;
  channelPolicy: ChannelPolicy;
  templateKey: string;
  /** "HH:mm:ss" */
  sendAtLocalTime: string | null;
}

export interface AutomationRuleUpsertRequest {
  ruleType: AutomationRuleType;
  isEnabled: boolean;
  offsetHours?: number;
  channelPolicy?: ChannelPolicy;
  templateKey?: string | null;
  sendAtLocalTime?: string | null;
}

// ---------------------------------------------------------------------------
// Odeme linki
// ---------------------------------------------------------------------------

export interface PaymentLinkCreateRequest {
  patientId: number;
  amount: number;
  description?: string | null;
  channel?: MessageChannel | null;
  currencyCode?: string;
  expiresInHours?: number;
}

export interface PaymentLinkDto {
  id: number;
  patientId: number;
  patientName: string;
  clinicId: number;
  amount: number;
  currencyCode: string;
  description: string | null;
  publicToken: string;
  providerKey: string | null;
  /** Saglayicinin barindirdigi 3DS sayfasi. */
  linkUrl: string | null;
  status: PaymentIntentStatus;
  paidAmount: number | null;
  providerPaymentId: string | null;
  paymentId: number | null;
  paidAtUtc: string | null;
  expiresAtUtc: string | null;
  /** Kuyruga alinan SMS/WhatsApp mesajinin kimligi (null ise mesaj uretilmemis). */
  messageId: number | null;
  createdAtUtc: string;
}

// --- Public (auth'suz) ------------------------------------------------------

export interface PublicPaymentViewDto {
  clinicName: string;
  patientName: string;
  amount: number;
  currencyCode: string;
  description: string | null;
  status: PaymentIntentStatus;
  payUrl: string | null;
  expiresAtUtc: string | null;
}

export interface PublicPaymentStatusDto {
  status: PaymentIntentStatus;
  paidAmount: number | null;
  paidAtUtc: string | null;
}
