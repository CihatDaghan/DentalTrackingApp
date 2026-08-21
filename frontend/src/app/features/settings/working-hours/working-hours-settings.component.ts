import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { SelectModule } from 'primeng/select';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { MessageService } from 'primeng/api';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { SettingsApiService } from '../../../core/api/settings-api.service';
import { ClinicWorkingHourItem } from '../../../core/api/settings-api.models';
import { AuthStore } from '../../../core/auth/auth.store';
import { ClinicContext } from '../../../core/auth/clinic-context';
import { HasPermissionDirective } from '../../../core/auth/has-permission.directive';
import { injectTranslationSignal } from '../../../shared/utils/transloco-signal';

/** Pazartesi -> Pazar sirasi (System.DayOfWeek: 0 = Pazar). */
const WEEK_ORDER = [1, 2, 3, 4, 5, 6, 0];

interface DayRow {
  dayOfWeek: number;
  open: Date | null;
  close: Date | null;
  isClosed: boolean;
}

function toTimeDate(value: string | null): Date | null {
  if (!value) {
    return null;
  }
  const [h, m] = value.split(':').map(Number);
  const d = new Date();
  d.setHours(h ?? 0, m ?? 0, 0, 0);
  return d;
}

function toTimeString(value: Date | null): string | null {
  if (!value) {
    return null;
  }
  return `${String(value.getHours()).padStart(2, '0')}:${String(value.getMinutes()).padStart(2, '0')}:00`;
}

/**
 * Klinik calisma saatleri: sube secici + haftalik gun/saat tablosu, toplu kaydet.
 * NOT: Arka uc calisma saatlerini KLINIK (sube) bazinda tutar; hekim bazli
 * saatler ayri `WorkingHoursController` ucundadir.
 */
@Component({
  selector: 'app-working-hours-settings',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    ButtonModule,
    DatePickerModule,
    SelectModule,
    ToggleSwitchModule,
    TranslocoPipe,
    HasPermissionDirective,
  ],
  template: `
    <div class="dt-card p-4 flex flex-col gap-4 max-w-3xl" data-testid="working-hours">
      <div class="flex items-center justify-between gap-3 flex-wrap">
        <div class="flex items-center gap-2">
          @if (clinicOptions().length > 1) {
            <p-select
              [options]="clinicOptions()"
              optionLabel="label"
              optionValue="value"
              [ngModel]="clinicId()"
              (ngModelChange)="onClinicChange($event)"
              size="small"
              data-testid="wh-clinic"
            />
          } @else {
            <span class="text-sm font-medium text-slate-700">{{ clinicLabel() }}</span>
          }
        </div>
        <p-button
          *hasPermission="'settings.update'"
          [label]="'common.save' | transloco"
          icon="fa-solid fa-check"
          size="small"
          [loading]="saving()"
          (onClick)="save()"
          data-testid="wh-save"
        />
      </div>

      @if (loading()) {
        <p class="text-slate-400 text-sm py-8 text-center">{{ 'common.loading' | transloco }}</p>
      } @else {
        <table class="w-full text-sm">
          <thead>
            <tr class="text-left text-slate-500">
              <th class="py-2 font-medium">{{ 'settings.workingHours.day' | transloco }}</th>
              <th class="py-2 font-medium">{{ 'settings.workingHours.open' | transloco }}</th>
              <th class="py-2 font-medium">{{ 'settings.workingHours.close' | transloco }}</th>
              <th class="py-2 font-medium">{{ 'settings.workingHours.closed' | transloco }}</th>
            </tr>
          </thead>
          <tbody>
            @for (row of rows(); track row.dayOfWeek) {
              <tr class="border-t border-slate-100">
                <td class="py-2 font-medium text-slate-700">
                  {{ 'settings.workingHours.days.' + row.dayOfWeek | transloco }}
                </td>
                <td class="py-2">
                  <p-datepicker
                    [ngModel]="row.open"
                    (ngModelChange)="patchRow(row.dayOfWeek, { open: $event })"
                    [timeOnly]="true"
                    hourFormat="24"
                    [disabled]="row.isClosed"
                    size="small"
                    appendTo="body"
                    inputStyleClass="w-24"
                    [attr.data-testid]="'wh-open-' + row.dayOfWeek"
                  />
                </td>
                <td class="py-2">
                  <p-datepicker
                    [ngModel]="row.close"
                    (ngModelChange)="patchRow(row.dayOfWeek, { close: $event })"
                    [timeOnly]="true"
                    hourFormat="24"
                    [disabled]="row.isClosed"
                    size="small"
                    appendTo="body"
                    inputStyleClass="w-24"
                    [attr.data-testid]="'wh-close-' + row.dayOfWeek"
                  />
                </td>
                <td class="py-2">
                  <p-toggleswitch
                    [ngModel]="row.isClosed"
                    (ngModelChange)="patchRow(row.dayOfWeek, { isClosed: $event })"
                    [attr.data-testid]="'wh-closed-' + row.dayOfWeek"
                  />
                </td>
              </tr>
            }
          </tbody>
        </table>
      }
    </div>
  `,
})
export class WorkingHoursSettingsComponent implements OnInit {
  private readonly api = inject(SettingsApiService);
  private readonly authStore = inject(AuthStore);
  private readonly clinicContext = inject(ClinicContext);
  private readonly messageService = inject(MessageService);
  private readonly transloco = inject(TranslocoService);
  private readonly translation = injectTranslationSignal();

  protected readonly rows = signal<DayRow[]>([]);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly clinicId = signal<number | null>(null);

  protected readonly clinicOptions = computed(() =>
    this.authStore.clinics().map((c) => ({ label: c.name, value: c.id })),
  );

  protected readonly clinicLabel = computed(() => {
    this.translation();
    const clinics = this.authStore.clinics();
    return clinics[0]?.name ?? this.transloco.translate('settings.tabs.clinic');
  });

  ngOnInit(): void {
    this.clinicId.set(this.clinicContext.clinicId());
    this.load();
  }

  protected onClinicChange(clinicId: number): void {
    this.clinicId.set(clinicId);
    this.load();
  }

  protected patchRow(dayOfWeek: number, patch: Partial<DayRow>): void {
    this.rows.update((rows) =>
      rows.map((r) => (r.dayOfWeek === dayOfWeek ? { ...r, ...patch } : r)),
    );
  }

  protected save(): void {
    const clinicId = this.clinicId();
    if (clinicId == null) {
      return;
    }
    const items: ClinicWorkingHourItem[] = this.rows().map((r) => ({
      dayOfWeek: r.dayOfWeek,
      openTime: r.isClosed ? null : toTimeString(r.open),
      closeTime: r.isClosed ? null : toTimeString(r.close),
      isClosed: r.isClosed,
    }));
    this.saving.set(true);
    this.api.saveWorkingHours({ clinicId, items }).subscribe({
      next: (saved) => {
        this.applyRows(saved);
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

  private load(): void {
    this.loading.set(true);
    this.api.workingHours(this.clinicId()).subscribe({
      next: (items) => {
        this.applyRows(items);
        this.loading.set(false);
      },
      error: () => {
        this.applyRows([]);
        this.loading.set(false);
      },
    });
  }

  private applyRows(
    items: { dayOfWeek: number; openTime: string | null; closeTime: string | null; isClosed: boolean }[],
  ): void {
    this.rows.set(
      WEEK_ORDER.map((day) => {
        const existing = items.find((i) => i.dayOfWeek === day);
        return {
          dayOfWeek: day,
          open: toTimeDate(existing?.openTime ?? '09:00:00'),
          close: toTimeDate(existing?.closeTime ?? '18:00:00'),
          isClosed: existing?.isClosed ?? day === 0,
        };
      }),
    );
  }
}
