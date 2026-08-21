import { inject } from '@angular/core';
import { CanMatchFn, Router } from '@angular/router';
import { AuthStore } from './auth.store';

/**
 * `/admin` kabugu: oturum + `isSuperAdmin` zorunlu.
 * Oturum yoksa /login'e, normal kullaniciysa /app'e yonlendirir.
 */
export const superAdminGuard: CanMatchFn = () => {
  const authStore = inject(AuthStore);
  const router = inject(Router);
  if (!authStore.isAuthenticated()) {
    return router.createUrlTree(['/login']);
  }
  return authStore.isSuperAdmin() ? true : router.createUrlTree(['/app/dashboard']);
};
