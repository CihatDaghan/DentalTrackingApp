import { ChangeDetectionStrategy, Component, computed, signal } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

import { ToothChartComponent } from '../../shared/components/tooth-chart/tooth-chart.component';
import { ToothChartLegendComponent } from '../../shared/components/tooth-chart/tooth-chart-legend.component';
import {
  Dentition,
  SurfaceClickEvent,
  ToothLayer,
} from '../../shared/components/tooth-chart/tooth-chart.models';
import {
  ADULT_SAMPLE,
  MIXED_OVERRIDES,
  PRIMARY_SAMPLE,
  SAMPLE_LEGEND,
} from './tooth-chart-sample-data';

interface DevLogEntry {
  time: string;
  text: string;
}

/**
 * Dis semasi dev onizleme sayfasi (`/app/dev/tooth-chart`).
 * C asamasinda gercek tedavi ekranina evrilecek.
 */
@Component({
  selector: 'app-tooth-chart-dev',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ToothChartComponent, ToothChartLegendComponent, TranslocoPipe],
  templateUrl: './tooth-chart-dev.page.html',
  styleUrl: './tooth-chart-dev.page.scss',
})
export class ToothChartDevPage {
  protected readonly dentition = signal<Dentition>('adult');
  protected readonly activeLayer = signal<ToothLayer>('treatment');
  protected readonly readonly = signal(false);
  protected readonly mixed = signal(false);

  protected readonly selected = signal<string[]>([]);
  protected readonly events = signal<DevLogEntry[]>([]);

  protected readonly legendItems = SAMPLE_LEGEND;
  protected readonly layers: ToothLayer[] = ['diagnosis', 'plan', 'treatment'];

  protected readonly teeth = computed(() =>
    this.dentition() === 'adult' ? ADULT_SAMPLE : PRIMARY_SAMPLE,
  );

  protected readonly overrides = computed(() =>
    this.mixed() && this.dentition() === 'adult' ? MIXED_OVERRIDES : {},
  );

  protected setDentition(value: Dentition): void {
    this.dentition.set(value);
    this.selected.set([]);
  }

  protected onToothClick(toothNo: string): void {
    this.log(`toothClick → ${toothNo}`);
  }

  protected onSurfaceClick(event: SurfaceClickEvent): void {
    this.log(`surfaceClick → ${event.toothNo} / ${event.surface}`);
  }

  protected onSelectionChange(selection: string[]): void {
    this.selected.set(selection);
    this.log(`selectionChange → [${selection.join(', ')}]`);
  }

  private log(text: string): void {
    const time = new Date().toLocaleTimeString('tr-TR');
    this.events.update((list) => [{ time, text }, ...list].slice(0, 40));
  }
}
