import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from './api.models';
import {
  AutomationRuleDto,
  AutomationRuleUpsertRequest,
  BulkMessageRequest,
  BulkMessageResult,
  MessageListQuery,
  MessageSendRequest,
  MessageTemplateDto,
  MessageTemplateUpsertRequest,
  OutboundMessageDto,
  WhatsAppTemplateDto,
  WhatsAppTemplateUpsertRequest,
} from './messaging-api.models';

/** Mesajlasma uclari: outbox, sablonlar, WhatsApp sablonlari, otomasyon kurallari. */
@Injectable({ providedIn: 'root' })
export class MessagingApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/v1`;

  // --- Giden mesajlar -------------------------------------------------------

  messages(query: MessageListQuery): Observable<PagedResult<OutboundMessageDto>> {
    let params = new HttpParams()
      .set('page', query.page ?? 1)
      .set('pageSize', query.pageSize ?? 25);
    if (query.channel != null) {
      params = params.set('channel', query.channel);
    }
    if (query.state != null) {
      params = params.set('state', query.state);
    }
    if (query.patientId != null) {
      params = params.set('patientId', query.patientId);
    }
    if (query.from) {
      params = params.set('from', query.from);
    }
    if (query.to) {
      params = params.set('to', query.to);
    }
    return this.http.get<PagedResult<OutboundMessageDto>>(`${this.baseUrl}/messages`, { params });
  }

  message(id: number): Observable<OutboundMessageDto> {
    return this.http.get<OutboundMessageDto>(`${this.baseUrl}/messages/${id}`);
  }

  send(request: MessageSendRequest): Observable<OutboundMessageDto> {
    return this.http.post<OutboundMessageDto>(`${this.baseUrl}/messages`, request);
  }

  bulk(request: BulkMessageRequest): Observable<BulkMessageResult> {
    return this.http.post<BulkMessageResult>(`${this.baseUrl}/messages/bulk`, request);
  }

  /** Kuyrugu elle isletir (arka planda zamanlanmis is de ayni ucu cagirir). */
  dispatch(): Observable<unknown> {
    return this.http.post(`${this.baseUrl}/messages/dispatch`, null);
  }

  // --- SMS/WhatsApp metin sablonlari ---------------------------------------

  templates(includeInactive = true): Observable<MessageTemplateDto[]> {
    const params = new HttpParams().set('includeInactive', includeInactive);
    return this.http.get<MessageTemplateDto[]>(`${this.baseUrl}/message-templates`, { params });
  }

  createTemplate(request: MessageTemplateUpsertRequest): Observable<MessageTemplateDto> {
    return this.http.post<MessageTemplateDto>(`${this.baseUrl}/message-templates`, request);
  }

  updateTemplate(
    id: number,
    request: MessageTemplateUpsertRequest,
  ): Observable<MessageTemplateDto> {
    return this.http.put<MessageTemplateDto>(`${this.baseUrl}/message-templates/${id}`, request);
  }

  deleteTemplate(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/message-templates/${id}`);
  }

  // --- WhatsApp (Meta onayli) sablonlari -----------------------------------

  whatsAppTemplates(): Observable<WhatsAppTemplateDto[]> {
    return this.http.get<WhatsAppTemplateDto[]>(`${this.baseUrl}/whatsapp-templates`);
  }

  createWhatsAppTemplate(
    request: WhatsAppTemplateUpsertRequest,
  ): Observable<WhatsAppTemplateDto> {
    return this.http.post<WhatsAppTemplateDto>(`${this.baseUrl}/whatsapp-templates`, request);
  }

  updateWhatsAppTemplate(
    id: number,
    request: WhatsAppTemplateUpsertRequest,
  ): Observable<WhatsAppTemplateDto> {
    return this.http.put<WhatsAppTemplateDto>(`${this.baseUrl}/whatsapp-templates/${id}`, request);
  }

  deleteWhatsAppTemplate(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/whatsapp-templates/${id}`);
  }

  // --- Otomasyon kurallari --------------------------------------------------

  automationRules(): Observable<AutomationRuleDto[]> {
    return this.http.get<AutomationRuleDto[]>(`${this.baseUrl}/automation-rules`);
  }

  createAutomationRule(request: AutomationRuleUpsertRequest): Observable<AutomationRuleDto> {
    return this.http.post<AutomationRuleDto>(`${this.baseUrl}/automation-rules`, request);
  }

  updateAutomationRule(
    id: number,
    request: AutomationRuleUpsertRequest,
  ): Observable<AutomationRuleDto> {
    return this.http.put<AutomationRuleDto>(`${this.baseUrl}/automation-rules/${id}`, request);
  }
}
