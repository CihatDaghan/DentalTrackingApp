import { Directive, effect, inject, input, TemplateRef, ViewContainerRef } from '@angular/core';
import { AuthStore } from './auth.store';

/**
 * Yapisal izin direktifi: `*hasPermission="'payment.delete'"`.
 * AuthStore.permissionSet() signal'ina reaktiftir; izin degisince gorunum guncellenir.
 * SuperAdmin her zaman gecer.
 */
@Directive({ selector: '[hasPermission]' })
export class HasPermissionDirective {
  private readonly templateRef = inject(TemplateRef<unknown>);
  private readonly viewContainer = inject(ViewContainerRef);
  private readonly authStore = inject(AuthStore);

  readonly hasPermission = input.required<string>();

  constructor() {
    effect(() => {
      const allowed = this.authStore.hasPermission(this.hasPermission());
      this.viewContainer.clear();
      if (allowed) {
        this.viewContainer.createEmbeddedView(this.templateRef);
      }
    });
  }
}
