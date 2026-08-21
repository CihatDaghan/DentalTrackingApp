import { PaymentMethod } from '../../core/api/finance-api.models';
import { AppointmentStatus } from '../../core/api/api.models';

/** Odeme yontemi -> i18n anahtari (grafik/tablo etiketleri). */
export const PAYMENT_METHOD_KEYS: Record<number, string> = {
  [PaymentMethod.Cash]: 'paymentMethod.cash',
  [PaymentMethod.CreditCardPos]: 'paymentMethod.creditCardPos',
  [PaymentMethod.BankTransfer]: 'paymentMethod.bankTransfer',
  [PaymentMethod.OnlineLink]: 'paymentMethod.onlineLink',
  [PaymentMethod.Check]: 'paymentMethod.check',
};

/** Randevu durumu -> i18n anahtari. */
export const APPOINTMENT_STATUS_KEYS: Record<number, string> = {
  [AppointmentStatus.Scheduled]: 'appointmentStatus.scheduled',
  [AppointmentStatus.Confirmed]: 'appointmentStatus.confirmed',
  [AppointmentStatus.Arrived]: 'appointmentStatus.arrived',
  [AppointmentStatus.InChair]: 'appointmentStatus.inChair',
  [AppointmentStatus.Completed]: 'appointmentStatus.completed',
  [AppointmentStatus.Cancelled]: 'appointmentStatus.cancelled',
  [AppointmentStatus.NoShow]: 'appointmentStatus.noShow',
};

/** Yaslandirma kovasi rengi: taze yesil -> gecikmis kirmizi. */
export const AGING_COLORS: Record<string, string> = {
  '0-30': '#10b981',
  '31-60': '#f59e0b',
  '61-90': '#f97316',
  '90+': '#ef4444',
};

export function agingColor(bucket: string): string {
  return AGING_COLORS[bucket] ?? '#94a3b8';
}
