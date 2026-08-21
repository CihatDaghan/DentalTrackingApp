import { AppointmentStatus, AppointmentType } from '../../core/api/api.models';

/** Durum -> sol kenar seridi rengi. */
export const STATUS_COLORS: Record<number, string> = {
  [AppointmentStatus.Scheduled]: '#3b82f6',
  [AppointmentStatus.Confirmed]: '#22c55e',
  [AppointmentStatus.Arrived]: '#f97316',
  [AppointmentStatus.InChair]: '#8b5cf6',
  [AppointmentStatus.Completed]: '#6b7280',
  [AppointmentStatus.Cancelled]: '#ef4444',
  [AppointmentStatus.NoShow]: '#881337',
};

/** Tur -> i18n anahtari (calendar.type.*). */
export const TYPE_KEYS: Record<number, string> = {
  [AppointmentType.Normal]: 'normal',
  [AppointmentType.Control]: 'control',
  [AppointmentType.EmptySlot]: 'emptySlot',
  [AppointmentType.Blocked]: 'blocked',
};

/** UTC naive "yyyy-MM-ddTHH:mm:ss" — query string DateTime baglaminda TZ kaymasini onler. */
export function toUtcNaive(date: Date): string {
  return date.toISOString().slice(0, 19);
}

export const APPOINTMENT_DURATIONS = [15, 30, 45, 60, 90, 120];
