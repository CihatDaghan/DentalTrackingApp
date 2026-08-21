import { inject } from '@angular/core';
import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { catchError, throwError } from 'rxjs';
import { MessageService } from 'primeng/api';

/** RFC 7807 problem+json govdesi (AppExceptionHandler ciktisi). */
interface ProblemDetails {
  status?: number;
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

/** Toast gosterilmeyecek uclar: login akisi kendi hatasini gosterir. */
const SILENT_ENDPOINTS = ['/auth/login', '/auth/refresh', '/auth/select-clinic'];

/**
 * API hatalarini problem+json'dan okuyup p-toast ile gosterir.
 * 401 sessiz gecilir (auth interceptor refresh/logout yonetir).
 */
export const errorInterceptor: HttpInterceptorFn = (req, next) => {
  const messageService = inject(MessageService);

  return next(req).pipe(
    catchError((error: unknown) => {
      if (
        error instanceof HttpErrorResponse &&
        error.status !== 401 &&
        !SILENT_ENDPOINTS.some((path) => req.url.includes(path))
      ) {
        const problem = (error.error ?? {}) as ProblemDetails;
        const validationDetails = problem.errors
          ? Object.values(problem.errors).flat().join(' ')
          : '';
        const summary = problem.title || `HTTP ${error.status}`;
        const detail = [problem.detail, validationDetails].filter(Boolean).join(' ');
        messageService.add({
          severity: 'error',
          summary,
          detail: detail || undefined,
          life: 6000,
        });
      }
      return throwError(() => error);
    }),
  );
};
