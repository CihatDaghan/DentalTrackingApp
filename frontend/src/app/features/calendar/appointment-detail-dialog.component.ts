import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { TextareaModule } from 'primeng/textarea';
import { ConfirmationService, MessageService } from 'primeng/api';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AppointmentsApiService } from '../../core/api/appointments-api.service';
import {
  AppointmentDto,
  AppointmentStatus,
  AppointmentType,
} from '../../core/api/api.models';
import { HasPermissionDirective } from '../../core/auth/has-permission.directive';
import { StatusTagComponent } from '../../shared/components/status-tag/status-tag.component';
import { TrDatePipe } from '../../shared/pipes/tr-date.pipe';
import { TYPE_KEYS } from './appointment-utils';

/** Randevu detay dialogu: durum gecisleri + duzenle + sil. */
@Component({
  selector: 'app-appointment-detail-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    FormsModule,
    ButtonModule,
    DialogModule,
    TextareaModule,
    TranslocoPipe,
    HasPermissionDirective,
    StatusTagComponent,
    TrDatePipe,
  ],
  templateUrl: './appointment-detail-dialog.component.html',
})
export class AppointmentDetailDialogComponent {
  private readonly appointmentsApi = inject(AppointmentsApiService);
  private readonly messageService = inject(MessageService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly transloco = inject(TranslocoService);

  /** null -> kapali. */
  readonly appointment = input.required<AppointmentDto | null>();
  readonly closed = output<void>();
  /** Durum/silme sonrasi takvimin yeniden yuklenmesi icin. */
  readonly changed = output<void>();
  readonly edit = output<AppointmentDto>();

  protected readonly AppointmentStatus = AppointmentStatus;
  protected readonly AppointmentType = AppointmentType;

  protected readonly busy = signal(false);
  protected readonly cancelMode = signal(false);
  protected cancelReason = '';

  protected readonly typeKey = computed(() => {
    const appointment = this.appointment();
    return appointment ? 'calendar.type.' + (TYPE_KEYS[appointment.type] ?? 'normal') : '';
  });

  constructor() {
    effect(() => {
      this.appointment();
      this.cancelMode.set(false);
      this.cancelReason = '';
    });
  }

  protected setStatus(status: AppointmentStatus): void {
    const appointment = this.appointment();
    if (!appointment) {
      return;
    }
    if (status === AppointmentStatus.Cancelled && !this.cancelMode()) {
      this.cancelMode.set(true);
      return;
    }
    if (status === AppointmentStatus.Cancelled && !this.cancelReason.trim()) {
      this.messageService.add({
        severity: 'warn',
        summary: this.transloco.translate('calendar.cancelReasonRequired'),
        life: 4000,
      });
      return;
    }
    this.busy.set(true);
    this.appointmentsApi
      .updateStatus(appointment.id, {
        status,
        cancelReason:
          status === AppointmentStatus.Cancelled ? this.cancelReason.trim() : null,
      })
      .subscribe({
        next: () => {
          this.busy.set(false);
          this.messageService.add({
            severity: 'success',
            summary: this.transloco.translate('calendar.statusUpdated'),
            life: 3000,
          });
          this.changed.emit();
          this.closed.emit();
        },
        error: () => this.busy.set(false),
      });
  }

  protected remove(): void {
    const appointment = this.appointment();
    if (!appointment) {
      return;
    }
    this.confirmation.confirm({
      header: this.transloco.translate('calendar.deleteTitle'),
      message: this.transloco.translate('calendar.deleteMessage'),
      icon: 'pi pi-exclamation-triangle',
      acceptButtonProps: {
        label: this.transloco.translate('common.delete'),
        severity: 'danger',
      },
      rejectButtonProps: {
        label: this.transloco.translate('common.cancel'),
        severity: 'secondary',
        outlined: true,
      },
      accept: () => {
        this.busy.set(true);
        this.appointmentsApi.delete(appointment.id).subscribe({
          next: () => {
            this.busy.set(false);
            this.messageService.add({
              severity: 'success',
              summary: this.transloco.translate('calendar.deleteSuccess'),
              life: 3000,
            });
            this.changed.emit();
            this.closed.emit();
          },
          error: () => this.busy.set(false),
        });
      },
    });
  }
}
