import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { SelectButtonModule } from 'primeng/selectbutton';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { MessageService } from 'primeng/api';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { SettingsApiService } from '../../../core/api/settings-api.service';
import {
  EnabizMode,
  EnabizSettingsDto,
  IntegrationSettingDto,
  IntegrationTestResultDto,
} from '../../../core/api/settings-api.models';
import { injectTranslationSignal } from '../../../shared/utils/transloco-signal';
import { INTEGRATION_ICONS, integrationFields } from './integration-fields';

/** Kart basina duzenleme tamponu. */
interface CardState {
  providerKey: string;
  environment: string;
  isEnabled: boolean;
  /** Kullanicinin girdigi yeni degerler; sir alanlari bos birakilirsa degismez. */
  values: Record<string, string>;
  saving: boolean;
  testing: boolean;
  result: IntegrationTestResultDto | null;
}

/**
 * Entegrasyon ayarlari (`settings.integrations`): 5 saglayici karti.
 * Sir alanlari maskeli gelir (`••••1234`) ve bos birakilirsa degismez.
 * e-Nabiz karti ayrica mod secici tasir; KtsRegistered kapaliysa Canli secilemez.
 */
@Component({
  selector: 'app-integrations-settings',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    ButtonModule,
    InputTextModule,
    SelectModule,
    SelectButtonModule,
    ToggleSwitchModule,
    TranslocoPipe,
  ],
  templateUrl: './integrations-settings.component.html',
})
export class IntegrationsSettingsComponent implements OnInit {
  private readonly api = inject(SettingsApiService);
  private readonly messageService = inject(MessageService);
  private readonly transloco = inject(TranslocoService);
  private readonly translation = injectTranslationSignal();

  protected readonly EnabizMode = EnabizMode;

  protected readonly integrations = signal<IntegrationSettingDto[]>([]);
  protected readonly enabiz = signal<EnabizSettingsDto | null>(null);
  protected readonly loading = signal(true);
  protected readonly cards = signal<Record<string, CardState>>({});

  protected readonly environmentOptions = computed(() => {
    this.translation();
    return [
      { label: this.transloco.translate('settings.integrations.envTest'), value: 'Test' },
      { label: this.transloco.translate('settings.integrations.envLive'), value: 'Live' },
    ];
  });

  protected readonly enabizModeOptions = computed(() => {
    this.translation();
    const canGoLive = this.enabiz()?.canGoLive ?? false;
    return [
      {
        label: this.transloco.translate('settings.integrations.enabiz.disabled'),
        value: EnabizMode.Disabled,
        disabled: false,
      },
      {
        label: this.transloco.translate('settings.integrations.enabiz.held'),
        value: EnabizMode.Held,
        disabled: false,
      },
      {
        label: this.transloco.translate('settings.integrations.enabiz.test'),
        value: EnabizMode.TestOnly,
        disabled: false,
      },
      {
        label: this.transloco.translate('settings.integrations.enabiz.live'),
        value: EnabizMode.Live,
        disabled: !canGoLive,
      },
    ];
  });

  ngOnInit(): void {
    this.reload();
    this.api.enabizSettings().subscribe({
      next: (dto) => this.enabiz.set(dto),
      error: () => this.enabiz.set(null),
    });
  }

  protected icon(key: string): string {
    return INTEGRATION_ICONS[key] ?? 'fa-solid fa-plug';
  }

  protected card(key: string): CardState {
    return (
      this.cards()[key] ?? {
        providerKey: 'fake',
        environment: 'Test',
        isEnabled: false,
        values: {},
        saving: false,
        testing: false,
        result: null,
      }
    );
  }

  protected fields(item: IntegrationSettingDto): { name: string; isSecret: boolean }[] {
    return integrationFields(item.integrationKey, this.card(item.integrationKey).providerKey);
  }

  /** Maskeli mevcut deger (`••••1234`) — input'ta placeholder olarak gosterilir. */
  protected maskedValue(item: IntegrationSettingDto, field: string): string {
    return item.settings?.[field] ?? '';
  }

  protected providerOptions(item: IntegrationSettingDto): { label: string; value: string }[] {
    return item.availableProviders.map((p) => ({ label: p, value: p }));
  }

  protected patchCard(key: string, patch: Partial<CardState>): void {
    this.cards.update((cards) => ({ ...cards, [key]: { ...this.card(key), ...patch } }));
  }

  protected value(key: string, field: string): string {
    return this.card(key).values[field] ?? '';
  }

  protected setValue(key: string, field: string, value: string): void {
    const card = this.card(key);
    this.patchCard(key, { values: { ...card.values, [field]: value } });
  }

  protected save(item: IntegrationSettingDto): void {
    const key = item.integrationKey;
    const card = this.card(key);
    this.patchCard(key, { saving: true });

    if (key === 'Enabiz') {
      const current = this.enabiz();
      this.api
        .saveEnabizSettings({
          mode: current?.mode ?? EnabizMode.Disabled,
          ckysCode: card.values['CkysCode'] || null,
          ussUsername: card.values['UssUsername'] || null,
          ussPassword: card.values['UssPassword'] || null,
          applicationCode: card.values['ApplicationCode'] || null,
        })
        .subscribe({
          next: (dto) => {
            this.enabiz.set(dto);
            this.patchCard(key, { saving: false, values: {} });
            this.reload();
            this.toastSaved();
          },
          error: () => this.patchCard(key, { saving: false }),
        });
      return;
    }

    // Yalniz doldurulmus alanlar gonderilir; bos birakilan sirlar arka ucta korunur.
    const settings: Record<string, string> = {};
    for (const [field, value] of Object.entries(card.values)) {
      if (value !== '') {
        settings[field] = value;
      }
    }
    this.api
      .updateIntegration(key, {
        providerKey: card.providerKey,
        environment: card.environment,
        isEnabled: card.isEnabled,
        settings,
      })
      .subscribe({
        next: (dto) => {
          this.integrations.update((items) =>
            items.map((i) => (i.integrationKey === key ? dto : i)),
          );
          this.patchCard(key, { saving: false, values: {} });
          this.toastSaved();
        },
        error: () => this.patchCard(key, { saving: false }),
      });
  }

  protected setEnabizMode(mode: EnabizMode): void {
    const current = this.enabiz();
    if (!current) {
      return;
    }
    this.enabiz.set({ ...current, mode });
    this.api
      .saveEnabizSettings({
        mode,
        ckysCode: null,
        ussUsername: null,
        ussPassword: null,
        applicationCode: null,
      })
      .subscribe({
        next: (dto) => {
          this.enabiz.set(dto);
          this.reload();
          this.toastSaved();
        },
        error: () => this.enabiz.set(current),
      });
  }

  protected test(item: IntegrationSettingDto): void {
    const key = item.integrationKey;
    this.patchCard(key, { testing: true, result: null });
    this.api.testIntegration(key).subscribe({
      next: (result) => this.patchCard(key, { testing: false, result }),
      error: () =>
        this.patchCard(key, {
          testing: false,
          result: {
            success: false,
            message: this.transloco.translate('settings.integrations.testFailed'),
            durationMs: 0,
            providerKey: item.providerKey,
          },
        }),
    });
  }

  private reload(): void {
    this.api.integrations().subscribe({
      next: (items) => {
        this.integrations.set(items);
        const cards: Record<string, CardState> = {};
        for (const item of items) {
          const existing = this.cards()[item.integrationKey];
          cards[item.integrationKey] = {
            providerKey: item.providerKey,
            environment: item.environment,
            isEnabled: item.isEnabled,
            values: {},
            saving: false,
            testing: false,
            result: existing?.result ?? null,
          };
        }
        this.cards.set(cards);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  private toastSaved(): void {
    this.messageService.add({
      severity: 'success',
      summary: this.transloco.translate('settings.saved'),
      life: 3000,
    });
  }
}
