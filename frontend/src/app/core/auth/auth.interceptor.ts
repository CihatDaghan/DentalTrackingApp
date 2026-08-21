import { inject } from '@angular/core';
import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { catchError, from, switchMap, throwError } from 'rxjs';
import { AuthStore } from './auth.store';

/** Refresh dongusune girmemesi gereken auth uclari. */
const AUTH_ENDPOINTS = ['/auth/login', '/auth/refresh', '/auth/logout'];

function isAuthEndpoint(req: HttpRequest<unknown>): boolean {
  return AUTH_ENDPOINTS.some((path) => req.url.includes(path));
}

function withBearer(req: HttpRequest<unknown>, token: string): HttpRequest<unknown> {
  return req.clone({ setHeaders: { Authorization: `Bearer ${token}` } });
}

/**
 * Bearer token ekler; 401'de (tek ucus kilidiyle) refresh dener,
 * basarisizsa AuthStore.logout() oturumu kapatip /login'e yonlendirir.
 */
export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authStore = inject(AuthStore);

  const accessToken = authStore.accessToken();
  const authReq =
    accessToken && !req.headers.has('Authorization') ? withBearer(req, accessToken) : req;

  return next(authReq).pipe(
    catchError((error: unknown) => {
      if (
        error instanceof HttpErrorResponse &&
        error.status === 401 &&
        !isAuthEndpoint(req) &&
        authStore.refreshToken()
      ) {
        return from(authStore.refresh()).pipe(
          switchMap((refreshed) => {
            const newToken = authStore.accessToken();
            if (!refreshed || !newToken) {
              return throwError(() => error);
            }
            return next(withBearer(req, newToken));
          }),
        );
      }
      return throwError(() => error);
    }),
  );
};
