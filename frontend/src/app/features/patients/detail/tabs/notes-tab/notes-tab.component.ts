import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
  untracked,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { TextareaModule } from 'primeng/textarea';
import { CheckboxModule } from 'primeng/checkbox';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmationService, MessageService } from 'primeng/api';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { ClinicalApiService } from '../../../../../core/api/clinical-api.service';
import { PatientNoteDto } from '../../../../../core/api/clinical-api.models';
import { AuthStore } from '../../../../../core/auth/auth.store';
import { UserType } from '../../../../../core/api/auth-api.models';
import { HasPermissionDirective } from '../../../../../core/auth/has-permission.directive';
import { TrDatePipe } from '../../../../../shared/pipes/tr-date.pipe';
import { PatientDetailStore } from '../../patient-detail.store';

/** Not kartlarinin renk paleti (pastel arka planlar). */
const NOTE_COLORS = ['#fef9c3', '#dbeafe', '#dcfce7', '#fce7f3', '#ffedd5', '#f1f5f9'];

/** Owner/Manager her notu, digerleri yalniz kendi notunu duzenler/siler (arka uc 403 ile destekler). */
function isManagerType(userType: UserType | null | undefined): boolean {
  return userType === UserType.Owner || userType === UserType.Manager;
}

/** Notlar sekmesi: sabitlenenler ustte, renkli kartlar, yazar/yonetici duzenleme kurali. */
@Component({
  selector: 'app-notes-tab',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    ButtonModule,
    DialogModule,
    TextareaModule,
    CheckboxModule,
    TooltipModule,
    TranslocoPipe,
    HasPermissionDirective,
    TrDatePipe,
  ],
  templateUrl: './notes-tab.component.html',
})
export class NotesTabComponent {
  private readonly store = inject(PatientDetailStore);
  private readonly api = inject(ClinicalApiService);
  private readonly authStore = inject(AuthStore);
  private readonly messageService = inject(MessageService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly transloco = inject(TranslocoService);
  private readonly fb = inject(FormBuilder);

  protected readonly colors = NOTE_COLORS;

  protected readonly notes = signal<PatientNoteDto[]>([]);
  protected readonly loading = signal(false);
  protected readonly editorVisible = signal(false);
  protected readonly saving = signal(false);
  protected readonly editing = signal<PatientNoteDto | null>(null);
  protected readonly selectedColor = signal<string>(NOTE_COLORS[0]);

  protected readonly form = this.fb.group({
    noteText: this.fb.control<string>('', [Validators.required]),
    isPinned: this.fb.nonNullable.control(false),
  });

  /** Sabitlenenler ustte, sonra en yeni. */
  protected readonly sortedNotes = computed(() =>
    [...this.notes()].sort((a, b) => {
      if (a.isPinned !== b.isPinned) {
        return a.isPinned ? -1 : 1;
      }
      return b.createdAtUtc.localeCompare(a.createdAtUtc);
    }),
  );

  constructor() {
    effect(() => {
      const patient = this.store.patient();
      if (patient) {
        untracked(() => this.load(patient.id));
      }
    });
  }

  private load(patientId: number): void {
    this.loading.set(true);
    this.api.notes(patientId).subscribe({
      next: (notes) => {
        this.notes.set(notes);
        this.loading.set(false);
      },
      error: () => {
        this.notes.set([]);
        this.loading.set(false);
      },
    });
  }

  /** UI gizleme kurali; asil yetki denetimi arka ucta (403). */
  protected canManage(note: PatientNoteDto): boolean {
    const user = this.authStore.user();
    if (!user) {
      return false;
    }
    if (user.isSuperAdmin || isManagerType(user.userType)) {
      return true;
    }
    return user.id === note.authorUserId;
  }

  protected openNew(): void {
    this.editing.set(null);
    this.selectedColor.set(NOTE_COLORS[0]);
    this.form.reset({ noteText: '', isPinned: false });
    this.editorVisible.set(true);
  }

  protected openEdit(note: PatientNoteDto): void {
    this.editing.set(note);
    this.selectedColor.set(note.colorHex ?? NOTE_COLORS[0]);
    this.form.reset({ noteText: note.noteText, isPinned: note.isPinned });
    this.editorVisible.set(true);
  }

  protected save(): void {
    const patientId = this.store.patient()?.id;
    if (!patientId || this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const request = {
      noteText: v.noteText ?? '',
      isPinned: v.isPinned,
      colorHex: this.selectedColor(),
    };
    this.saving.set(true);
    const editing = this.editing();
    const call = editing
      ? this.api.updateNote(patientId, editing.id, request)
      : this.api.createNote(patientId, request);
    call.subscribe({
      next: () => {
        this.saving.set(false);
        this.editorVisible.set(false);
        this.messageService.add({
          severity: 'success',
          summary: this.transloco.translate('notes.saveSuccess'),
          life: 3000,
        });
        this.load(patientId);
      },
      error: () => this.saving.set(false),
    });
  }

  /** Sabitleme durumunu kart uzerinden hizli degistir. */
  protected togglePin(note: PatientNoteDto): void {
    const patientId = this.store.patient()?.id;
    if (!patientId) {
      return;
    }
    this.api
      .updateNote(patientId, note.id, {
        noteText: note.noteText,
        isPinned: !note.isPinned,
        colorHex: note.colorHex,
      })
      .subscribe({ next: () => this.load(patientId) });
  }

  protected remove(note: PatientNoteDto): void {
    const patientId = this.store.patient()?.id;
    if (!patientId) {
      return;
    }
    this.confirmation.confirm({
      header: this.transloco.translate('notes.deleteTitle'),
      message: this.transloco.translate('notes.deleteMessage'),
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
        this.api.deleteNote(patientId, note.id).subscribe({
          next: () => {
            this.messageService.add({
              severity: 'success',
              summary: this.transloco.translate('notes.deleteSuccess'),
              life: 3000,
            });
            this.load(patientId);
          },
        });
      },
    });
  }
}
