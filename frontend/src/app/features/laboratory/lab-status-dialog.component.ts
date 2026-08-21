import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  untracked,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { SelectModule } from 'primeng/select';
import { TextareaModule } from 'primeng/textarea';
import { MessageService } from 'primeng/api';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { LaboratoryApiService } from '../../core/api/laboratory-api.service';
import {
  LAB_ALL_STATUSES,
  LAB_STATUS_KEYS,
  LabCaseDto,
  LabCaseStatus,
} from '../../core/api/laboratory-api.models';
import { injectTranslationSignal } from '../../shared/utils/transloco-signal';

/** Lab vakasi durum degistirme dialogu (durum + opsiyonel not). */
@Component({
  selector: 'app-lab-status-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [FormsModule, ButtonModule, DialogModule, SelectModule, TextareaModule, TranslocoPipe],
  template: `
    <p-dialog
      [visible]="visible()"
      (visibleChange)="visibleChange.emit($event)"
      [modal]="true"
      [focusOnShow]="false"
      [style]="{ width: '26rem' }"
      [header]="'laboratory.changeStatus' | transloco"
    >
      @if (labCase(); as c) {
        <div class="flex flex-col gap-3 pt-1" data-testid="lab-status-dialog">
          <p class="m-0 text-sm text-slate-500">
            {{ c.caseNo }} · {{ c.patientName }} · {{ c.workType }}
          </p>
          <div class="flex flex-col gap-1">
            <label for="lab-status" class="text-sm font-medium">
              {{ 'laboratory.form.status' | transloco }}
            </label>
            <p-select
              inputId="lab-status"
              [options]="statusOptions()"
              optionLabel="label"
              optionValue="value"
              [ngModel]="status()"
              (ngModelChange)="status.set($event)"
              [fluid]="true"
              appendTo="body"
              data-testid="lab-status-select"
            />
          </div>
          <div class="flex flex-col gap-1">
            <label for="lab-status-note" class="text-sm font-medium">
              {{ 'laboratory.form.note' | transloco }}
            </label>
            <textarea
              id="lab-status-note"
              pTextarea
              rows="3"
              class="w-full"
              [ngModel]="note()"
              (ngModelChange)="note.set($event)"
              data-testid="lab-status-note"
            ></textarea>
          </div>
          <div class="flex justify-end gap-2 pt-1">
            <p-button
              [label]="'common.cancel' | transloco"
              severity="secondary"
              [outlined]="true"
              (onClick)="visibleChange.emit(false)"
            />
            <p-button
              [label]="'common.save' | transloco"
              [loading]="saving()"
              data-testid="lab-status-save"
              (onClick)="save()"
            />
          </div>
        </div>
      }
    </p-dialog>
  `,
})
export class LabStatusDialogComponent {
  private readonly api = inject(LaboratoryApiService);
  private readonly messageService = inject(MessageService);
  private readonly transloco = inject(TranslocoService);

  readonly visible = input(false);
  readonly visibleChange = output<boolean>();
  readonly labCase = input<LabCaseDto | null>(null);
  readonly changed = output<LabCaseDto>();

  /** Ceviriler yuklendiginde/dil degistiginde secenek listeleri yeniden hesaplansin. */
  private readonly translation = injectTranslationSignal();

  protected readonly saving = signal(false);
  protected readonly status = signal<LabCaseStatus>(LabCaseStatus.Draft);
  protected readonly note = signal('');

  protected readonly statusOptions = computed(() => {
    this.translation();
    return LAB_ALL_STATUSES.map((value) => ({
      label: this.transloco.translate('laboratory.status.' + LAB_STATUS_KEYS[value]),
      value,
    }));
  });

  constructor() {
    effect(() => {
      const current = this.labCase();
      if (this.visible() && current) {
        untracked(() => {
          this.status.set(current.status);
          this.note.set('');
        });
      }
    });
  }

  protected save(): void {
    const current = this.labCase();
    if (!current) {
      return;
    }
    this.saving.set(true);
    this.api
      .changeStatus(current.id, { status: this.status(), note: this.note() || null })
      .subscribe({
        next: (updated) => {
          this.saving.set(false);
          this.messageService.add({
            severity: 'success',
            summary: this.transloco.translate('laboratory.statusChanged'),
            life: 3000,
          });
          this.changed.emit(updated);
          this.visibleChange.emit(false);
        },
        error: () => this.saving.set(false),
      });
  }
}
