import { ChangeDetectionStrategy, Component, inject, OnInit, signal } from '@angular/core';
import { TableModule } from 'primeng/table';
import { TranslocoPipe } from '@jsverse/transloco';
import { AdminApiService, TenantIntegrationHealthDto } from '../../../core/api/admin-api.service';
import { EnabizMode } from '../../../core/api/settings-api.models';
import { TrDatePipe } from '../../../shared/pipes/tr-date.pipe';

const ENABIZ_MODE_KEYS: Record<number, string> = {
  [EnabizMode.Disabled]: 'disabled',
  [EnabizMode.Held]: 'held',
  [EnabizMode.TestOnly]: 'test',
  [EnabizMode.Live]: 'live',
};

/** Kiraci bazinda entegrasyon sagligi — hatali satirlar kirmizi vurgulanir. */
@Component({
  selector: 'app-admin-integration-health',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TableModule, TranslocoPipe, TrDatePipe],
  template: `
    <div class="flex flex-col gap-3" data-testid="admin-integration-health">
      <h2 class="m-0 text-lg font-semibold text-slate-800">
        {{ 'admin.health.title' | transloco }}
      </h2>

      @if (loading()) {
        <p class="text-slate-400 text-sm py-8 text-center">{{ 'common.loading' | transloco }}</p>
      } @else {
        @for (tenant of tenants(); track tenant.tenantId) {
          <div class="dt-card p-4 flex flex-col gap-3" [attr.data-testid]="'health-tenant-' + tenant.tenantId">
            <div class="flex items-center gap-3 flex-wrap">
              <span class="font-semibold text-slate-800">{{ tenant.tenantName }}</span>
              <span class="rounded-full bg-slate-100 px-2 py-0.5 text-[11px] font-semibold text-slate-600">
                {{ 'admin.health.enabizMode' | transloco }}:
                {{ 'settings.integrations.enabiz.' + enabizModeKey(tenant.enabizMode) | transloco }}
              </span>
              <span
                class="rounded-full px-2 py-0.5 text-[11px] font-semibold"
                [class.bg-emerald-100]="tenant.ktsRegistered"
                [class.text-emerald-700]="tenant.ktsRegistered"
                [class.bg-amber-100]="!tenant.ktsRegistered"
                [class.text-amber-700]="!tenant.ktsRegistered"
              >
                KTS: {{ (tenant.ktsRegistered ? 'common.yes' : 'common.no') | transloco }}
              </span>
            </div>

            <p-table [value]="tenant.integrations" styleClass="p-datatable-sm" dataKey="integrationKey">
              <ng-template #header>
                <tr>
                  <th>{{ 'admin.health.integration' | transloco }}</th>
                  <th>{{ 'settings.integrations.provider' | transloco }}</th>
                  <th>{{ 'settings.integrations.environment' | transloco }}</th>
                  <th>{{ 'admin.health.lastSuccess' | transloco }}</th>
                  <th>{{ 'admin.health.lastFailure' | transloco }}</th>
                  <th class="text-right!">{{ 'admin.health.calls24h' | transloco }}</th>
                  <th class="text-right!">{{ 'admin.health.failures24h' | transloco }}</th>
                  <th>{{ 'admin.health.lastError' | transloco }}</th>
                </tr>
              </ng-template>
              <ng-template #body let-row>
                <tr [class.bg-rose-50]="row.failureCount24h > 0">
                  <td class="font-medium text-slate-700">
                    {{ 'settings.integrations.keys.' + row.integrationKey | transloco }}
                    @if (!row.hasCredentials) {
                      <i
                        class="fa-solid fa-triangle-exclamation ml-1 text-amber-500"
                        aria-hidden="true"
                      ></i>
                    }
                  </td>
                  <td class="text-slate-500">{{ row.providerKey || '—' }}</td>
                  <td class="text-slate-500">{{ row.environment }}</td>
                  <td class="text-slate-500 text-xs">
                    {{ row.lastSuccessUtc ? (row.lastSuccessUtc | trDate: 'dd.MM.yyyy HH:mm') : '—' }}
                  </td>
                  <td class="text-xs" [class.text-rose-600]="row.lastFailureUtc">
                    {{ row.lastFailureUtc ? (row.lastFailureUtc | trDate: 'dd.MM.yyyy HH:mm') : '—' }}
                  </td>
                  <td class="text-right!">{{ row.callCount24h }}</td>
                  <td class="text-right!" [class.text-rose-600]="row.failureCount24h > 0">
                    {{ row.failureCount24h }}
                  </td>
                  <td class="text-xs text-rose-600 max-w-64 truncate" [title]="row.lastError">
                    {{ row.lastError || '—' }}
                  </td>
                </tr>
              </ng-template>
            </p-table>
          </div>
        } @empty {
          <p class="text-slate-400 text-sm py-8 text-center">{{ 'table.empty' | transloco }}</p>
        }
      }
    </div>
  `,
})
export class AdminIntegrationHealthComponent implements OnInit {
  private readonly api = inject(AdminApiService);

  protected readonly tenants = signal<TenantIntegrationHealthDto[]>([]);
  protected readonly loading = signal(true);

  ngOnInit(): void {
    this.api.integrationHealth().subscribe({
      next: (items) => {
        this.tenants.set(items);
        this.loading.set(false);
      },
      error: () => {
        this.tenants.set([]);
        this.loading.set(false);
      },
    });
  }

  protected enabizModeKey(mode: number): string {
    return ENABIZ_MODE_KEYS[mode] ?? 'disabled';
  }
}
