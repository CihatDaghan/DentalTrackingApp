import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
  untracked,
} from '@angular/core';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { SelectModule } from 'primeng/select';
import { SelectButtonModule } from 'primeng/selectbutton';
import { CheckboxModule } from 'primeng/checkbox';
import { TextareaModule } from 'primeng/textarea';
import { AccordionModule } from 'primeng/accordion';
import { TooltipModule } from 'primeng/tooltip';
import { FormsModule } from '@angular/forms';
import { MessageService } from 'primeng/api';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { ClinicalApiService } from '../../../../../core/api/clinical-api.service';
import {
  AnamnesisAnswerType,
  AnamnesisQuestionDto,
  AnamnesisResponseDto,
  AnamnesisTemplateDto,
  AnamnesisTemplateListItemDto,
} from '../../../../../core/api/clinical-api.models';
import { HasPermissionDirective } from '../../../../../core/auth/has-permission.directive';
import { TrDatePipe } from '../../../../../shared/pipes/tr-date.pipe';
import { PatientDetailStore } from '../../patient-detail.store';

/** Form durumundaki tek soru: cevap alanlari soru turune gore dolar. */
interface QuestionState {
  question: AnamnesisQuestionDto;
  options: string[];
  boolValue: boolean | null;
  textValue: string | null;
  selectedOptions: string[];
}

/**
 * Anamnez sekmesi: sablon gudumlu form (YesNo/YesNoDetail/Text/MultiSelect) +
 * gecmis doldurmalarin salt okunur listesi. Kritik cevaplar kaydedince
 * PatientDetailStore.reloadCriticalFlags ile baslik rozeti guncellenir.
 */
@Component({
  selector: 'app-anamnesis-tab',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    FormsModule,
    ButtonModule,
    DialogModule,
    SelectModule,
    SelectButtonModule,
    CheckboxModule,
    TextareaModule,
    AccordionModule,
    TooltipModule,
    TranslocoPipe,
    HasPermissionDirective,
    TrDatePipe,
  ],
  templateUrl: './anamnesis-tab.component.html',
})
export class AnamnesisTabComponent {
  private readonly store = inject(PatientDetailStore);
  private readonly api = inject(ClinicalApiService);
  private readonly messageService = inject(MessageService);
  private readonly transloco = inject(TranslocoService);

  protected readonly AnswerType = AnamnesisAnswerType;

  protected readonly history = signal<AnamnesisResponseDto[]>([]);
  protected readonly loading = signal(false);
  protected readonly templates = signal<AnamnesisTemplateListItemDto[]>([]);

  // Yeni doldurma formu
  protected readonly fillVisible = signal(false);
  protected readonly saving = signal(false);
  protected readonly selectedTemplateId = signal<number | null>(null);
  protected readonly activeTemplate = signal<AnamnesisTemplateDto | null>(null);
  protected readonly questions = signal<QuestionState[]>([]);

  // Gecmis goruntuleme
  protected readonly viewTarget = signal<AnamnesisResponseDto | null>(null);

  protected readonly yesNoOptions = computed(() => [
    { label: this.transloco.translate('common.yes'), value: true },
    { label: this.transloco.translate('common.no'), value: false },
  ]);

  constructor() {
    effect(() => {
      const patient = this.store.patient();
      if (patient) {
        untracked(() => this.loadHistory(patient.id));
      }
    });
    this.api.anamnesisTemplates().subscribe({
      next: (templates) => this.templates.set(templates),
      error: () => this.templates.set([]),
    });
  }

  private loadHistory(patientId: number): void {
    this.loading.set(true);
    this.api.patientAnamnesis(patientId).subscribe({
      next: (history) => {
        this.history.set(history);
        this.loading.set(false);
      },
      error: () => {
        this.history.set([]);
        this.loading.set(false);
      },
    });
  }

  // --- Yeni doldurma --------------------------------------------------------

  protected openFill(): void {
    const defaultTemplate =
      this.templates().find((t) => t.isDefault) ?? this.templates()[0] ?? null;
    this.selectedTemplateId.set(defaultTemplate?.id ?? null);
    this.activeTemplate.set(null);
    this.questions.set([]);
    this.fillVisible.set(true);
    if (defaultTemplate) {
      this.loadTemplate(defaultTemplate.id);
    }
  }

  protected onTemplateChange(id: number | null): void {
    this.selectedTemplateId.set(id);
    this.activeTemplate.set(null);
    this.questions.set([]);
    if (id != null) {
      this.loadTemplate(id);
    }
  }

  private loadTemplate(id: number): void {
    this.api.anamnesisTemplate(id).subscribe({
      next: (template) => {
        this.activeTemplate.set(template);
        this.questions.set(
          [...template.questions]
            .sort((a, b) => a.sortOrder - b.sortOrder)
            .map((question) => ({
              question,
              options: this.parseOptions(question.optionsJson),
              boolValue: null,
              textValue: null,
              selectedOptions: [],
            })),
        );
      },
    });
  }

  private parseOptions(json: string | null): string[] {
    if (!json) {
      return [];
    }
    try {
      const parsed = JSON.parse(json) as unknown;
      return Array.isArray(parsed) ? parsed.map(String) : [];
    } catch {
      return [];
    }
  }

  protected save(): void {
    const patientId = this.store.patient()?.id;
    const templateId = this.selectedTemplateId();
    if (!patientId || templateId == null) {
      return;
    }
    // Arka uc bos yanit kabul etmiyor (BoolValue veya TextValue dolu olmali) —
    // yanitlanmamis sorular istege hic konmaz.
    const answers = this.questions()
      .map((q) => ({
        questionId: q.question.id,
        boolValue:
          q.question.answerType === AnamnesisAnswerType.YesNo ||
          q.question.answerType === AnamnesisAnswerType.YesNoDetail
            ? q.boolValue
            : null,
        textValue:
          q.question.answerType === AnamnesisAnswerType.MultiSelect
            ? q.selectedOptions.length
              ? JSON.stringify(q.selectedOptions)
              : null
            : q.question.answerType === AnamnesisAnswerType.Text
              ? (q.textValue?.trim() || null)
              : q.boolValue
                ? (q.textValue?.trim() || null)
                : null,
      }))
      .filter((a) => a.boolValue !== null || !!a.textValue);

    if (answers.length === 0) {
      this.messageService.add({
        severity: 'warn',
        summary: this.transloco.translate('anamnesis.answerRequired'),
        life: 3000,
      });
      return;
    }

    this.saving.set(true);
    this.api
      .fillAnamnesis(patientId, { templateId, answers })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.fillVisible.set(false);
          this.messageService.add({
            severity: 'success',
            summary: this.transloco.translate('anamnesis.saveSuccess'),
            life: 3000,
          });
          this.loadHistory(patientId);
          // Alerji vb. kritik cevaplar basliktaki rozeti besler.
          this.store.reloadCriticalFlags();
        },
        error: () => this.saving.set(false),
      });
  }

  // --- Gecmis ---------------------------------------------------------------

  protected answerDisplay(answer: {
    answerType: number;
    boolValue: boolean | null;
    textValue: string | null;
  }): string {
    if (
      answer.answerType === AnamnesisAnswerType.YesNo ||
      answer.answerType === AnamnesisAnswerType.YesNoDetail
    ) {
      if (answer.boolValue == null) {
        return '—';
      }
      const base = this.transloco.translate(answer.boolValue ? 'common.yes' : 'common.no');
      return answer.textValue ? `${base} — ${answer.textValue}` : base;
    }
    if (answer.answerType === AnamnesisAnswerType.MultiSelect && answer.textValue) {
      try {
        const parsed = JSON.parse(answer.textValue) as unknown;
        if (Array.isArray(parsed)) {
          return parsed.join(', ');
        }
      } catch {
        // duz metin olarak goster
      }
    }
    return answer.textValue ?? '—';
  }

  protected isPositiveCritical(answer: { isCritical: boolean; boolValue: boolean | null; textValue: string | null }): boolean {
    return answer.isCritical && (answer.boolValue === true || !!answer.textValue);
  }
}
