import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from './api.models';
import {
  IcdCodeDto,
  PriceListDto,
  PriceListItemDto,
  PriceListItemsSaveRequest,
  PriceListUpsertRequest,
  TreatmentCatalogQuery,
  TreatmentCategoryDto,
  TreatmentCategoryUpsertRequest,
  TreatmentDefinitionDto,
  TreatmentDefinitionUpsertRequest,
} from './treatment-api.models';

/** Tedavi katalogu + fiyat listeleri + ICD-10 uclari. */
@Injectable({ providedIn: 'root' })
export class CatalogApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/v1`;

  // --- Kategoriler ----------------------------------------------------------

  categories(): Observable<TreatmentCategoryDto[]> {
    return this.http.get<TreatmentCategoryDto[]>(`${this.baseUrl}/treatment-categories`);
  }

  createCategory(request: TreatmentCategoryUpsertRequest): Observable<TreatmentCategoryDto> {
    return this.http.post<TreatmentCategoryDto>(`${this.baseUrl}/treatment-categories`, request);
  }

  updateCategory(
    id: number,
    request: TreatmentCategoryUpsertRequest,
  ): Observable<TreatmentCategoryDto> {
    return this.http.put<TreatmentCategoryDto>(
      `${this.baseUrl}/treatment-categories/${id}`,
      request,
    );
  }

  deleteCategory(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/treatment-categories/${id}`);
  }

  // --- Tedavi tanimlari -----------------------------------------------------

  definitions(query: TreatmentCatalogQuery): Observable<PagedResult<TreatmentDefinitionDto>> {
    let params = new HttpParams()
      .set('page', query.page ?? 1)
      .set('pageSize', query.pageSize ?? 50);
    if (query.search) {
      params = params.set('search', query.search);
    }
    if (query.categoryId != null) {
      params = params.set('categoryId', query.categoryId);
    }
    if (query.isActive != null) {
      params = params.set('isActive', query.isActive);
    }
    return this.http.get<PagedResult<TreatmentDefinitionDto>>(`${this.baseUrl}/treatment-catalog`, {
      params,
    });
  }

  createDefinition(request: TreatmentDefinitionUpsertRequest): Observable<TreatmentDefinitionDto> {
    return this.http.post<TreatmentDefinitionDto>(`${this.baseUrl}/treatment-catalog`, request);
  }

  updateDefinition(
    id: number,
    request: TreatmentDefinitionUpsertRequest,
  ): Observable<TreatmentDefinitionDto> {
    return this.http.put<TreatmentDefinitionDto>(`${this.baseUrl}/treatment-catalog/${id}`, request);
  }

  deleteDefinition(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/treatment-catalog/${id}`);
  }

  // --- Fiyat listeleri ------------------------------------------------------

  priceLists(): Observable<PriceListDto[]> {
    return this.http.get<PriceListDto[]>(`${this.baseUrl}/price-lists`);
  }

  createPriceList(request: PriceListUpsertRequest): Observable<PriceListDto> {
    return this.http.post<PriceListDto>(`${this.baseUrl}/price-lists`, request);
  }

  updatePriceList(id: number, request: PriceListUpsertRequest): Observable<PriceListDto> {
    return this.http.put<PriceListDto>(`${this.baseUrl}/price-lists/${id}`, request);
  }

  deletePriceList(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/price-lists/${id}`);
  }

  priceListItems(id: number): Observable<PriceListItemDto[]> {
    return this.http.get<PriceListItemDto[]>(`${this.baseUrl}/price-lists/${id}/items`);
  }

  savePriceListItems(
    id: number,
    request: PriceListItemsSaveRequest,
  ): Observable<PriceListItemDto[]> {
    return this.http.put<PriceListItemDto[]>(`${this.baseUrl}/price-lists/${id}/items`, request);
  }

  // --- ICD-10 ---------------------------------------------------------------

  icdCodes(search: string): Observable<IcdCodeDto[]> {
    const params = new HttpParams().set('search', search);
    return this.http.get<IcdCodeDto[]>(`${this.baseUrl}/icd-codes`, { params });
  }
}
