import { inject } from '@angular/core';
import { CanMatchFn, Router } from '@angular/router';
import { AuthStore } from './auth.store';

/** Oturum yoksa /login'e yonlendirir. */
export const authGuard: CanMatchFn = () => {
  const authStore = inject(AuthStore);
  const router = inject(Router);
  return authStore.isAuthenticated() ? true : router.createUrlTree(['/login']);
};
