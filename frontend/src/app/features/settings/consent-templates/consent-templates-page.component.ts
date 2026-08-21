import { ChangeDetectionStrategy, Component, inject, signal, viewChild } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { Editor, EditorModule } from 'primeng/editor';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { TableModule } from 'primeng/table';
import { CheckboxModule } from 'primeng/checkbox';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmationService, MessageService } from 'primeng/api';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { ConsentsApiService } from '../../../core/api/consents-api.service';
import { ConsentTemplateListItemDto } from '../../../core/api/clinical-api.models';
import { HasPermissionDirective } from '../../../core/auth/has-permission.directive';

/** Sablon govdesine tikla-ekle degisken chip'leri. */
const TEMPLATE_VARIABLES = [
  '{{HastaAdi}}',
  '{{HekimAdi}}',
  '{{KlinikAdi}}',
  '{{Tarih}}',
  '{{Tedavi}}',
];

/**
 * Onam sablonlari yonetimi (/app/settings/consent-templates):
 * liste + p-editor (Quill) ile zengin metin duzenleme + degisken chip'leri.
 */
@Component({
  selector: 'app-consent-templates-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    ButtonModule,
    DialogModule,
    EditorModule,
    InputTextModule,
    SelectModule,
    TableModule,
    CheckboxModule,
    TooltipModule,
    TranslocoPipe,
    HasPermissionDirective,
  ],
  templateUrl: './consent-templates-page.component.html',
})
export class ConsentTemplatesPageComponent {
  private readonly api = inject(ConsentsApiService);
  private readonly messageService = inject(MessageService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly transloco = inject(TranslocoService);
  private readonly fb = inject(FormBuilder);

  protected readonly variables = TEMPLATE_VARIABLES;
  protected readonly localeOptions = [
    { label: 'Türkçe', value: 'tr' },
    { label: 'English', value: 'en' },
  ];

  protected readonly templates = signal<ConsentTemplateListItemDto[]>([]);
  protected readonly loading = signal(false);
  protected readonly editorVisible = signal(false);
  protected readonly saving = signal(false);
  protected readonly editingId = signal<number | null>(null);

  private readonly editor = viewChild(Editor);

  protected readonly form = this.fb.group({
    name: this.fb.control<string>('', [Validators.required]),
    locale: this.fb.nonNullable.control('tr'),
    isActive: this.fb.nonNullable.control(true),
    bodyHtml: this.fb.control<string>('', [Validators.required]),
  });

  constructor() {
    this.load();
  }

  private load(): void {
    this.loading.set(true);
    this.api.templates().subscribe({
      next: (templates) => {
        this.templates.set(templates);
        this.loading.set(false);
      },
      error: () => {
        this.templates.set([]);
        this.loading.set(false);
      },
    });
  }

  protected openNew(): void {
    this.editingId.set(null);
    this.form.reset({ name: '', locale: 'tr', isActive: true, bodyHtml: '' });
    this.editorVisible.set(true);
  }

  protected openEdit(item: ConsentTemplateListItemDto): void {
    this.api.template(item.id).subscribe({
      next: (template) => {
        this.editingId.set(template.id);
        this.form.reset({
          name: template.name,
          locale: template.locale,
          isActive: template.isActive,
          bodyHtml: template.bodyHtml,
        });
        this.editorVisible.set(true);
      },
    });
  }

  /** Degisken chip'i: imlecin oldugu yere Quill uzerinden eklenir. */
  protected insertVariable(variable: string): void {
    const quill = this.editor()?.quill;
    if (!quill) {
      return;
    }
    const range = quill.getSelection(true);
    const index = range?.index ?? quill.getLength();
    quill.insertText(index, variable, 'user');
    quill.setSelection(index + variable.length, 0);
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const request = {
      name: v.name ?? '',
      bodyHtml: v.bodyHtml ?? '',
      locale: v.locale,
      isActive: v.isActive,
    };
    this.saving.set(true);
    const id = this.editingId();
    const call = id != null ? this.api.updateTemplate(id, request) : this.api.createTemplate(request);
    call.subscribe({
      next: () => {
        this.saving.set(false);
        this.editorVisible.set(false);
        this.messageService.add({
          severity: 'success',
          summary: this.transloco.translate('consentTemplates.saveSuccess'),
          life: 3000,
        });
        this.load();
      },
      error: () => this.saving.set(false),
    });
  }

  protected remove(item: ConsentTemplateListItemDto): void {
    this.confirmation.confirm({
      header: this.transloco.translate('consentTemplates.deleteTitle'),
      message: this.transloco.translate('consentTemplates.deleteMessage'),
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
        this.api.deleteTemplate(item.id).subscribe({ next: () => this.load() });
      },
    });
  }
}
