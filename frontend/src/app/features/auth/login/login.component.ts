import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule, NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { ButtonModule } from 'primeng/button';
import { CheckboxModule } from 'primeng/checkbox';
import { InputTextModule } from 'primeng/inputtext';
import { MessageModule } from 'primeng/message';
import { PasswordModule } from 'primeng/password';
import { SelectModule } from 'primeng/select';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { AuthStore } from '../../../core/auth/auth.store';
import { ClinicSummaryDto } from '../../../core/api/auth-api.models';
import { TurnstileComponent } from '../../../shared/components/turnstile/turnstile.component';

type LoginStep = 'credentials' | 'clinic';

@Component({
  selector: 'app-login',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    FormsModule,
    ButtonModule,
    CheckboxModule,
    InputTextModule,
    MessageModule,
    PasswordModule,
    SelectModule,
    TranslocoPipe,
    TurnstileComponent,
  ],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent {
  private readonly fb = inject(NonNullableFormBuilder);
  private readonly authStore = inject(AuthStore);
  private readonly router = inject(Router);
  private readonly transloco = inject(TranslocoService);

  protected readonly form = this.fb.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', Validators.required],
    rememberMe: [false],
  });

  protected readonly step = signal<LoginStep>('credentials');
  protected readonly loading = signal(false);
  protected readonly error = signal(false);
  protected readonly turnstileToken = signal<string | null>(null);
  protected readonly clinics = signal<ClinicSummaryDto[]>([]);
  protected readonly selectedClinicId = signal<number | null>(null);

  protected readonly languages = [
    { label: 'TR', value: 'tr' },
    { label: 'EN', value: 'en' },
  ];

  protected readonly activeLang = toSignal(this.transloco.langChanges$, {
    initialValue: this.transloco.getActiveLang(),
  });

  protected setLang(lang: string): void {
    this.transloco.setActiveLang(lang);
  }

  /** Super admin kullanicilar platform paneline, digerleri uygulamaya iner. */
  private landingRoute(): string {
    return this.authStore.user()?.isSuperAdmin ? '/admin/tenants' : '/app/dashboard';
  }

  protected async submit(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.loading.set(true);
    this.error.set(false);
    const { email, password, rememberMe } = this.form.getRawValue();
    try {
      const response = await this.authStore.login({
        email,
        password,
        rememberMe,
        turnstileToken: this.turnstileToken(),
      });
      if (response.requiresClinicSelection) {
        this.clinics.set(response.clinics ?? []);
        const defaultClinic =
          response.clinics?.find((clinic) => clinic.isDefault) ?? response.clinics?.[0];
        this.selectedClinicId.set(defaultClinic?.id ?? null);
        this.step.set('clinic');
      } else {
        await this.router.navigate([this.landingRoute()]);
      }
    } catch {
      this.error.set(true);
    } finally {
      this.loading.set(false);
    }
  }

  protected async continueWithClinic(): Promise<void> {
    const clinicId = this.selectedClinicId();
    if (!clinicId) {
      return;
    }
    this.loading.set(true);
    try {
      await this.authStore.selectClinic(clinicId);
      await this.router.navigate(['/app/dashboard']);
    } catch {
      this.error.set(true);
    } finally {
      this.loading.set(false);
    }
  }
}
