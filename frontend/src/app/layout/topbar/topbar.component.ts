import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { toSignal } from '@angular/core/rxjs-interop';
import { SelectModule } from 'primeng/select';
import { PopoverModule } from 'primeng/popover';
import { ButtonModule } from 'primeng/button';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AuthStore } from '../../core/auth/auth.store';
import { NotificationBellComponent } from '../notification-bell/notification-bell.component';

/** Beyaz ust bar: sayfa basligi, bildirim zili, dil secici (TR/EN), kullanici menusu + cikis. */
@Component({
  selector: 'app-topbar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    SelectModule,
    PopoverModule,
    ButtonModule,
    TranslocoPipe,
    NotificationBellComponent,
  ],
  templateUrl: './topbar.component.html',
  styleUrl: './topbar.component.scss',
})
export class TopbarComponent {
  protected readonly authStore = inject(AuthStore);
  private readonly transloco = inject(TranslocoService);

  /** Aktif sayfanin transloco anahtari (route data'dan gelir). */
  readonly titleKey = input<string>('menu.dashboard');

  protected readonly languages = [
    { label: 'TR', value: 'tr' },
    { label: 'EN', value: 'en' },
  ];

  protected readonly activeLang = toSignal(this.transloco.langChanges$, {
    initialValue: this.transloco.getActiveLang(),
  });

  protected readonly initials = computed(() => {
    const user = this.authStore.user();
    if (!user) {
      return '';
    }
    return `${user.firstName?.[0] ?? ''}${user.lastName?.[0] ?? ''}`.toUpperCase();
  });

  protected setLang(lang: string): void {
    this.transloco.setActiveLang(lang);
  }

  protected logout(): void {
    this.authStore.logout();
  }
}
