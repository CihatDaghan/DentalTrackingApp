import { inject } from '@angular/core';
import { CanDeactivateFn } from '@angular/router';
import { Observable, Subject } from 'rxjs';
import { ConfirmationService } from 'primeng/api';
import { TranslocoService } from '@jsverse/transloco';

/** Kirli form uyarisi verebilen bilesen sozlesmesi. */
export interface HasPendingChanges {
  hasPendingChanges(): boolean;
}

/**
 * Route cikisinda kaydedilmemis degisiklik onayi.
 * Bilesen `hasPendingChanges()` true dondurdugunde ConfirmDialog acilir.
 */
export const pendingChangesGuard: CanDeactivateFn<HasPendingChanges> = (component) => {
  if (!component.hasPendingChanges()) {
    return true;
  }
  const confirmation = inject(ConfirmationService);
  const transloco = inject(TranslocoService);
  const result$ = new Subject<boolean>();

  confirmation.confirm({
    header: transloco.translate('common.unsavedTitle'),
    message: transloco.translate('common.unsavedMessage'),
    icon: 'pi pi-exclamation-triangle',
    acceptButtonProps: { label: transloco.translate('common.leave'), severity: 'danger' },
    rejectButtonProps: { label: transloco.translate('common.stay'), severity: 'secondary', outlined: true },
    accept: () => {
      result$.next(true);
      result$.complete();
    },
    reject: () => {
      result$.next(false);
      result$.complete();
    },
  });

  return result$ as Observable<boolean>;
};
