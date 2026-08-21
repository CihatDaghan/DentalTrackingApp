import { computed, inject, Injectable } from '@angular/core';
import { AuthStore } from './auth.store';

/** JWT payload'ini (base64url) guvenli sekilde okur; bozuksa null. */
function decodeJwtPayload(token: string): Record<string, unknown> | null {
  try {
    const part = token.split('.')[1];
    const json = atob(part.replace(/-/g, '+').replace(/_/g, '/'));
    return JSON.parse(json) as Record<string, unknown>;
  } catch {
    return null;
  }
}

/**
 * Aktif klinik baglami — access token'daki `clinic_id` claim'inden turetilir.
 * Randevu/recall isteklerinin zorunlu `clinicId` alani buradan beslenir.
 */
@Injectable({ providedIn: 'root' })
export class ClinicContext {
  private readonly authStore = inject(AuthStore);

  readonly clinicId = computed<number | null>(() => {
    const token = this.authStore.accessToken();
    if (!token) {
      return null;
    }
    const payload = decodeJwtPayload(token);
    const raw = payload?.['clinic_id'];
    const parsed = typeof raw === 'string' ? Number(raw) : typeof raw === 'number' ? raw : NaN;
    return Number.isFinite(parsed) ? parsed : null;
  });
}
