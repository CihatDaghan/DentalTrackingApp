import { MessageChannel, MessageKind } from '../../core/api/messaging-api.models';

/** Arayuzde secilebilen kanallar (Email surucusu G asamasinda kapsam disi). */
export const MESSAGE_CHANNELS: MessageChannel[] = [
  MessageChannel.Sms,
  MessageChannel.WhatsApp,
  MessageChannel.Email,
];

export const CHANNEL_LABEL_KEYS: Record<number, string> = {
  [MessageChannel.Sms]: 'messaging.channel.sms',
  [MessageChannel.WhatsApp]: 'messaging.channel.whatsApp',
  [MessageChannel.Email]: 'messaging.channel.email',
};

export const CHANNEL_ICONS: Record<number, string> = {
  [MessageChannel.Sms]: 'fa-solid fa-comment-sms',
  [MessageChannel.WhatsApp]: 'fa-brands fa-whatsapp',
  [MessageChannel.Email]: 'fa-solid fa-envelope',
};

export const CHANNEL_COLORS: Record<number, string> = {
  [MessageChannel.Sms]: '#3b82f6',
  [MessageChannel.WhatsApp]: '#25d366',
  [MessageChannel.Email]: '#6366f1',
};

export const MESSAGE_KINDS: MessageKind[] = [MessageKind.Transactional, MessageKind.Commercial];
