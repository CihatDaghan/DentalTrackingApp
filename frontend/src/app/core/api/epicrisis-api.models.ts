/**
 * Elle tiplenmis epikriz API sozlesmesi (+ ICD kodu arama).
 * NOT: Sonraki asamada NSwag ile `api-client.generated.ts` uretilecek ve bu dosya kaldirilacak.
 */

export interface IcdCodeDto {
  id: number;
  code: string;
  name: string;
  nameEn: string | null;
}

/** Epikriz tanisi — arka uca kod + ad ciftiyle gomulu yazilir (referans degil). */
export interface EpicrisisDiagnosis {
  code: string;
  name: string;
}

export interface EpicrisisTreatmentLine {
  id: number;
  date: string | null;
  toothNumber: string | null;
  name: string;
  doctorName: string | null;
}

export interface EpicrisisDto {
  id: number;
  patientId: number;
  patientName: string;
  doctorUserId: number;
  doctorName: string;
  title: string;
  diagnoses: EpicrisisDiagnosis[];
  treatments: EpicrisisTreatmentLine[];
  bodyText: string | null;
  pdfFileId: number | null;
  createdAtUtc: string;
}

export interface EpicrisisCreateRequest {
  doctorUserId: number;
  title: string;
  diagnoses: EpicrisisDiagnosis[];
  treatmentRecordIds: number[];
  bodyText?: string | null;
}
