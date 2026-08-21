import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  model,
  OnInit,
  output,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DatePickerModule } from 'primeng/datepicker';
import { SelectModule } from 'primeng/select';
import { SelectButtonModule } from 'primeng/selectbutton';
import { InputNumberModule } from 'primeng/inputnumber';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AppointmentsApiService } from '../../core/api/appointments-api.service';
import { CatalogApiService } from '../../core/api/catalog-api.service';
import { DoctorDto } from '../../core/api/api.models';
import { TreatmentCategoryDto } from '../../core/api/treatment-api.models';
import { ReportGroupBy } from '../../core/api/reports-api.models';
import { AuthStore } from '../../core/auth/auth.store';
import { HasPermissionDirective } from '../../core/auth/has-permission.directive';
import { injectTranslationSignal } from '../../shared/utils/transloco-signal';
import {
  DATE_PRESETS,
  DateRangePreset,
  ReportFilterState,
  ReportTabDef,
  resolvePreset,
} from './report-filter';

/**
 * Raporlarin ortak filtre cubugu: tarih ön ayari (+ özel aralik), hekim, sube,
 * gruplama, kategori, minimum bakiye; sagda "Excel indir".
 */
@Component({
  selector: 'app-report-filter-bar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    ButtonModule,
    DatePickerModule,
    SelectModule,
    SelectButtonModule,
    InputNumberModule,
    TranslocoPipe,
    HasPermissionDirective,
  ],
  template: `
    <div class="dt-card flex flex-wrap items-end gap-3 p-3" data-testid="report-filter-bar">
      @if (tab().showDateRange) {
        <div class="flex flex-col gap-1">
          <label class="text-[11px] font-medium uppercase tracking-wide text-slate-500">
            {{ 'reports.filter.period' | transloco }}
          </label>
          <p-selectbutton
            [options]="presetOptions()"
            optionLabel="label"
            optionValue="value"
            [allowEmpty]="false"
            [ngModel]="state().preset"
            (ngModelChange)="onPreset($event)"
            size="small"
            data-testid="report-preset"
          />
        </div>

        @if (state().preset === 'custom') {
          <div class="flex flex-col gap-1">
            <label class="text-[11px] font-medium uppercase tracking-wide text-slate-500">
              {{ 'reports.filter.from' | transloco }}
            </label>
            <p-datepicker
              [ngModel]="state().from"
              (ngModelChange)="patch({ from: $event })"
              dateFormat="dd.mm.yy"
              [showIcon]="true"
              appendTo="body"
              size="small"
              data-testid="report-from"
            />
          </div>
          <div class="flex flex-col gap-1">
            <label class="text-[11px] font-medium uppercase tracking-wide text-slate-500">
              {{ 'reports.filter.to' | transloco }}
            </label>
            <p-datepicker
              [ngModel]="state().to"
              (ngModelChange)="patch({ to: $event })"
              dateFormat="dd.mm.yy"
              [showIcon]="true"
              appendTo="body"
              size="small"
              data-testid="report-to"
            />
          </div>
        }
      }

      @if (tab().showDoctor) {
        <div class="flex flex-col gap-1">
          <label class="text-[11px] font-medium uppercase tracking-wide text-slate-500">
            {{ 'reports.filter.doctor' | transloco }}
          </label>
          <p-select
            [options]="doctorOptions()"
            optionLabel="label"
            optionValue="value"
            [ngModel]="state().doctorId"
            (ngModelChange)="patch({ doctorId: $event })"
            [style]="{ minWidth: '11rem' }"
            size="small"
            appendTo="body"
            data-testid="report-doctor"
          />
        </div>
      }

      @if (clinicOptions().length > 1) {
        <div class="flex flex-col gap-1">
          <label class="text-[11px] font-medium uppercase tracking-wide text-slate-500">
            {{ 'reports.filter.clinic' | transloco }}
          </label>
          <p-select
            [options]="clinicOptions()"
            optionLabel="label"
            optionValue="value"
            [ngModel]="state().clinicId"
            (ngModelChange)="patch({ clinicId: $event })"
            [style]="{ minWidth: '11rem' }"
            size="small"
            appendTo="body"
            data-testid="report-clinic"
          />
        </div>
      }

      @if (tab().showGroupBy) {
        <div class="flex flex-col gap-1">
          <label class="text-[11px] font-medium uppercase tracking-wide text-slate-500">
            {{ 'reports.filter.groupBy' | transloco }}
          </label>
          <p-selectbutton
            [options]="groupByOptions()"
            optionLabel="label"
            optionValue="value"
            [allowEmpty]="false"
            [ngModel]="state().groupBy"
            (ngModelChange)="patch({ groupBy: $event })"
            size="small"
            data-testid="report-groupby"
          />
        </div>
      }

      @if (tab().showCategory) {
        <div class="flex flex-col gap-1">
          <label class="text-[11px] font-medium uppercase tracking-wide text-slate-500">
            {{ 'reports.filter.category' | transloco }}
          </label>
          <p-select
            [options]="categoryOptions()"
            optionLabel="label"
            optionValue="value"
            [ngModel]="state().categoryId"
            (ngModelChange)="patch({ categoryId: $event })"
            [style]="{ minWidth: '12rem' }"
            size="small"
            appendTo="body"
            data-testid="report-category"
          />
        </div>
      }

      @if (tab().showMinBalance) {
        <div class="flex flex-col gap-1">
          <label class="text-[11px] font-medium uppercase tracking-wide text-slate-500">
            {{ 'reports.filter.minBalance' | transloco }}
          </label>
          <p-inputnumber
            [ngModel]="state().minBalance"
            (ngModelChange)="patch({ minBalance: $event ?? 0.01 })"
            mode="decimal"
            [minFractionDigits]="2"
            [min]="0"
            [showButtons]="false"
            size="small"
            inputStyleClass="w-32"
            data-testid="report-minbalance"
          />
        </div>
      }

      <div class="ml-auto flex items-end gap-2">
        <p-button
          *hasPermission="'report.export'"
          [label]="'reports.exportExcel' | transloco"
          icon="fa-solid fa-file-excel"
          size="small"
          severity="success"
          [outlined]="true"
          [loading]="exporting()"
          (onClick)="exportClick.emit()"
          data-testid="report-export"
        />
      </div>
    </div>
  `,
})
export class ReportFilterBarComponent implements OnInit {
  private readonly appointmentsApi = inject(AppointmentsApiService);
  private readonly catalogApi = inject(CatalogApiService);
  private readonly transloco = inject(TranslocoService);
  private readonly authStore = inject(AuthStore);
  private readonly translation = injectTranslationSignal();

  readonly tab = input.required<ReportTabDef>();
  readonly state = model.required<ReportFilterState>();
  readonly exporting = input(false);
  readonly exportClick = output<void>();

  private readonly doctors = signal<DoctorDto[]>([]);
  private readonly categories = signal<TreatmentCategoryDto[]>([]);

  protected readonly presetOptions = computed(() => {
    this.translation();
    return DATE_PRESETS.map((p) => ({
      label: this.transloco.translate('reports.preset.' + p),
      value: p,
    }));
  });

  protected readonly groupByOptions = computed(() => {
    this.translation();
    return [
      { label: this.transloco.translate('reports.groupBy.day'), value: ReportGroupBy.Day },
      { label: this.transloco.translate('reports.groupBy.week'), value: ReportGroupBy.Week },
      { label: this.transloco.translate('reports.groupBy.month'), value: ReportGroupBy.Month },
    ];
  });

  protected readonly doctorOptions = computed(() => {
    this.translation();
    return [
      { label: this.transloco.translate('common.all'), value: null as number | null },
      ...this.doctors().map((d) => ({
        label: `${d.firstName} ${d.lastName}`,
        value: d.id as number | null,
      })),
    ];
  });

  protected readonly clinicOptions = computed(() => {
    this.translation();
    const clinics = this.authStore.clinics();
    if (clinics.length <= 1) {
      return [];
    }
    return [
      { label: this.transloco.translate('common.all'), value: null as number | null },
      ...clinics.map((c) => ({ label: c.name, value: c.id as number | null })),
    ];
  });

  protected readonly categoryOptions = computed(() => {
    this.translation();
    return [
      { label: this.transloco.translate('common.all'), value: null as number | null },
      ...this.categories().map((c) => ({ label: c.name, value: c.id as number | null })),
    ];
  });

  ngOnInit(): void {
    this.appointmentsApi.doctors().subscribe({
      next: (doctors) => this.doctors.set(doctors),
      error: () => this.doctors.set([]),
    });
    this.catalogApi.categories().subscribe({
      next: (categories) => this.categories.set(categories),
      error: () => this.categories.set([]),
    });
  }

  protected onPreset(preset: DateRangePreset): void {
    const range = resolvePreset(preset);
    this.state.update((s) => ({
      ...s,
      preset,
      from: range ? range.from : s.from,
      to: range ? range.to : s.to,
    }));
  }

  protected patch(patch: Partial<ReportFilterState>): void {
    this.state.update((s) => ({ ...s, ...patch }));
  }
}
