import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from './api.models';

export interface NotificationDto {
  id: number;
  /** Or. "appointment_created" | "einvoice_error" | "stock_low" | "payment_received". */
  eventType: string;
  title: string;
  body: string | null;
  /** Arka ucun urettigi mantiksal yol; `notificationLink()` ile uygulama rotasina cevrilir. */
  linkPath: string | null;
  createdAtUtc: string;
  readAtUtc: string | null;
}

export interface NotificationListDto {
  page: PagedResult<NotificationDto>;
  unreadCount: number;
}

export const AnnouncementSeverity = {
  Info: 1,
  Warning: 2,
} as const;
export type AnnouncementSeverity =
  (typeof AnnouncementSeverity)[keyof typeof AnnouncementSeverity];

export interface ActiveAnnouncementDto {
  id: number;
  title: string;
  body: string;
  severity: AnnouncementSeverity;
  startsAtUtc: string;
  endsAtUtc: string | null;
}

/**
 * Arka uctaki mantiksal `linkPath` degerini uygulama rotasina cevirir.
 * Ornek: "/patients/55/payments" -> ["/app/patients/55/payment"].
 */
export function notificationLink(
  linkPath: string | null,
): { path: string; queryParams: Record<string, string> } | null {
  if (!linkPath) {
    return null;
  }
  const [rawPath, rawQuery] = linkPath.split('?');
  const queryParams: Record<string, string> = {};
  for (const [key, value] of new URLSearchParams(rawQuery ?? '')) {
    queryParams[key] = value;
  }

  const patientPayments = /^\/patients\/(\d+)\/payments$/.exec(rawPath);
  if (patientPayments) {
    return { path: `/app/patients/${patientPayments[1]}/payment`, queryParams: {} };
  }
  const patient = /^\/patients\/(\d+)/.exec(rawPath);
  if (patient) {
    return { path: `/app/patients/${patient[1]}`, queryParams: {} };
  }
  if (rawPath.startsWith('/appointments')) {
    return { path: '/app/calendar', queryParams };
  }
  if (rawPath.startsWith('/invoices')) {
    return { path: `/app${rawPath}`, queryParams };
  }
  if (rawPath.startsWith('/stock')) {
    return { path: '/app/inventory', queryParams: { lowOnly: 'true' } };
  }
  if (rawPath.startsWith('/finance/payments')) {
    return { path: '/app/cash', queryParams: {} };
  }
  if (rawPath.startsWith('/enabiz')) {
    // e-Nabiz gonderim ekrani yok; ayarlardaki entegrasyon karti en yakin hedef.
    return { path: '/app/settings/integrations', queryParams: {} };
  }
  return { path: '/app/dashboard', queryParams: {} };
}

/** Bildirim zili + platform duyurusu uclari. */
@Injectable({ providedIn: 'root' })
export class NotificationsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/v1`;

  list(unreadOnly = false, page = 1, pageSize = 10): Observable<NotificationListDto> {
    const params = new HttpParams()
      .set('unreadOnly', String(unreadOnly))
      .set('page', String(page))
      .set('pageSize', String(pageSize));
    return this.http.get<NotificationListDto>(`${this.baseUrl}/notifications`, { params });
  }

  markRead(id: number): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/notifications/${id}/read`, {});
  }

  markAllRead(): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/notifications/read-all`, {});
  }

  activeAnnouncements(): Observable<ActiveAnnouncementDto[]> {
    return this.http.get<ActiveAnnouncementDto[]>(`${this.baseUrl}/announcements/active`);
  }
}
