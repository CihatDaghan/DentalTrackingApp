import { computed, inject } from '@angular/core';
import { Router } from '@angular/router';
import {
  patchState,
  signalStore,
  withComputed,
  withHooks,
  withMethods,
  withState,
} from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { AuthApiService } from '../api/auth-api.service';
import {
  AuthUserDto,
  ClinicSummaryDto,
  LoginRequestDto,
  LoginResponseDto,
  UserType,
} from '../api/auth-api.models';

const STORAGE_KEY = 'dentaltrack.auth';
/** Impersonation sirasinda super admin oturumu burada bekletilir (cikista geri yuklenir). */
const IMPERSONATION_ORIGIN_KEY = 'dentaltrack.impersonation-origin';

/** Aktif impersonation baglami — uyari bandi bu bilgiyle cizilir. */
export interface ImpersonationState {
  tenantId: number;
  tenantName: string;
  /** Token 15 dk'lik ve refresh'i YOK; suresi dolunca 401 -> /login. */
  expiresAtUtc: string;
  impersonatedUserEmail: string;
}

interface AuthState {
  user: AuthUserDto | null;
  accessToken: string | null;
  refreshToken: string | null;
  permissions: string[];
  rememberMe: boolean;
  /** Login yanitindaki klinik listesi — rapor/ayar ekranlarindaki sube seciciyi besler. */
  clinics: ClinicSummaryDto[];
  impersonation: ImpersonationState | null;
}

type PersistedAuthState = AuthState;

const initialState: AuthState = {
  user: null,
  accessToken: null,
  refreshToken: null,
  permissions: [],
  rememberMe: false,
  clinics: [],
  impersonation: null,
};

function normalize(parsed: Partial<PersistedAuthState>): AuthState {
  return {
    user: parsed.user ?? null,
    accessToken: parsed.accessToken ?? null,
    refreshToken: parsed.refreshToken ?? null,
    permissions: parsed.permissions ?? [],
    rememberMe: parsed.rememberMe ?? false,
    clinics: parsed.clinics ?? [],
    impersonation: parsed.impersonation ?? null,
  };
}

function readPersistedState(): AuthState | null {
  for (const storage of [localStorage, sessionStorage]) {
    try {
      const raw = storage.getItem(STORAGE_KEY);
      if (raw) {
        return normalize(JSON.parse(raw) as Partial<PersistedAuthState>);
      }
    } catch {
      storage.removeItem(STORAGE_KEY);
    }
  }
  return null;
}

/** JWT payload'ini (base64url) guvenli sekilde okur; bozuksa null. */
function decodeJwt(token: string): Record<string, unknown> | null {
  try {
    const part = token.split('.')[1];
    const json = decodeURIComponent(
      atob(part.replace(/-/g, '+').replace(/_/g, '/'))
        .split('')
        .map((c) => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
        .join(''),
    );
    return JSON.parse(json) as Record<string, unknown>;
  } catch {
    return null;
  }
}

/** Impersonation token'i kullanici DTO'su tasimaz; claim'lerden turetilir. */
function userFromToken(token: string, email: string): AuthUserDto | null {
  const payload = decodeJwt(token);
  if (!payload) {
    return null;
  }
  const name = String(payload['name'] ?? '').trim();
  const spaceIndex = name.lastIndexOf(' ');
  const perm = payload['perm'];
  return {
    id: Number(payload['sub'] ?? 0),
    email: String(payload['email'] ?? email),
    firstName: spaceIndex > 0 ? name.slice(0, spaceIndex) : name,
    lastName: spaceIndex > 0 ? name.slice(spaceIndex + 1) : '',
    userType: Number(payload['user_type'] ?? UserType.Owner) as UserType,
    locale: String(payload['locale'] ?? 'tr'),
    tenantId: payload['tenant_id'] != null ? Number(payload['tenant_id']) : null,
    isSuperAdmin: false,
    permissions: Array.isArray(perm) ? (perm as string[]) : perm ? [String(perm)] : [],
  };
}

function persistState(state: AuthState): void {
  const target = state.rememberMe ? localStorage : sessionStorage;
  const other = state.rememberMe ? sessionStorage : localStorage;
  other.removeItem(STORAGE_KEY);
  target.setItem(STORAGE_KEY, JSON.stringify(state satisfies PersistedAuthState));
}

function clearPersistedState(): void {
  localStorage.removeItem(STORAGE_KEY);
  sessionStorage.removeItem(STORAGE_KEY);
}

export const AuthStore = signalStore(
  { providedIn: 'root' },
  withState<AuthState>(initialState),
  withComputed((store) => ({
    isAuthenticated: computed(() => !!store.accessToken()),
    /** Izin seti — `*hasPermission` direktifi ve permission guard'lar buradan beslenir. */
    permissionSet: computed<Set<string>>(() => new Set(store.permissions())),
    fullName: computed(() => {
      const user = store.user();
      return user ? `${user.firstName} ${user.lastName}`.trim() : '';
    }),
    isSuperAdmin: computed(() => store.user()?.isSuperAdmin === true),
    isImpersonating: computed(() => store.impersonation() !== null),
  })),
  withMethods((store) => {
    const api = inject(AuthApiService);
    const router = inject(Router);
    /** Tek ucus kilidi: es zamanli 401'lerde yalniz bir refresh istegi atilir. */
    let refreshInFlight: Promise<boolean> | null = null;

    const snapshot = (): AuthState => ({
      user: store.user(),
      accessToken: store.accessToken(),
      refreshToken: store.refreshToken(),
      permissions: store.permissions(),
      rememberMe: store.rememberMe(),
      clinics: store.clinics(),
      impersonation: store.impersonation(),
    });

    return {
      /** Login cagrisi: state'i doldurur, kalicilastirir; klinik secimi gerekip gerekmedigini yanittan okuyun. */
      async login(request: LoginRequestDto): Promise<LoginResponseDto> {
        const response = await firstValueFrom(api.login(request));
        sessionStorage.removeItem(IMPERSONATION_ORIGIN_KEY);
        patchState(store, {
          user: response.user,
          accessToken: response.accessToken,
          refreshToken: response.refreshToken,
          permissions: response.user?.permissions ?? [],
          rememberMe: request.rememberMe,
          clinics: response.clinics ?? [],
          impersonation: null,
        });
        persistState(snapshot());
        return response;
      },

      /** Coklu klinik kullanicisi icin klinik secimi; yeni token cifti alinir. */
      async selectClinic(clinicId: number): Promise<void> {
        const tokens = await firstValueFrom(api.selectClinic({ clinicId }));
        patchState(store, {
          accessToken: tokens.accessToken,
          refreshToken: tokens.refreshToken,
        });
        persistState(snapshot());
      },

      /** Access token yenileme (tek ucus). Basarisizsa oturumu kapatir ve /login'e yonlendirir. */
      refresh(): Promise<boolean> {
        if (refreshInFlight) {
          return refreshInFlight;
        }
        // Baska bir sekme rotasyon yapmis olabilir: bellekteki eski token'la istek atmak
        // sunucuda "yeniden kullanim" sayilip TUM oturumlari iptal ettiriyordu
        // (inceleme bulgusu). Her denemede storage'daki guncel token esas alinir.
        const persisted = readPersistedState();
        if (persisted?.refreshToken && persisted.refreshToken !== store.refreshToken()) {
          patchState(store, {
            accessToken: persisted.accessToken,
            refreshToken: persisted.refreshToken,
          });
        }
        const refreshToken = store.refreshToken();
        if (!refreshToken) {
          this.logout();
          return Promise.resolve(false);
        }
        refreshInFlight = firstValueFrom(api.refresh({ refreshToken }))
          .then((tokens) => {
            patchState(store, {
              accessToken: tokens.accessToken,
              refreshToken: tokens.refreshToken,
            });
            persistState(snapshot());
            return true;
          })
          .catch(() => {
            this.logout();
            return false;
          })
          .finally(() => {
            refreshInFlight = null;
          });
        return refreshInFlight;
      },

      /**
       * Super admin -> kiracı oturumu. Donen token 15 dk'lik ve refresh'siz;
       * suresi dolunca `refresh()` refreshToken bulamayip /login'e duser (tasarim geregi).
       */
      startImpersonation(response: {
        accessToken: string;
        tenantId: number;
        tenantName: string;
        expiresAtUtc: string;
        impersonatedUserEmail: string;
      }): boolean {
        const user = userFromToken(response.accessToken, response.impersonatedUserEmail);
        if (!user) {
          return false;
        }
        // Super admin oturumu cikista geri yuklenmek uzere ayri anahtarda bekletilir.
        sessionStorage.setItem(IMPERSONATION_ORIGIN_KEY, JSON.stringify(snapshot()));
        patchState(store, {
          user,
          accessToken: response.accessToken,
          refreshToken: null,
          permissions: user.permissions,
          clinics: [],
          impersonation: {
            tenantId: response.tenantId,
            tenantName: response.tenantName,
            expiresAtUtc: response.expiresAtUtc,
            impersonatedUserEmail: response.impersonatedUserEmail,
          },
        });
        persistState(snapshot());
        return true;
      },

      /** Impersonation'dan cikis: super admin oturumu geri yuklenir. Yoksa tam cikis. */
      stopImpersonation(): void {
        const raw = sessionStorage.getItem(IMPERSONATION_ORIGIN_KEY);
        sessionStorage.removeItem(IMPERSONATION_ORIGIN_KEY);
        if (!raw) {
          this.logout();
          return;
        }
        try {
          const origin = normalize(JSON.parse(raw) as Partial<PersistedAuthState>);
          patchState(store, { ...origin, impersonation: null });
          persistState(snapshot());
          void router.navigate(['/admin/tenants']);
        } catch {
          this.logout();
        }
      },

      /** Oturumu kapatir: sunucuya bildirir (best-effort), state + storage temizler, /login'e doner. */
      logout(): void {
        const refreshToken = store.refreshToken();
        if (refreshToken) {
          api.logout({ refreshToken }).subscribe({ error: () => undefined });
        }
        sessionStorage.removeItem(IMPERSONATION_ORIGIN_KEY);
        patchState(store, initialState);
        clearPersistedState();
        void router.navigate(['/login']);
      },

      hasPermission(permission: string): boolean {
        return store.user()?.isSuperAdmin === true || store.permissionSet().has(permission);
      },
    };
  }),
  withHooks({
    onInit(store) {
      const persisted = readPersistedState();
      if (persisted) {
        patchState(store, persisted);
      }
      // Sekmeler arasi token senkronu: bir sekme rotasyon yaptiginda digerleri guncel cifti alir.
      window.addEventListener('storage', () => {
        const latest = readPersistedState();
        if (!latest?.refreshToken) {
          return;
        }
        if (latest.refreshToken !== store.refreshToken()) {
          patchState(store, {
            accessToken: latest.accessToken,
            refreshToken: latest.refreshToken,
          });
        }
      });
    },
  }),
);
