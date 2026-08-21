import { ChangeDetectionStrategy, Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { MultiSelectModule } from 'primeng/multiselect';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { ToggleSwitchModule } from 'primeng/toggleswitch';
import { ConfirmationService, MessageService } from 'primeng/api';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { SettingsApiService } from '../../../core/api/settings-api.service';
import { RolePermissionsDto, StaffDto } from '../../../core/api/settings-api.models';
import { UserType } from '../../../core/api/auth-api.models';
import { TrDatePipe } from '../../../shared/pipes/tr-date.pipe';
import { injectTranslationSignal } from '../../../shared/utils/transloco-signal';

const USER_TYPE_KEYS: Record<number, string> = {
  [UserType.Owner]: 'owner',
  [UserType.Manager]: 'manager',
  [UserType.Dentist]: 'dentist',
  [UserType.Assistant]: 'assistant',
  [UserType.Secretary]: 'secretary',
  [UserType.Accountant]: 'accountant',
};

interface StaffForm {
  id: number | null;
  firstName: string;
  lastName: string;
  email: string;
  userType: UserType;
  roleIds: number[];
  color: string | null;
  branch: string | null;
  diplomaNo: string | null;
  isActive: boolean;
}

function emptyForm(): StaffForm {
  return {
    id: null,
    firstName: '',
    lastName: '',
    email: '',
    userType: UserType.Assistant,
    roleIds: [],
    color: '#3b82f6',
    branch: null,
    diplomaNo: null,
    isActive: true,
  };
}

/**
 * Personel yonetimi (`settings.staff`): kullanici tablosu, davet dialogu
 * (gecici sifre yanitta gosterilir), duzenleme, sifre sifirlama, pasife alma.
 */
@Component({
  selector: 'app-staff-settings',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    ButtonModule,
    DialogModule,
    InputTextModule,
    MultiSelectModule,
    SelectModule,
    TableModule,
    ToggleSwitchModule,
    TranslocoPipe,
    TrDatePipe,
  ],
  templateUrl: './staff-settings.component.html',
})
export class StaffSettingsComponent implements OnInit {
  private readonly api = inject(SettingsApiService);
  private readonly messageService = inject(MessageService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly transloco = inject(TranslocoService);
  private readonly translation = injectTranslationSignal();

  protected readonly UserType = UserType;

  protected readonly staff = signal<StaffDto[]>([]);
  protected readonly roles = signal<RolePermissionsDto[]>([]);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);

  protected readonly dialogVisible = signal(false);
  protected readonly form = signal<StaffForm>(emptyForm());

  /** Davet/sifre sifirlama sonrasi gosterilen gecici sifre. */
  protected readonly tempPassword = signal<string | null>(null);
  protected readonly tempPasswordUser = signal<string>('');
  protected readonly passwordCopied = signal(false);

  protected readonly userTypeOptions = computed(() => {
    this.translation();
    return Object.entries(USER_TYPE_KEYS).map(([value, key]) => ({
      label: this.transloco.translate('settings.staff.userType.' + key),
      value: Number(value) as UserType,
    }));
  });

  protected readonly roleOptions = computed(() =>
    this.roles().map((r) => ({ label: r.name, value: r.id })),
  );

  protected readonly isDoctor = computed(() => this.form().userType === UserType.Dentist);

  ngOnInit(): void {
    this.load();
    this.api.roles().subscribe({
      next: (roles) => this.roles.set(roles),
      error: () => this.roles.set([]),
    });
  }

  protected userTypeLabel(userType: number): string {
    return 'settings.staff.userType.' + (USER_TYPE_KEYS[userType] ?? 'assistant');
  }

  protected roleNames(row: StaffDto): string {
    return row.roles.length ? row.roles.map((r) => r.name).join(', ') : '—';
  }

  protected openCreate(): void {
    this.form.set(emptyForm());
    this.tempPassword.set(null);
    this.dialogVisible.set(true);
  }

  protected openEdit(row: StaffDto): void {
    this.form.set({
      id: row.id,
      firstName: row.firstName,
      lastName: row.lastName,
      email: row.email,
      userType: row.userType,
      roleIds: row.roles.map((r) => r.id),
      color: row.color ?? '#3b82f6',
      branch: row.branch,
      diplomaNo: row.diplomaNo,
      isActive: row.isActive,
    });
    this.tempPassword.set(null);
    this.dialogVisible.set(true);
  }

  protected patch(patch: Partial<StaffForm>): void {
    this.form.update((f) => ({ ...f, ...patch }));
  }

  protected save(): void {
    const f = this.form();
    if (!f.firstName || !f.lastName || (!f.id && !f.email) || f.roleIds.length === 0) {
      this.messageService.add({
        severity: 'warn',
        summary: this.transloco.translate('validation.formInvalid'),
        life: 4000,
      });
      return;
    }
    this.saving.set(true);
    const doctorFields = {
      color: f.userType === UserType.Dentist ? f.color : null,
      branch: f.userType === UserType.Dentist ? f.branch : null,
      diplomaNo: f.userType === UserType.Dentist ? f.diplomaNo : null,
    };

    if (f.id) {
      this.api
        .updateStaff(f.id, {
          firstName: f.firstName,
          lastName: f.lastName,
          userType: f.userType,
          roleIds: f.roleIds,
          isActive: f.isActive,
          ...doctorFields,
        })
        .subscribe({
          next: () => {
            this.saving.set(false);
            this.dialogVisible.set(false);
            this.load();
            this.toastSaved();
          },
          error: () => this.saving.set(false),
        });
      return;
    }

    this.api
      .inviteStaff({
        email: f.email,
        firstName: f.firstName,
        lastName: f.lastName,
        userType: f.userType,
        roleIds: f.roleIds,
        ...doctorFields,
      })
      .subscribe({
        next: (result) => {
          this.saving.set(false);
          this.tempPassword.set(result.temporaryPassword);
          this.tempPasswordUser.set(result.user.email);
          this.passwordCopied.set(false);
          this.load();
        },
        error: () => this.saving.set(false),
      });
  }

  protected resetPassword(row: StaffDto): void {
    this.api.resetStaffPassword(row.id).subscribe({
      next: (result) => {
        this.tempPassword.set(result.temporaryPassword);
        this.tempPasswordUser.set(row.email);
        this.passwordCopied.set(false);
        this.dialogVisible.set(true);
        this.form.update((f) => ({ ...f, id: row.id }));
      },
      error: () => undefined,
    });
  }

  protected deactivate(row: StaffDto): void {
    this.confirmationService.confirm({
      header: this.transloco.translate('settings.staff.deactivateTitle'),
      message: this.transloco.translate('settings.staff.deactivateMessage', { name: row.fullName }),
      acceptLabel: this.transloco.translate('common.yes'),
      rejectLabel: this.transloco.translate('common.no'),
      acceptButtonStyleClass: 'p-button-danger p-button-sm',
      rejectButtonStyleClass: 'p-button-text p-button-sm',
      accept: () => {
        this.api.deactivateStaff(row.id).subscribe({
          next: () => {
            this.load();
            this.toastSaved();
          },
          error: () => undefined,
        });
      },
    });
  }

  protected copyPassword(): void {
    const password = this.tempPassword();
    if (!password) {
      return;
    }
    void navigator.clipboard?.writeText(password).then(() => this.passwordCopied.set(true));
  }

  protected closeDialog(): void {
    this.dialogVisible.set(false);
    this.tempPassword.set(null);
  }

  private load(): void {
    this.loading.set(true);
    this.api.staff(true).subscribe({
      next: (staff) => {
        this.staff.set(staff);
        this.loading.set(false);
      },
      error: () => {
        this.staff.set([]);
        this.loading.set(false);
      },
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
