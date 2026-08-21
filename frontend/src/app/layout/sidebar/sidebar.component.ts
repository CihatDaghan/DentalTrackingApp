import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { TooltipModule } from 'primeng/tooltip';
import { TranslocoPipe } from '@jsverse/transloco';

interface SidebarItem {
  key: string;
  icon: string;
  route?: string;
}

/** 64px koyu ikon rail — hover'da tooltip, aktif ikon mavi. */
@Component({
  selector: 'app-sidebar',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, RouterLinkActive, TooltipModule, TranslocoPipe],
  templateUrl: './sidebar.component.html',
  styleUrl: './sidebar.component.scss',
})
export class SidebarComponent {
  protected readonly items: SidebarItem[] = [
    { key: 'dashboard', icon: 'fa-solid fa-gauge-high', route: '/app/dashboard' },
    { key: 'calendar', icon: 'fa-solid fa-calendar-days', route: '/app/calendar' },
    { key: 'patients', icon: 'fa-solid fa-hospital-user', route: '/app/patients' },
    { key: 'catalog', icon: 'fa-solid fa-book-medical', route: '/app/catalog' },
    { key: 'cash', icon: 'fa-solid fa-cash-register', route: '/app/cash' },
    { key: 'invoices', icon: 'fa-solid fa-file-invoice', route: '/app/invoices' },
    { key: 'laboratory', icon: 'fa-solid fa-flask', route: '/app/laboratory' },
    { key: 'inventory', icon: 'fa-solid fa-boxes-stacked', route: '/app/inventory' },
    { key: 'reports', icon: 'fa-solid fa-chart-column', route: '/app/reports' },
    { key: 'messaging', icon: 'fa-solid fa-comment-sms', route: '/app/messaging' },
    { key: 'settings', icon: 'fa-solid fa-gear', route: '/app/settings' },
  ];
}
