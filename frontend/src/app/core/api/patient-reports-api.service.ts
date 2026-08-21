import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { TreatmentRecordStatus } from './treatment-api.models';

/** Diş durumu (Dental.Domain.Enums.ToothCondition). */
export const ToothCondition = {
  Present: 1,
  Missing: 2,
  Extracted: 3,
  Implant: 4,
  Crown: 5,
  Bridge: 6,
  RootCanalTreated: 7,
  Unerupted: 8,
} as const;
export type ToothCondition = (typeof ToothCondition)[keyof typeof ToothCondition];

export interface PatientTreatmentRowDto {
  date: string | null;
  doctorName: string;
  toothNumber: string | null;
  treatmentName: string;
  price: number;
  discountAmount: number;
  netAmount: number;
  status: TreatmentRecordStatus;
}

export interface PatientTreatmentReportDto {
  patientId: number;
  patientName: string;
  fileNo: string;
  clinicName: string;
  from: string | null;
  to: string | null;
  issuedOn: string;
  rows: PatientTreatmentRowDto[];
  totalGross: number;
  totalDiscount: number;
  totalNet: number;
  pdfFileId: number | null;
}

export interface PatientToothStatusRowDto {
  toothNumber: string;
  condition: ToothCondition;
  conditionText: string;
}

export interface PatientStatusReportDto {
  patientId: number;
  patientName: string;
  fileNo: string;
  clinicName: string;
  birthDate: string | null;
  age: number | null;
  genderText: string | null;
  identityMasked: string | null;
  phone: string | null;
  issuedOn: string;
  doctorName: string;
  diplomaNo: string | null;
  teeth: PatientToothStatusRowDto[];
  treatments: PatientTreatmentRowDto[];
  pdfFileId: number | null;
}

export interface ProformaRequest {
  treatmentRecordIds: number[];
  validUntil?: string | null;
  note?: string | null;
}

export interface ProformaLineDto {
  seqNo: number;
  treatmentName: string;
  toothNumber: string | null;
  unitPrice: number;
  discountAmount: number;
  vatRate: number;
  vatAmount: number;
  lineTotal: number;
}

export interface ProformaDto {
  patientId: number;
  patientName: string;
  fileNo: string;
  clinicName: string;
  issuedOn: string;
  validUntil: string;
  lines: ProformaLineDto[];
  subTotal: number;
  discountTotal: number;
  vatTotal: number;
  grandTotal: number;
  note: string | null;
  /** "Bu belge fatura degildir" ibaresi — arka uctan gelir. */
  disclaimer: string;
  pdfFileId: number | null;
}

/**
 * Hasta karti "Rapor" sekmesi uclari. `format` verilmezse JSON onizleme,
 * `format=pdf` ile klinik antetli PDF doner (belge MediaFile arsivine de yazilir).
 */
@Injectable({ providedIn: 'root' })
export class PatientReportsApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/v1`;

  treatmentReport(
    patientId: number,
    from?: string | null,
    to?: string | null,
  ): Observable<PatientTreatmentReportDto> {
    return this.http.get<PatientTreatmentReportDto>(
      `${this.baseUrl}/patients/${patientId}/reports/treatment`,
      { params: this.rangeParams(from, to) },
    );
  }

  treatmentReportPdf(patientId: number, from?: string | null, to?: string | null): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/patients/${patientId}/reports/treatment`, {
      params: this.rangeParams(from, to).set('format', 'pdf'),
      responseType: 'blob',
    });
  }

  statusReport(patientId: number): Observable<PatientStatusReportDto> {
    return this.http.get<PatientStatusReportDto>(
      `${this.baseUrl}/patients/${patientId}/reports/status`,
    );
  }

  statusReportPdf(patientId: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/patients/${patientId}/reports/status`, {
      params: new HttpParams().set('format', 'pdf'),
      responseType: 'blob',
    });
  }

  proforma(patientId: number, request: ProformaRequest): Observable<ProformaDto> {
    return this.http.post<ProformaDto>(
      `${this.baseUrl}/patients/${patientId}/reports/proforma`,
      request,
    );
  }

  proformaPdf(patientId: number, request: ProformaRequest): Observable<Blob> {
    return this.http.post(`${this.baseUrl}/patients/${patientId}/reports/proforma`, request, {
      params: new HttpParams().set('format', 'pdf'),
      responseType: 'blob',
    });
  }

  private rangeParams(from?: string | null, to?: string | null): HttpParams {
    let params = new HttpParams();
    if (from) {
      params = params.set('from', from);
    }
    if (to) {
      params = params.set('to', to);
    }
    return params;
  }
}
