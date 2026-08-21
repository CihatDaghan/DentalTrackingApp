import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  linkedSignal,
  output,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { TranslocoService } from '@jsverse/transloco';

import {
  Dentition,
  Surface,
  SurfaceClickEvent,
  ToothLayer,
  ToothLayerEntry,
  ToothState,
} from './tooth-chart.models';
import { IMPLANT_SHAPE, TOOTH_TEMPLATES, ToothShapeKind, ToothTemplate } from './tooth-shapes';

/* ---------- Geometri sabitleri (viewBox birimi) ---------- */
const VIEW_W = 1200;
const VIEW_H = 660;
const CELL_W = 70;
const QUAD_GAP = 40;
const MID_X = VIEW_W / 2;
const UPPER_CERVICAL_Y = 150;
const LOWER_CERVICAL_Y = 510;
const UPPER_BOX_CY = 270;
const LOWER_BOX_CY = 390;
const UPPER_NUM_Y = 318;
const LOWER_NUM_Y = 352;
const HALO_UPPER_Y = 34;
const HALO_LOWER_Y = 358;
const HALO_H = 268;

/** Yuzey besgeni hucre path'leri (kutu merkezi orijinde, dis kenar 52x52). */
const SURFACE_CELLS: { pos: 'top' | 'left' | 'right' | 'bottom' | 'center'; d: string }[] = [
  { pos: 'top', d: 'M -26 -26 L 26 -26 L 11 -11 L -11 -11 Z' },
  { pos: 'left', d: 'M -26 -26 L -11 -11 L -11 11 L -26 26 Z' },
  { pos: 'right', d: 'M 26 -26 L 11 -11 L 11 11 L 26 26 Z' },
  { pos: 'bottom', d: 'M -26 26 L 26 26 L 11 11 L -11 11 Z' },
  { pos: 'center', d: 'M -11 -11 L 11 -11 L 11 11 L -11 11 Z' },
];

const LAYER_ORDER: Record<ToothLayer, number> = { diagnosis: 0, plan: 1, treatment: 2 };

/** Pozisyona gore hafif boyut farklari (anatomik gerceklik icin). */
const ADULT_UPPER_SCALE = [1, 0.86, 0.96, 0.9, 0.9, 1, 0.95, 0.85];
const ADULT_LOWER_SCALE = [0.72, 0.78, 0.88, 0.9, 0.92, 1, 0.95, 0.85];
const PRIMARY_UPPER_SCALE = [1, 0.9, 0.95, 0.92, 1];
const PRIMARY_LOWER_SCALE = [0.8, 0.85, 0.92, 0.92, 1];

interface LayerMark {
  layer: ToothLayer;
  color: string;
  fill: string;
  fillOpacity: number;
  stroke: string;
}

interface SurfaceCellVM {
  surface: Surface;
  d: string;
  marks: LayerMark[];
  label: string;
}

interface ToothVM {
  no: string;
  cx: number;
  upper: boolean;
  anatomyTransform: string;
  boxTransform: string;
  numY: number;
  haloX: number;
  haloY: number;
  haloH: number;
  template: ToothTemplate;
  crownMarks: LayerMark[];
  surfaceCells: SurfaceCellVM[];
  rootCanal: LayerMark | null;
  implant: LayerMark | null;
  missing: boolean;
  missingX: string;
  tooltip: string;
  aria: string;
}

interface SlotDef {
  /** Ceyrek rakami (daimi 1-4). */
  quadrant: number;
  /** Ceyrek ici pozisyon 1..8 (1 = orta hatta en yakin). */
  pos: number;
  upper: boolean;
  /** Sira ici kolon indeksi (soldan). */
  col: number;
  /** Orta hattin solunda mi? (mezial sag tarafta demektir) */
  leftOfMidline: boolean;
}

let nextUid = 0;

/**
 * Interaktif dis semasi (odontogram) — design-1.md §2.6.
 * Tek responsive SVG; 4 ceyrek; dis basina anatomik kuron + kok + yuzey besgeni.
 */
@Component({
  selector: 'app-tooth-chart',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './tooth-chart.component.html',
  styleUrl: './tooth-chart.component.scss',
})
export class ToothChartComponent {
  private readonly transloco = inject(TranslocoService);
  /** Ceviri degisince tooltip/aria yeniden hesaplansin diye sinyal. */
  private readonly lang = toSignal(this.transloco.langChanges$, {
    initialValue: this.transloco.getActiveLang(),
  });

  /** SVG pattern id'lerinin sayfada benzersizligi icin bilesen kimligi. */
  protected readonly uid = `tc${nextUid++}`;

  /* ---------- Girdiler ---------- */
  readonly dentition = input<Dentition>('adult');
  readonly teeth = input<ToothState[]>([]);
  readonly activeLayer = input<ToothLayer>('treatment');
  readonly readonly = input(false);
  /** Karma dentisyon: dis bazinda sut/daimi gosterim override'i (anahtar: FDI no). */
  readonly mixedOverrides = input<Record<string, Dentition>>({});

  /* ---------- Ciktilar ---------- */
  readonly toothClick = output<string>();
  readonly surfaceClick = output<SurfaceClickEvent>();
  readonly selectionChange = output<string[]>();

  protected readonly viewBox = `0 0 ${VIEW_W} ${VIEW_H}`;
  protected readonly midX = MID_X;
  protected readonly implantShape = IMPLANT_SHAPE;

  /** Coklu secim durumu — dentisyon degisince sifirlanir. */
  private readonly selection = linkedSignal<Dentition, ReadonlySet<string>>({
    source: this.dentition,
    computation: () => new Set<string>(),
  });

  protected readonly selectedSet = computed(() => this.selection());

  protected readonly svgClass = computed(
    () => `tooth-chart focus-${this.activeLayer()}${this.readonly() ? ' readonly' : ''}`,
  );

  /** Tani katmaninda kullanilan renkler icin capraz tarama pattern'leri. */
  protected readonly hatchPatterns = computed(() => {
    const colors = new Set<string>();
    for (const tooth of this.teeth()) {
      for (const entry of tooth.layers) {
        if (entry.layer === 'diagnosis') {
          colors.add(entry.color);
        }
      }
    }
    return [...colors].map((color) => ({ color, id: this.hatchId(color) }));
  });

  /* ---------- Gorunum modeli ---------- */
  protected readonly teethVM = computed<ToothVM[]>(() => {
    this.lang(); // ceviri degisiminde yeniden hesapla
    const dent = this.dentition();
    const overrides = this.mixedOverrides();
    const states = new Map(this.teeth().map((t) => [t.toothNo, t]));

    return this.buildSlots(dent).map((slot) => this.buildToothVM(slot, dent, overrides, states));
  });

  protected readonly chartAria = computed(() => {
    this.lang();
    return this.transloco.translate('toothChart.title');
  });

  /* ---------- Olaylar ---------- */
  protected onToothClick(event: MouseEvent | KeyboardEvent, tooth: ToothVM): void {
    if (this.readonly()) {
      return;
    }
    const multi = event.ctrlKey || event.metaKey;
    const next = new Set(multi ? this.selection() : []);
    if (multi && next.has(tooth.no)) {
      next.delete(tooth.no);
    } else if (!multi && this.selection().has(tooth.no) && this.selection().size === 1) {
      next.clear(); // ayni dise tekrar tik = secimi kaldir
    } else {
      next.add(tooth.no);
    }
    this.selection.set(next);
    this.toothClick.emit(tooth.no);
    this.selectionChange.emit([...next]);
  }

  protected onToothKey(event: Event, tooth: ToothVM): void {
    event.preventDefault();
    this.onToothClick(event as KeyboardEvent, tooth);
  }

  protected onSurfaceClick(event: MouseEvent, tooth: ToothVM, surface: Surface): void {
    event.stopPropagation();
    if (this.readonly()) {
      return;
    }
    this.surfaceClick.emit({ toothNo: tooth.no, surface });
  }

  /** Programatik secim temizleme (ust bilesenler icin). */
  clearSelection(): void {
    this.selection.set(new Set());
    this.selectionChange.emit([]);
  }

  /* ---------- VM kurulumu ---------- */

  private buildSlots(dent: Dentition): SlotDef[] {
    const perQuad = dent === 'adult' ? 8 : 5;
    // Ust sira: [sol ceyrek ters sirali] + [sag ceyrek duz]; alt sira aynisi.
    const rows: { upper: boolean; leftQ: number; rightQ: number }[] =
      dent === 'adult'
        ? [
            { upper: true, leftQ: 1, rightQ: 2 },
            { upper: false, leftQ: 4, rightQ: 3 },
          ]
        : [
            { upper: true, leftQ: 5, rightQ: 6 },
            { upper: false, leftQ: 8, rightQ: 7 },
          ];

    const slots: SlotDef[] = [];
    for (const row of rows) {
      for (let col = 0; col < perQuad * 2; col++) {
        const leftOfMidline = col < perQuad;
        const quadrant = leftOfMidline ? row.leftQ : row.rightQ;
        const pos = leftOfMidline ? perQuad - col : col - perQuad + 1;
        slots.push({ quadrant, pos, upper: row.upper, col, leftOfMidline });
      }
    }
    return slots;
  }

  private cellCx(col: number, dent: Dentition): number {
    const perQuad = dent === 'adult' ? 8 : 5;
    const rowW = perQuad * 2 * CELL_W + QUAD_GAP;
    const x0 = (VIEW_W - rowW) / 2;
    return x0 + col * CELL_W + CELL_W / 2 + (col >= perQuad ? QUAD_GAP : 0);
  }

  private buildToothVM(
    slot: SlotDef,
    dent: Dentition,
    overrides: Record<string, Dentition>,
    states: Map<string, ToothState>,
  ): ToothVM {
    const { quadrant, pos, upper } = slot;

    // FDI numaralari: daimi ceyrek q -> sut karsiligi q+4 (yalniz pos 1-5).
    const adultQ = dent === 'adult' ? quadrant : quadrant - 4;
    const adultNo = `${adultQ}${pos}`;
    const primaryNo = pos <= 5 ? `${adultQ + 4}${pos}` : null;

    let effective: Dentition = dent;
    const override = overrides[adultNo] ?? (primaryNo ? overrides[primaryNo] : undefined);
    if (override && (override === 'adult' || primaryNo)) {
      effective = override;
    }
    const no = effective === 'primary' && primaryNo ? primaryNo : adultNo;

    const template = TOOTH_TEMPLATES[this.shapeKind(effective, upper, pos)];
    const scale = this.toothScale(effective, upper, pos);

    const cx = this.cellCx(slot.col, dent);
    const cervicalY = upper ? UPPER_CERVICAL_Y : LOWER_CERVICAL_Y;
    const anatomyTransform = `translate(${cx} ${cervicalY}) scale(${scale} ${upper ? scale : -scale})`;
    const boxTransform = `translate(${cx} ${upper ? UPPER_BOX_CY : LOWER_BOX_CY})`;

    const state = states.get(no);
    const entries = [...(state?.layers ?? [])].sort(
      (a, b) => LAYER_ORDER[a.layer] - LAYER_ORDER[b.layer],
    );

    const missing = entries.some((e) => e.missing);
    const implantEntry = entries.filter((e) => e.implant).at(-1) ?? null;
    const canalEntry = entries.filter((e) => e.rootCanal).at(-1) ?? null;

    const crownMarks = entries
      .filter((e) => !e.missing && !e.implant && !e.rootCanal && !e.surfaces?.length)
      .map((e) => this.markPaint(e.layer, e.color));

    const surfaceCells = this.buildSurfaceCells(slot, entries);

    const isMolarLike = effective === 'adult' ? pos >= 4 : pos >= 4;
    const tooltip = this.buildTooltip(no, entries, isMolarLike);

    return {
      no,
      cx,
      upper,
      anatomyTransform,
      boxTransform,
      numY: upper ? UPPER_NUM_Y : LOWER_NUM_Y,
      haloX: cx - 33,
      haloY: upper ? HALO_UPPER_Y : HALO_LOWER_Y,
      haloH: HALO_H,
      template,
      crownMarks,
      surfaceCells,
      rootCanal: canalEntry
        ? this.markPaint(canalEntry.layer, canalEntry.color, canalEntry.layer === 'plan' ? 0.25 : 1)
        : null,
      implant: implantEntry ? this.markPaint(implantEntry.layer, implantEntry.color) : null,
      missing,
      missingX: `M -20 8 L 20 ${template.crownH - 6} M 20 8 L -20 ${template.crownH - 6}`,
      tooltip,
      aria: tooltip.replace(/\n/g, ', '),
    };
  }

  private buildSurfaceCells(slot: SlotDef, entries: ToothLayerEntry[]): SurfaceCellVM[] {
    // Orta hattin solundaki ceyreklerde mezial sag tarafa duser (aynalama).
    const mesialOnRight = slot.leftOfMidline;
    const posToSurface: Record<string, Surface> = {
      top: 'B',
      bottom: 'L',
      center: 'O',
      left: mesialOnRight ? 'D' : 'M',
      right: mesialOnRight ? 'M' : 'D',
    };
    return SURFACE_CELLS.map((cell) => {
      const surface = posToSurface[cell.pos];
      const marks = entries
        .filter((e) => !e.missing && e.surfaces?.includes(surface))
        .map((e) => this.markPaint(e.layer, e.color));
      return { surface, d: cell.d, marks, label: surface };
    });
  }

  private shapeKind(dent: Dentition, upper: boolean, pos: number): ToothShapeKind {
    if (dent === 'primary') {
      if (pos <= 2) return 'pIncisor';
      if (pos === 3) return 'pCanine';
      return upper ? 'pMolarUpper' : 'pMolarLower';
    }
    if (pos <= 2) return 'incisor';
    if (pos === 3) return 'canine';
    if (pos <= 5) return 'premolar';
    return upper ? 'molarUpper' : 'molarLower';
  }

  private toothScale(dent: Dentition, upper: boolean, pos: number): number {
    const table =
      dent === 'primary'
        ? upper
          ? PRIMARY_UPPER_SCALE
          : PRIMARY_LOWER_SCALE
        : upper
          ? ADULT_UPPER_SCALE
          : ADULT_LOWER_SCALE;
    return table[pos - 1] ?? 1;
  }

  private markPaint(layer: ToothLayer, color: string, planOpacity = 0.45): LayerMark {
    switch (layer) {
      case 'diagnosis':
        return { layer, color, fill: `url(#${this.hatchId(color)})`, fillOpacity: 1, stroke: color };
      case 'plan':
        return { layer, color, fill: color, fillOpacity: planOpacity, stroke: color };
      case 'treatment':
        return { layer, color, fill: color, fillOpacity: 1, stroke: 'none' };
    }
  }

  private hatchId(color: string): string {
    return `${this.uid}-hatch-${color.replace(/[^a-zA-Z0-9]/g, '')}`;
  }

  private buildTooltip(no: string, entries: ToothLayerEntry[], molarLike: boolean): string {
    const t = (key: string) => this.transloco.translate(key);
    const head = `${t('toothChart.tooth')} ${no}`;
    if (!entries.length) {
      return `${head} — ${t('toothChart.noRecords')}`;
    }
    const lines = entries.map((e) => {
      const name = e.treatmentName ?? e.treatmentCode;
      const layerLabel = t(`toothChart.layers.${e.layer}`);
      const extras: string[] = [];
      if (e.surfaces?.length) {
        // Kesici/kanin icin okluzal yerine insizal göster
        extras.push(e.surfaces.map((s) => (s === 'O' && !molarLike ? 'I' : s)).join(''));
      }
      if (e.rootCanal) extras.push(t('toothChart.legend.rootCanal'));
      if (e.missing) extras.push(t('toothChart.legend.missing'));
      if (e.implant) extras.push(t('toothChart.legend.implant'));
      return `${name} · ${layerLabel}${extras.length ? ` · ${extras.join(', ')}` : ''}`;
    });
    return [head, ...lines].join('\n');
  }
}
