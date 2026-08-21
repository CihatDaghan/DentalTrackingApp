import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';
import { AppointmentStatus, RecallStatus } from '../../../core/api/api.models';
import { TreatmentRecordStatus } from '../../../core/api/treatment-api.models';
import { InstallmentStatus, LedgerEntryType } from '../../../core/api/finance-api.models';
import { ConsentFormStatus } from '../../../core/api/clinical-api.models';
import { PrescriptionStatus } from '../../../core/api/prescription-api.models';
import { LAB_STATUS_KEYS, LabCaseStatus } from '../../../core/api/laboratory-api.models';
import {
  MESSAGE_KIND_KEYS,
  MESSAGE_STATE_KEYS,
  MessageKind,
  OutboundMessageState,
  PAYMENT_INTENT_STATUS_KEYS,
  PaymentIntentStatus,
  WA_TEMPLATE_STATUS_KEYS,
  WaTemplateStatus,
} from '../../../core/api/messaging-api.models';

interface TagStyle {
  bg: string;
  fg: string;
  strike?: boolean;
}

const APPOINTMENT_STYLES: Record<number, TagStyle> = {
  [AppointmentStatus.Scheduled]: { bg: '#dbeafe', fg: '#1d4ed8' },
  [AppointmentStatus.Confirmed]: { bg: '#dcfce7', fg: '#15803d' },
  [AppointmentStatus.Arrived]: { bg: '#ffedd5', fg: '#c2410c' },
  [AppointmentStatus.InChair]: { bg: '#ede9fe', fg: '#6d28d9' },
  [AppointmentStatus.Completed]: { bg: '#f1f5f9', fg: '#475569' },
  [AppointmentStatus.Cancelled]: { bg: '#fee2e2', fg: '#b91c1c', strike: true },
  [AppointmentStatus.NoShow]: { bg: '#fecdd3', fg: '#881337' },
};

const RECALL_STYLES: Record<number, TagStyle> = {
  [RecallStatus.Planned]: { bg: '#dbeafe', fg: '#1d4ed8' },
  [RecallStatus.ConvertedToAppointment]: { bg: '#dcfce7', fg: '#15803d' },
  [RecallStatus.Arrived]: { bg: '#ccfbf1', fg: '#0f766e' },
  [RecallStatus.Abandoned]: { bg: '#f1f5f9', fg: '#64748b' },
};

const APPOINTMENT_KEYS: Record<number, string> = {
  [AppointmentStatus.Scheduled]: 'scheduled',
  [AppointmentStatus.Confirmed]: 'confirmed',
  [AppointmentStatus.Arrived]: 'arrived',
  [AppointmentStatus.InChair]: 'inChair',
  [AppointmentStatus.Completed]: 'completed',
  [AppointmentStatus.Cancelled]: 'cancelled',
  [AppointmentStatus.NoShow]: 'noShow',
};

const RECALL_KEYS: Record<number, string> = {
  [RecallStatus.Planned]: 'planned',
  [RecallStatus.ConvertedToAppointment]: 'converted',
  [RecallStatus.Arrived]: 'arrived',
  [RecallStatus.Abandoned]: 'abandoned',
};

/** Tedavi kaydi katman rozeti: Tani sari / Plan mavi / Yapildi yesil / Iptal gri. */
const TREATMENT_STYLES: Record<number, TagStyle> = {
  [TreatmentRecordStatus.Diagnosis]: { bg: '#fef3c7', fg: '#b45309' },
  [TreatmentRecordStatus.Planned]: { bg: '#dbeafe', fg: '#1d4ed8' },
  [TreatmentRecordStatus.Done]: { bg: '#dcfce7', fg: '#15803d' },
  [TreatmentRecordStatus.Cancelled]: { bg: '#f1f5f9', fg: '#64748b', strike: true },
};

const TREATMENT_KEYS: Record<number, string> = {
  [TreatmentRecordStatus.Diagnosis]: 'diagnosis',
  [TreatmentRecordStatus.Planned]: 'planned',
  [TreatmentRecordStatus.Done]: 'done',
  [TreatmentRecordStatus.Cancelled]: 'cancelled',
};

/** Cari ekstre satir turu: borc kirmizi tonlar, alacak yesil tonlar. */
const LEDGER_STYLES: Record<number, TagStyle> = {
  [LedgerEntryType.TreatmentCharge]: { bg: '#fee2e2', fg: '#b91c1c' },
  [LedgerEntryType.PaymentIn]: { bg: '#dcfce7', fg: '#15803d' },
  [LedgerEntryType.Refund]: { bg: '#ffedd5', fg: '#c2410c' },
  [LedgerEntryType.Discount]: { bg: '#ccfbf1', fg: '#0f766e' },
  [LedgerEntryType.OpeningBalance]: { bg: '#f1f5f9', fg: '#475569' },
  [LedgerEntryType.CompanyTransfer]: { bg: '#ede9fe', fg: '#6d28d9' },
  [LedgerEntryType.Correction]: { bg: '#fef3c7', fg: '#b45309' },
};

const LEDGER_KEYS: Record<number, string> = {
  [LedgerEntryType.TreatmentCharge]: 'treatmentCharge',
  [LedgerEntryType.PaymentIn]: 'paymentIn',
  [LedgerEntryType.Refund]: 'refund',
  [LedgerEntryType.Discount]: 'discount',
  [LedgerEntryType.OpeningBalance]: 'openingBalance',
  [LedgerEntryType.CompanyTransfer]: 'companyTransfer',
  [LedgerEntryType.Correction]: 'correction',
};

const INSTALLMENT_STYLES: Record<number, TagStyle> = {
  [InstallmentStatus.Pending]: { bg: '#dbeafe', fg: '#1d4ed8' },
  [InstallmentStatus.Partial]: { bg: '#fef3c7', fg: '#b45309' },
  [InstallmentStatus.Paid]: { bg: '#dcfce7', fg: '#15803d' },
  [InstallmentStatus.Overdue]: { bg: '#fee2e2', fg: '#b91c1c' },
};

const INSTALLMENT_KEYS: Record<number, string> = {
  [InstallmentStatus.Pending]: 'pending',
  [InstallmentStatus.Partial]: 'partial',
  [InstallmentStatus.Paid]: 'paid',
  [InstallmentStatus.Overdue]: 'overdue',
};

const CONSENT_STYLES: Record<number, TagStyle> = {
  [ConsentFormStatus.Draft]: { bg: '#f1f5f9', fg: '#475569' },
  [ConsentFormStatus.SentBySms]: { bg: '#dbeafe', fg: '#1d4ed8' },
  [ConsentFormStatus.Signed]: { bg: '#dcfce7', fg: '#15803d' },
  [ConsentFormStatus.Expired]: { bg: '#fef3c7', fg: '#b45309' },
  [ConsentFormStatus.Declined]: { bg: '#fee2e2', fg: '#b91c1c' },
};

const CONSENT_KEYS: Record<number, string> = {
  [ConsentFormStatus.Draft]: 'draft',
  [ConsentFormStatus.SentBySms]: 'sentBySms',
  [ConsentFormStatus.Signed]: 'signed',
  [ConsentFormStatus.Expired]: 'expired',
  [ConsentFormStatus.Declined]: 'declined',
};

const PRESCRIPTION_STYLES: Record<number, TagStyle> = {
  [PrescriptionStatus.Draft]: { bg: '#f1f5f9', fg: '#475569' },
  [PrescriptionStatus.Printed]: { bg: '#dbeafe', fg: '#1d4ed8' },
  [PrescriptionStatus.SubmittedToUss]: { bg: '#ede9fe', fg: '#6d28d9' },
  [PrescriptionStatus.Accepted]: { bg: '#dcfce7', fg: '#15803d' },
  [PrescriptionStatus.Rejected]: { bg: '#fee2e2', fg: '#b91c1c' },
};

const PRESCRIPTION_KEYS: Record<number, string> = {
  [PrescriptionStatus.Draft]: 'draft',
  [PrescriptionStatus.Printed]: 'printed',
  [PrescriptionStatus.SubmittedToUss]: 'submittedToUss',
  [PrescriptionStatus.Accepted]: 'accepted',
  [PrescriptionStatus.Rejected]: 'rejected',
};

/** Lab vakasi akisi: taslak gri -> gonderim mavi -> labda mor -> prova turuncu -> geldi/takildi yesil. */
const LAB_CASE_STYLES: Record<number, TagStyle> = {
  [LabCaseStatus.Draft]: { bg: '#f1f5f9', fg: '#475569' },
  [LabCaseStatus.Sent]: { bg: '#dbeafe', fg: '#1d4ed8' },
  [LabCaseStatus.InLab]: { bg: '#ede9fe', fg: '#6d28d9' },
  [LabCaseStatus.TryIn]: { bg: '#ffedd5', fg: '#c2410c' },
  [LabCaseStatus.Received]: { bg: '#ccfbf1', fg: '#0f766e' },
  [LabCaseStatus.Delivered]: { bg: '#dcfce7', fg: '#15803d' },
  [LabCaseStatus.Redo]: { bg: '#fee2e2', fg: '#b91c1c' },
};

/** Outbox durum makinesi: kuyrukta gri, gonderiliyor sari, gonderildi mavi, iletildi yesil, hata kirmizi, atlandi turuncu. */
const MESSAGE_STATE_STYLES: Record<number, TagStyle> = {
  [OutboundMessageState.Pending]: { bg: '#f1f5f9', fg: '#475569' },
  [OutboundMessageState.Sending]: { bg: '#fef3c7', fg: '#b45309' },
  [OutboundMessageState.Sent]: { bg: '#dbeafe', fg: '#1d4ed8' },
  [OutboundMessageState.Delivered]: { bg: '#dcfce7', fg: '#15803d' },
  [OutboundMessageState.Failed]: { bg: '#fee2e2', fg: '#b91c1c' },
  [OutboundMessageState.Skipped]: { bg: '#ffedd5', fg: '#c2410c' },
};

/** IYS/KVKK ayrimi: bilgilendirme notr, ticari dikkat cekici. */
const MESSAGE_KIND_STYLES: Record<number, TagStyle> = {
  [MessageKind.Transactional]: { bg: '#e0f2fe', fg: '#0369a1' },
  [MessageKind.Commercial]: { bg: '#fce7f3', fg: '#be185d' },
};

const WA_TEMPLATE_STYLES: Record<number, TagStyle> = {
  [WaTemplateStatus.Draft]: { bg: '#f1f5f9', fg: '#475569' },
  [WaTemplateStatus.Submitted]: { bg: '#fef3c7', fg: '#b45309' },
  [WaTemplateStatus.Approved]: { bg: '#dcfce7', fg: '#15803d' },
  [WaTemplateStatus.Rejected]: { bg: '#fee2e2', fg: '#b91c1c' },
};

const PAYMENT_INTENT_STYLES: Record<number, TagStyle> = {
  [PaymentIntentStatus.Created]: { bg: '#f1f5f9', fg: '#475569' },
  [PaymentIntentStatus.LinkSent]: { bg: '#dbeafe', fg: '#1d4ed8' },
  [PaymentIntentStatus.Paid]: { bg: '#dcfce7', fg: '#15803d' },
  [PaymentIntentStatus.Failed]: { bg: '#fee2e2', fg: '#b91c1c' },
  [PaymentIntentStatus.Expired]: { bg: '#ffedd5', fg: '#c2410c' },
  [PaymentIntentStatus.Refunded]: { bg: '#ede9fe', fg: '#6d28d9' },
};

type TagKind =
  | 'appointment'
  | 'recall'
  | 'treatment'
  | 'ledger'
  | 'installment'
  | 'consent'
  | 'prescription'
  | 'labCase'
  | 'messageState'
  | 'messageKind'
  | 'waTemplate'
  | 'paymentIntent';

const STYLE_MAPS: Record<TagKind, Record<number, TagStyle>> = {
  appointment: APPOINTMENT_STYLES,
  recall: RECALL_STYLES,
  treatment: TREATMENT_STYLES,
  ledger: LEDGER_STYLES,
  installment: INSTALLMENT_STYLES,
  consent: CONSENT_STYLES,
  prescription: PRESCRIPTION_STYLES,
  labCase: LAB_CASE_STYLES,
  messageState: MESSAGE_STATE_STYLES,
  messageKind: MESSAGE_KIND_STYLES,
  waTemplate: WA_TEMPLATE_STYLES,
  paymentIntent: PAYMENT_INTENT_STYLES,
};

const KEY_MAPS: Record<TagKind, Record<number, string>> = {
  appointment: APPOINTMENT_KEYS,
  recall: RECALL_KEYS,
  treatment: TREATMENT_KEYS,
  ledger: LEDGER_KEYS,
  installment: INSTALLMENT_KEYS,
  consent: CONSENT_KEYS,
  prescription: PRESCRIPTION_KEYS,
  labCase: LAB_STATUS_KEYS,
  messageState: MESSAGE_STATE_KEYS,
  messageKind: MESSAGE_KIND_KEYS,
  waTemplate: WA_TEMPLATE_STATUS_KEYS,
  paymentIntent: PAYMENT_INTENT_STATUS_KEYS,
};

const KEY_PREFIXES: Record<TagKind, string> = {
  appointment: 'appointmentStatus.',
  recall: 'recallStatus.',
  treatment: 'treatmentStatus.',
  ledger: 'ledgerEntryType.',
  installment: 'installmentStatus.',
  consent: 'consentStatus.',
  prescription: 'prescriptionStatus.',
  labCase: 'laboratory.status.',
  messageState: 'messageState.',
  messageKind: 'messageKind.',
  waTemplate: 'waTemplateStatus.',
  paymentIntent: 'paymentIntentStatus.',
};

/** Durum rozeti (randevu/recall/tedavi/ekstre/taksit/onam/recete/lab) — renk + i18n etiketi tek yerden. */
@Component({
  selector: 'app-status-tag',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [TranslocoPipe],
  template: `
    <span
      class="status-tag"
      [style.background]="style().bg"
      [style.color]="style().fg"
      [style.text-decoration]="style().strike ? 'line-through' : 'none'"
    >
      {{ labelKey() | transloco }}
    </span>
  `,
  styles: `
    .status-tag {
      display: inline-block;
      padding: 0.2rem 0.6rem;
      border-radius: 999px;
      font-size: 0.75rem;
      font-weight: 600;
      white-space: nowrap;
      line-height: 1.2;
    }
  `,
})
export class StatusTagComponent {
  readonly kind = input.required<TagKind>();
  readonly value = input.required<number>();

  protected readonly style = computed<TagStyle>(
    () => STYLE_MAPS[this.kind()][this.value()] ?? { bg: '#f1f5f9', fg: '#475569' },
  );

  protected readonly labelKey = computed(() => {
    const kind = this.kind();
    return KEY_PREFIXES[kind] + (KEY_MAPS[kind][this.value()] ?? 'unknown');
  });
}
