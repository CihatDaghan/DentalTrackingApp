import {
  ApplicationConfig,
  isDevMode,
  provideBrowserGlobalErrorListeners,
  provideZoneChangeDetection,
} from '@angular/core';
import { provideRouter, withComponentInputBinding } from '@angular/router';
import { provideHttpClient, withFetch, withInterceptors } from '@angular/common/http';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { providePrimeNG } from 'primeng/config';
import { definePreset } from '@primeuix/themes';
import Aura from '@primeuix/themes/aura';
import { provideTransloco } from '@jsverse/transloco';
import { provideTranslocoPersistLang } from '@jsverse/transloco-persist-lang';

import { ConfirmationService, MessageService } from 'primeng/api';

import { routes } from './app.routes';
import { authInterceptor } from './core/auth/auth.interceptor';
import { errorInterceptor } from './core/interceptors/error.interceptor';
import { TranslocoHttpLoader } from './transloco-loader';

/**
 * Aura preset'i, primary rengi Tailwind `blue` skalasiyla (#3b82f6) esitlenmis halde.
 * Boylece PrimeNG `primary` tokenlari ile tailwindcss-primeui `primary` alias'i ayni skalaya baglanir.
 */
const DentalPreset = definePreset(Aura, {
  semantic: {
    primary: {
      50: '#eff6ff',
      100: '#dbeafe',
      200: '#bfdbfe',
      300: '#93c5fd',
      400: '#60a5fa',
      500: '#3b82f6',
      600: '#2563eb',
      700: '#1d4ed8',
      800: '#1e40af',
      900: '#1e3a8a',
      950: '#172554',
    },
  },
});

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes, withComponentInputBinding()),
    provideHttpClient(withFetch(), withInterceptors([authInterceptor, errorInterceptor])),
    // p-toast + p-confirmdialog (App kokunde render edilir)
    MessageService,
    ConfirmationService,
    // PrimeNG'nin bazi bilesenleri (or. p-message) hala Angular animations kullaniyor
    provideAnimationsAsync(),
    providePrimeNG({
      theme: {
        preset: DentalPreset,
        options: {
          darkModeSelector: false,
        },
      },
      ripple: true,
    }),
    provideTransloco({
      config: {
        availableLangs: ['tr', 'en'],
        defaultLang: 'tr',
        fallbackLang: 'en',
        reRenderOnLangChange: true,
        prodMode: !isDevMode(),
      },
      loader: TranslocoHttpLoader,
    }),
    provideTranslocoPersistLang({
      storage: { useValue: localStorage },
    }),
  ],
};
