/**
 * Arka uc dis verisini (`GET /patients/{id}/teeth`) ToothChartComponent'in
 * ToothState[] modeline cevirir: yuzey bit bayragi -> Surface[], status -> katman,
 * ToothCondition -> eksik/implant/kanal isaretleri.
 */
import {
  ToothLayerEntry,
  ToothState,
  ToothLayer,
} from '../../../../../shared/components/tooth-chart/tooth-chart.models';
import {
  PatientTeethDto,
  surfacesFromFlags,
  ToothCondition,
  ToothMarkDto,
  TreatmentRecordStatus,
} from '../../../../../core/api/treatment-api.models';

const FALLBACK_COLOR = '#3b82f6';
const MISSING_COLOR = '#6b7280';
const IMPLANT_COLOR = '#10b981';
const ROOT_CANAL_COLOR = '#ef4444';

/** Backend status -> sema katmani. Cancelled kayitlar API'de zaten elenmistir. */
export function statusToLayer(status: TreatmentRecordStatus): ToothLayer {
  switch (status) {
    case TreatmentRecordStatus.Diagnosis:
      return 'diagnosis';
    case TreatmentRecordStatus.Planned:
      return 'plan';
    default:
      return 'treatment';
  }
}

function markToEntry(mark: ToothMarkDto): ToothLayerEntry {
  return {
    layer: statusToLayer(mark.status),
    treatmentCode: String(mark.treatmentRecordId),
    treatmentName: mark.treatmentName,
    color: mark.categoryColorHex ?? FALLBACK_COLOR,
    surfaces: surfacesFromFlags(mark.surfaces),
    rootCanal: (mark.rootCanalCount ?? 0) > 0 || undefined,
  };
}

/** Kalici dis durumunu (ToothStatus.Condition) sentetik katman kaydina cevirir. */
function conditionEntry(condition: ToothCondition): ToothLayerEntry | null {
  switch (condition) {
    case ToothCondition.Missing:
    case ToothCondition.Extracted:
      return { layer: 'treatment', treatmentCode: 'condition', color: MISSING_COLOR, missing: true };
    case ToothCondition.Implant:
      return { layer: 'treatment', treatmentCode: 'condition', color: IMPLANT_COLOR, implant: true };
    case ToothCondition.RootCanalTreated:
      return {
        layer: 'treatment',
        treatmentCode: 'condition',
        color: ROOT_CANAL_COLOR,
        rootCanal: true,
      };
    default:
      return null;
  }
}

export function mapTeethToStates(dto: PatientTeethDto): ToothState[] {
  return dto.teeth.map((tooth) => {
    const layers = tooth.marks.map(markToEntry);
    const extra = conditionEntry(tooth.condition);
    if (extra) {
      // Kanal isareti zaten marklardan geliyorsa condition kaydini yinelemeyelim.
      const duplicate =
        extra.rootCanal === true && layers.some((l) => l.rootCanal && l.layer === 'treatment');
      if (!duplicate) {
        layers.push(extra);
      }
    }
    return { toothNo: tooth.toothNumber, layers };
  });
}
