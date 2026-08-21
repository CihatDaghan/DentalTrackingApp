import { inject } from '@angular/core';
import { CanMatchFn, Router } from '@angular/router';
import { AuthStore } from './auth.store';

/**
 * Izin tabanli route korumasi: `canMatch: [permissionGuard('report.view')]`.
 * Izin yoksa panele doner (SuperAdmin her zaman gecer — AuthStore.hasPermission).
 */
export function permissionGuard(permission: string): CanMatchFn {
  return () => {
    const authStore = inject(AuthStore);
    const router = inject(Router);
    if (!authStore.isAuthenticated()) {
      return router.createUrlTree(['/login']);
    }
    return authStore.hasPermission(permission) ? true : router.createUrlTree(['/app/dashboard']);
  };
}
