import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslocoPipe } from '@jsverse/transloco';
import { HasPermissionDirective } from '../../core/auth/has-permission.directive';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { MessageHistoryComponent } from './history/message-history.component';
import { MessageTemplatesComponent } from './templates/message-templates.component';
import { BulkSendComponent } from './bulk/bulk-send.component';
import { AutomationRulesComponent } from './automation/automation-rules.component';

type MessagingTab = 'history' | 'templates' | 'bulk' | 'automation';

interface TabDef {
  key: MessagingTab;
  icon: string;
  permission: string;
}

/**
 * Mesajlasma sayfasi (/app/messaging): gonderim gecmisi, sablonlar,
 * toplu gonderim sihirbazi ve otomasyon kurallari.
 * `?tab=` aktif sekmeyi, `?state=` gecmis filtresini derin baglantiyla tasir.
 */
@Component({
  selector: 'app-messaging-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    TranslocoPipe,
    HasPermissionDirective,
    PageHeaderComponent,
    MessageHistoryComponent,
    MessageTemplatesComponent,
    BulkSendComponent,
    AutomationRulesComponent,
  ],
  templateUrl: './messaging-page.component.html',
  styleUrl: './messaging-page.component.scss',
})
export class MessagingPageComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  private readonly queryParams = toSignal(this.route.queryParamMap, {
    initialValue: this.route.snapshot.queryParamMap,
  });

  protected readonly tabs: TabDef[] = [
    { key: 'history', icon: 'fa-solid fa-paper-plane', permission: 'messaging.read' },
    { key: 'templates', icon: 'fa-solid fa-file-lines', permission: 'messaging.read' },
    { key: 'bulk', icon: 'fa-solid fa-bullhorn', permission: 'messaging.bulk' },
    { key: 'automation', icon: 'fa-solid fa-robot', permission: 'settings.view' },
  ];

  private readonly manualTab = signal<MessagingTab | null>(null);

  /** Elle secim yoksa URL'deki `tab` degeri gecerlidir. */
  protected readonly activeTab = computed<MessagingTab>(() => {
    const manual = this.manualTab();
    if (manual) {
      return manual;
    }
    const fromUrl = this.queryParams().get('tab');
    return this.tabs.some((t) => t.key === fromUrl) ? (fromUrl as MessagingTab) : 'history';
  });

  /** Dashboard'daki "Basarisiz mesajlar" sayaci buradan gecmis filtresine baglanir. */
  protected readonly initialState = computed(() => {
    const raw = this.queryParams().get('state');
    const value = raw ? Number(raw) : NaN;
    return Number.isFinite(value) && value > 0 ? value : null;
  });

  protected readonly initialTemplateKey = computed(
    () => this.queryParams().get('templateKey') || null,
  );

  protected select(tab: MessagingTab): void {
    this.manualTab.set(tab);
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { tab },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }
}
