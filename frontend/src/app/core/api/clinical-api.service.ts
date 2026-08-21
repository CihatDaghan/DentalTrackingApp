import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AnamnesisFillRequest,
  AnamnesisResponseDto,
  AnamnesisTemplateDto,
  AnamnesisTemplateListItemDto,
  CriticalFlagDto,
  MediaCategory,
  MediaFileDto,
  MediaUploadMeta,
  PatientNoteDto,
  PatientNoteUpsertRequest,
} from './clinical-api.models';

/** Klinik uclar: anamnez, kritik uyari bayraklari, hasta notlari, goruntu arsivi. */
@Injectable({ providedIn: 'root' })
export class ClinicalApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/v1`;

  // --- Anamnez --------------------------------------------------------------

  anamnesisTemplates(): Observable<AnamnesisTemplateListItemDto[]> {
    return this.http.get<AnamnesisTemplateListItemDto[]>(`${this.baseUrl}/anamnesis-templates`);
  }

  anamnesisTemplate(id: number): Observable<AnamnesisTemplateDto> {
    return this.http.get<AnamnesisTemplateDto>(`${this.baseUrl}/anamnesis-templates/${id}`);
  }

  patientAnamnesis(patientId: number): Observable<AnamnesisResponseDto[]> {
    return this.http.get<AnamnesisResponseDto[]>(`${this.baseUrl}/patients/${patientId}/anamnesis`);
  }

  fillAnamnesis(patientId: number, request: AnamnesisFillRequest): Observable<AnamnesisResponseDto> {
    return this.http.post<AnamnesisResponseDto>(
      `${this.baseUrl}/patients/${patientId}/anamnesis`,
      request,
    );
  }

  criticalFlags(patientId: number): Observable<CriticalFlagDto[]> {
    return this.http.get<CriticalFlagDto[]>(`${this.baseUrl}/patients/${patientId}/critical-flags`);
  }

  // --- Notlar ---------------------------------------------------------------

  notes(patientId: number): Observable<PatientNoteDto[]> {
    return this.http.get<PatientNoteDto[]>(`${this.baseUrl}/patients/${patientId}/notes`);
  }

  createNote(patientId: number, request: PatientNoteUpsertRequest): Observable<PatientNoteDto> {
    return this.http.post<PatientNoteDto>(`${this.baseUrl}/patients/${patientId}/notes`, request);
  }

  updateNote(
    patientId: number,
    noteId: number,
    request: PatientNoteUpsertRequest,
  ): Observable<PatientNoteDto> {
    return this.http.put<PatientNoteDto>(
      `${this.baseUrl}/patients/${patientId}/notes/${noteId}`,
      request,
    );
  }

  deleteNote(patientId: number, noteId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/patients/${patientId}/notes/${noteId}`);
  }

  // --- Medya ----------------------------------------------------------------

  media(patientId: number, category?: MediaCategory): Observable<MediaFileDto[]> {
    let params = new HttpParams();
    if (category != null) {
      params = params.set('category', category);
    }
    return this.http.get<MediaFileDto[]>(`${this.baseUrl}/patients/${patientId}/media`, { params });
  }

  uploadMedia(patientId: number, file: File, meta: MediaUploadMeta): Observable<MediaFileDto> {
    const form = new FormData();
    form.append('File', file, file.name);
    form.append('Category', String(meta.category));
    if (meta.description) {
      form.append('Description', meta.description);
    }
    if (meta.toothNumber) {
      form.append('ToothNumber', meta.toothNumber);
    }
    if (meta.takenAt) {
      form.append('TakenAt', meta.takenAt);
    }
    return this.http.post<MediaFileDto>(`${this.baseUrl}/patients/${patientId}/media`, form);
  }

  deleteMedia(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/media/${id}`);
  }

  thumbnailUrl(id: number): string {
    return `${this.baseUrl}/media/${id}/thumbnail`;
  }

  downloadUrl(id: number): string {
    return `${this.baseUrl}/media/${id}/download`;
  }

  /** Buyuk gorsel/indirilecek dosya — Authorization interceptor'dan gectigi icin blob ile cekilir. */
  downloadBlob(id: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/media/${id}/download`, { responseType: 'blob' });
  }

  thumbnailBlob(id: number): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/media/${id}/thumbnail`, { responseType: 'blob' });
  }
}
