import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { ActivatedRoute, NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { filter, map, startWith } from 'rxjs';
import { SidebarComponent } from '../sidebar/sidebar.component';
import { TopbarComponent } from '../topbar/topbar.component';
import { AnnouncementBannerComponent } from '../announcement-banner/announcement-banner.component';
import { ImpersonationBannerComponent } from '../impersonation-banner/impersonation-banner.component';

/** Oturumlu uygulama kabugu: impersonation/duyuru bandi + sol ikon rail + ust bar + icerik. */
@Component({
  selector: 'app-main-layout',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterOutlet,
    SidebarComponent,
    TopbarComponent,
    AnnouncementBannerComponent,
    ImpersonationBannerComponent,
  ],
  templateUrl: './main-layout.component.html',
  styleUrl: './main-layout.component.scss',
})
export class MainLayoutComponent {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  /** Aktif route'un data.titleKey degeri — topbar basligi. */
  protected readonly titleKey = toSignal(
    this.router.events.pipe(
      filter((event) => event instanceof NavigationEnd),
      startWith(null),
      map(() => {
        let child = this.route.snapshot;
        while (child.firstChild) {
          child = child.firstChild;
        }
        return (child.data['titleKey'] as string | undefined) ?? 'menu.dashboard';
      }),
    ),
    { initialValue: 'menu.dashboard' },
  );
}
