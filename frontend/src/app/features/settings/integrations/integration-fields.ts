/**
 * Arka uctaki `IntegrationCatalog` alan listesinin istemci aynasi.
 * Saglayici degistiginde hangi kimlik alanlarinin gosterilecegini belirler.
 */
export interface IntegrationFieldSpec {
  name: string;
  isSecret: boolean;
}

const f = (name: string, isSecret = false): IntegrationFieldSpec => ({ name, isSecret });

const FIELDS_BY_PROVIDER: Record<string, IntegrationFieldSpec[]> = {
  'EInvoice:uyumsoft': [
    f('Username'),
    f('Password', true),
    f('TestUrl'),
    f('LiveUrl'),
    f('SmmUrl'),
  ],
  'EInvoice:nes': [
    f('Username'),
    f('Password', true),
    f('TestUrl'),
    f('LiveUrl'),
    f('SenderVknTckn'),
  ],
  'Sms:netgsm': [f('UserCode'), f('Password', true), f('MsgHeader'), f('BaseUrl')],
  'WhatsApp:meta': [
    f('AccessToken', true),
    f('PhoneNumberId'),
    f('AppSecret', true),
    f('GraphApiBase'),
  ],
  'Payment:iyzico': [f('ApiKey', true), f('SecretKey', true), f('BaseUrl')],
};

/** e-Nabiz alanlari surucuden bagimsizdir (Mod ayri secici ile yonetilir). */
export const ENABIZ_FIELDS: IntegrationFieldSpec[] = [
  f('CkysCode'),
  f('UssUsername'),
  f('UssPassword', true),
  f('ApplicationCode'),
];

export function integrationFields(
  integrationKey: string,
  providerKey: string,
): IntegrationFieldSpec[] {
  if (integrationKey === 'Enabiz') {
    return ENABIZ_FIELDS;
  }
  return FIELDS_BY_PROVIDER[`${integrationKey}:${providerKey}`] ?? [];
}

/** Kart ikonlari — sirasiyla e-Belge, SMS, WhatsApp, Odeme, e-Nabiz. */
export const INTEGRATION_ICONS: Record<string, string> = {
  EInvoice: 'fa-solid fa-file-invoice',
  Sms: 'fa-solid fa-comment-sms',
  WhatsApp: 'fa-brands fa-whatsapp',
  Payment: 'fa-solid fa-credit-card',
  Enabiz: 'fa-solid fa-notes-medical',
};
