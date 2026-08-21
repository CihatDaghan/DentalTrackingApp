/**
 * Chart.js (p-chart) icin ortak tema/secenek yardimcilari.
 * Palet uygulama birincil rengiyle (#3b82f6) hizalidir.
 */

export const CHART_COLORS = {
  primary: '#3b82f6',
  primarySoft: 'rgba(59, 130, 246, 0.15)',
  emerald: '#10b981',
  emeraldSoft: 'rgba(16, 185, 129, 0.15)',
  amber: '#f59e0b',
  rose: '#ef4444',
  violet: '#8b5cf6',
  slate: '#94a3b8',
};

/** Pasta/dilim grafiklerinde kategori sirasina gore donen palet. */
export const CATEGORICAL_PALETTE = [
  '#3b82f6',
  '#10b981',
  '#f59e0b',
  '#8b5cf6',
  '#ef4444',
  '#06b6d4',
  '#ec4899',
  '#84cc16',
  '#f97316',
  '#64748b',
];

export function paletteFor(count: number): string[] {
  return Array.from({ length: count }, (_, i) => CATEGORICAL_PALETTE[i % CATEGORICAL_PALETTE.length]);
}

const moneyFormatter = new Intl.NumberFormat('tr-TR', {
  minimumFractionDigits: 0,
  maximumFractionDigits: 0,
});

export function formatMoneyShort(value: number): string {
  return `${moneyFormatter.format(value)} ₺`;
}

/** Cizgi/cubuk grafikler icin ortak secenekler (para ekseni + tooltip). */
export function moneyChartOptions(options: { stacked?: boolean; horizontal?: boolean } = {}) {
  const horizontal = options.horizontal ?? false;
  // Etiket ekseninde `callback` ANAHTARI hic bulunmamali: chart.js'te
  // `callback: undefined` varsayilan etiket cikticisini ezip index gosterir.
  const moneyTicks = { font: { size: 11 }, color: '#64748b', callback: (v: number | string) => formatMoneyShort(Number(v)) };
  const labelTicks = { font: { size: 11 }, color: '#64748b' };
  return {
    responsive: true,
    maintainAspectRatio: false,
    indexAxis: options.horizontal ? ('y' as const) : ('x' as const),
    interaction: { mode: 'index' as const, intersect: false },
    plugins: {
      legend: {
        position: 'bottom' as const,
        labels: { usePointStyle: true, boxWidth: 8, font: { size: 11 } },
      },
      tooltip: {
        callbacks: {
          label: (ctx: { dataset: { label?: string }; parsed: { x: number; y: number } }) => {
            const value = options.horizontal ? ctx.parsed.x : ctx.parsed.y;
            return `${ctx.dataset.label ?? ''}: ${formatMoneyShort(value ?? 0)}`;
          },
        },
      },
    },
    scales: {
      x: {
        stacked: options.stacked ?? false,
        grid: { display: horizontal, color: '#f1f5f9' },
        ticks: horizontal ? moneyTicks : labelTicks,
      },
      y: {
        stacked: options.stacked ?? false,
        grid: { display: !horizontal, color: '#f1f5f9' },
        ticks: horizontal ? labelTicks : moneyTicks,
      },
    },
  };
}

/** Sayisal (para olmayan) eksen icin secenekler. */
export function countChartOptions() {
  return {
    responsive: true,
    maintainAspectRatio: false,
    interaction: { mode: 'index' as const, intersect: false },
    plugins: {
      legend: {
        position: 'bottom' as const,
        labels: { usePointStyle: true, boxWidth: 8, font: { size: 11 } },
      },
    },
    scales: {
      x: { grid: { display: false }, ticks: { font: { size: 11 }, color: '#64748b' } },
      y: {
        beginAtZero: true,
        grid: { color: '#f1f5f9' },
        ticks: { font: { size: 11 }, color: '#64748b', precision: 0 },
      },
    },
  };
}

/** Pasta/donut secenekleri — tooltip'te para bicimi. */
export function pieChartOptions(money = true) {
  return {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      legend: {
        position: 'right' as const,
        labels: { usePointStyle: true, boxWidth: 8, font: { size: 11 } },
      },
      tooltip: {
        callbacks: {
          label: (ctx: { label?: string; parsed: number }) =>
            `${ctx.label ?? ''}: ${money ? formatMoneyShort(ctx.parsed) : ctx.parsed}`,
        },
      },
    },
  };
}
