import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { SelectButtonModule } from 'primeng/selectbutton';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { MessageService } from 'primeng/api';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { SettingsApiService } from '../../../core/api/settings-api.service';
import { ClinicSettingsDto, TenantLegalType } from '../../../core/api/settings-api.models';
import { HasPermissionDirective } from '../../../core/auth/has-permission.directive';
import { MediaImageComponent } from '../../../shared/components/media-image/media-image.component';
import { injectTranslationSignal } from '../../../shared/utils/transloco-signal';

/**
 * Klinik kunyesi: unvan, klinik tipi (fatura belge tipini belirler), VKN/TCKN,
 * vergi dairesi, adres, iletisim, saglik turizmi yetki belgesi, CKYS kodu.
 */
@Component({
  selector: 'app-clinic-settings',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    ButtonModule,
    InputTextModule,
    SelectButtonModule,
    ToggleSwitchModule,
    TranslocoPipe,
    HasPermissionDirective,
    MediaImageComponent,
  ],
  template: `
    <div class="dt-card p-4 flex flex-col gap-4 max-w-4xl" data-testid="clinic-settings">
      @if (loading()) {
        <p class="text-slate-400 text-sm py-8 text-center">{{ 'common.loading' | transloco }}</p>
      } @else if (form(); as f) {
        <div class="grid grid-cols-1 md:grid-cols-2 gap-4">
          <label class="flex flex-col gap-1">
            <span class="dt-label">{{ 'settings.clinic.tenantName' | transloco }}</span>
            <input
              pInputText
              [ngModel]="f.tenantName"
              (ngModelChange)="patch({ tenantName: $event })"
              data-testid="clinic-tenant-name"
            />
          </label>

          <label class="flex flex-col gap-1">
            <span class="dt-label">{{ 'settings.clinic.clinicName' | transloco }}</span>
            <input
              pInputText
              [ngModel]="f.clinicName"
              (ngModelChange)="patch({ clinicName: $event })"
              data-testid="clinic-name"
            />
          </label>

          <div class="flex flex-col gap-1 md:col-span-2">
            <span class="dt-label">{{ 'settings.clinic.legalType' | transloco }}</span>
            <div class="flex items-center gap-3 flex-wrap">
              <p-selectbutton
                [options]="legalTypeOptions()"
                optionLabel="label"
                optionValue="value"
                [allowEmpty]="false"
                [ngModel]="f.legalType"
                (ngModelChange)="patch({ legalType: $event })"
                data-testid="clinic-legal-type"
              />
              <span class="text-xs text-slate-500">
                <i class="fa-solid fa-circle-info mr-1" aria-hidden="true"></i>
                {{ 'settings.clinic.legalTypeHint' | transloco }}
              </span>
            </div>
          </div>

          <label class="flex flex-col gap-1">
            <span class="dt-label">{{ 'settings.clinic.taxNumber' | transloco }}</span>
            <input
              pInputText
              [ngModel]="f.taxNumber"
              (ngModelChange)="patch({ taxNumber: $event })"
              data-testid="clinic-tax-number"
            />
          </label>

          <label class="flex flex-col gap-1">
            <span class="dt-label">{{ 'settings.clinic.taxOffice' | transloco }}</span>
            <input
              pInputText
              [ngModel]="f.taxOffice"
              (ngModelChange)="patch({ taxOffice: $event })"
            />
          </label>

          <label class="flex flex-col gap-1 md:col-span-2">
            <span class="dt-label">{{ 'settings.clinic.address' | transloco }}</span>
            <input pInputText [ngModel]="f.address" (ngModelChange)="patch({ address: $event })" />
          </label>

          <label class="flex flex-col gap-1">
            <span class="dt-label">{{ 'settings.clinic.city' | transloco }}</span>
            <input pInputText [ngModel]="f.city" (ngModelChange)="patch({ city: $event })" />
          </label>

          <label class="flex flex-col gap-1">
            <span class="dt-label">{{ 'settings.clinic.district' | transloco }}</span>
            <input pInputText [ngModel]="f.district" (ngModelChange)="patch({ district: $event })" />
          </label>

          <label class="flex flex-col gap-1">
            <span class="dt-label">{{ 'settings.clinic.phone' | transloco }}</span>
            <input pInputText [ngModel]="f.phone" (ngModelChange)="patch({ phone: $event })" />
          </label>

          <label class="flex flex-col gap-1">
            <span class="dt-label">{{ 'settings.clinic.email' | transloco }}</span>
            <input pInputText [ngModel]="f.email" (ngModelChange)="patch({ email: $event })" />
          </label>

          <label class="flex flex-col gap-1">
            <span class="dt-label">{{ 'settings.clinic.ckysCode' | transloco }}</span>
            <input
              pInputText
              [ngModel]="f.ckysCode"
              (ngModelChange)="patch({ ckysCode: $event })"
              data-testid="clinic-ckys"
            />
          </label>

          <div class="flex items-center gap-3 md:col-span-2">
            <p-toggleswitch
              [ngModel]="f.hasHealthTourismAuthorization"
              (ngModelChange)="patch({ hasHealthTourismAuthorization: $event })"
              data-testid="clinic-health-tourism"
            />
            <div class="flex flex-col">
              <span class="text-sm text-slate-700">
                {{ 'settings.clinic.healthTourism' | transloco }}
              </span>
              <span class="text-xs text-slate-500">
                {{ 'settings.clinic.healthTourismHint' | transloco }}
              </span>
            </div>
          </div>

          <!-- Logo: arka uctaki tenant duzeyi yukleme ucu olmadigindan mevcut dosya gosterilir -->
          <div class="flex flex-col gap-1 md:col-span-2">
            <span class="dt-label">{{ 'settings.clinic.logo' | transloco }}</span>
            <div class="flex items-center gap-3">
              @if (f.logoFileId) {
                <app-media-image [mediaId]="f.logoFileId" [alt]="f.clinicName" class="h-14 w-14" />
                <p-button
                  [text]="true"
                  severity="danger"
                  size="small"
                  icon="fa-solid fa-trash"
                  [label]="'settings.clinic.logoRemove' | transloco"
                  (onClick)="patch({ logoFileId: null })"
                />
              } @else {
                <span class="text-xs text-slate-500">
                  {{ 'settings.clinic.logoHint' | transloco }}
                </span>
              }
            </div>
          </div>
        </div>

        <div class="flex items-center gap-3 pt-2 border-t border-slate-100">
          <p-button
            *hasPermission="'settings.update'"
            [label]="'common.save' | transloco"
            icon="fa-solid fa-check"
            size="small"
            [loading]="saving()"
            (onClick)="save()"
            data-testid="clinic-save"
          />
          <span class="text-xs text-slate-400">
            {{ 'settings.clinic.planInfo' | transloco: { plan: original()?.planCode ?? '—' } }}
          </span>
        </div>
      }
    </div>
  `,
  styles: `
    .dt-label {
      font-size: 0.75rem;
      font-weight: 500;
      color: #64748b;
      text-transform: uppercase;
      letter-spacing: 0.02em;
    }
  `,
})
export class ClinicSettingsComponent implements OnInit {
  private readonly api = inject(SettingsApiService);
  private readonly messageService = inject(MessageService);
  private readonly transloco = inject(TranslocoService);
  private readonly translation = injectTranslationSignal();

  protected readonly original = signal<ClinicSettingsDto | null>(null);
  protected readonly form = signal<ClinicSettingsDto | null>(null);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);

  protected readonly legalTypeOptions = computed(() => {
    this.translation();
    return [
      {
        label: this.transloco.translate('settings.clinic.legalTypeSole'),
        value: TenantLegalType.SoleProprietor,
      },
      {
        label: this.transloco.translate('settings.clinic.legalTypeCompany'),
        value: TenantLegalType.Company,
      },
    ];
  });

  ngOnInit(): void {
    this.api.clinic().subscribe({
      next: (dto) => {
        this.original.set(dto);
        this.form.set({ ...dto });
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  protected patch(patch: Partial<ClinicSettingsDto>): void {
    this.form.update((f) => (f ? { ...f, ...patch } : f));
  }

  protected save(): void {
    const f = this.form();
    if (!f) {
      return;
    }
    this.saving.set(true);
    this.api
      .updateClinic({
        tenantName: f.tenantName,
        legalType: f.legalType,
        clinicName: f.clinicName,
        taxNumber: f.taxNumber,
        taxOffice: f.taxOffice,
        hasHealthTourismAuthorization: f.hasHealthTourismAuthorization,
        address: f.address,
        city: f.city,
        district: f.district,
        phone: f.phone,
        email: f.email,
        ckysCode: f.ckysCode,
        logoFileId: f.logoFileId,
        clinicId: f.clinicId,
      })
      .subscribe({
        next: (dto) => {
          this.original.set(dto);
          this.form.set({ ...dto });
          this.saving.set(false);
          this.messageService.add({
            severity: 'success',
            summary: this.transloco.translate('settings.saved'),
            life: 3000,
          });
        },
        error: () => this.saving.set(false),
      });
  }
}
