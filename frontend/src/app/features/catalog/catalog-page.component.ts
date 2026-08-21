import { ChangeDetectionStrategy, Component } from '@angular/core';
import { TabsModule } from 'primeng/tabs';
import { TranslocoPipe } from '@jsverse/transloco';
import { CatalogTabComponent } from './catalog-tab.component';
import { PriceListsTabComponent } from './price-lists-tab.component';

/** Tedavi katalogu yonetimi (`/app/catalog`) — iki sekme: Katalog + Fiyat Listeleri (§2.7). */
@Component({
  selector: 'app-catalog-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TabsModule, TranslocoPipe, CatalogTabComponent, PriceListsTabComponent],
  template: `
    <div class="catalog-page">
      <p-tabs value="catalog">
        <p-tablist>
          <p-tab value="catalog" data-testid="tab-catalog">
            <i class="fa-solid fa-book-medical mr-2" aria-hidden="true"></i>
            {{ 'catalog.tabs.catalog' | transloco }}
          </p-tab>
          <p-tab value="prices" data-testid="tab-prices">
            <i class="fa-solid fa-tags mr-2" aria-hidden="true"></i>
            {{ 'catalog.tabs.priceLists' | transloco }}
          </p-tab>
        </p-tablist>
        <p-tabpanels>
          <p-tabpanel value="catalog">
            <app-catalog-tab />
          </p-tabpanel>
          <p-tabpanel value="prices">
            <app-price-lists-tab />
          </p-tabpanel>
        </p-tabpanels>
      </p-tabs>
    </div>
  `,
  styles: `
    .catalog-page ::ng-deep {
      .p-tabpanels {
        background: transparent;
        padding: 1rem 0 0;
      }

      .p-tablist-tab-list {
        background: transparent;
        border-color: #e2e8f0;
      }

      .p-tab {
        background: transparent;
      }
    }
  `,
})
export class CatalogPageComponent {}
