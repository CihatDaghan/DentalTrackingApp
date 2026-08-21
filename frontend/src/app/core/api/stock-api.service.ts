import { inject, Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  StockCategoryDto,
  StockCategoryUpsertRequest,
  StockItemDto,
  StockItemListQuery,
  StockItemUpsertRequest,
  StockMovementCreateRequest,
  StockMovementDto,
} from './stock-api.models';

/** Stok uclari: kategori CRUD, malzeme CRUD, hareket ekleme/gecmis, kritik seviye listesi. */
@Injectable({ providedIn: 'root' })
export class StockApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiBaseUrl}/v1`;

  // --- Kategoriler ----------------------------------------------------------

  categories(): Observable<StockCategoryDto[]> {
    return this.http.get<StockCategoryDto[]>(`${this.baseUrl}/stock-categories`);
  }

  createCategory(request: StockCategoryUpsertRequest): Observable<StockCategoryDto> {
    return this.http.post<StockCategoryDto>(`${this.baseUrl}/stock-categories`, request);
  }

  updateCategory(id: number, request: StockCategoryUpsertRequest): Observable<StockCategoryDto> {
    return this.http.put<StockCategoryDto>(`${this.baseUrl}/stock-categories/${id}`, request);
  }

  deleteCategory(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/stock-categories/${id}`);
  }

  // --- Malzemeler -----------------------------------------------------------

  items(query: StockItemListQuery = {}): Observable<StockItemDto[]> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(query)) {
      if (value !== null && value !== undefined && value !== '') {
        params = params.set(key, String(value));
      }
    }
    return this.http.get<StockItemDto[]>(`${this.baseUrl}/stock-items`, { params });
  }

  lowItems(): Observable<StockItemDto[]> {
    return this.http.get<StockItemDto[]>(`${this.baseUrl}/stock-items/low`);
  }

  item(id: number): Observable<StockItemDto> {
    return this.http.get<StockItemDto>(`${this.baseUrl}/stock-items/${id}`);
  }

  createItem(request: StockItemUpsertRequest): Observable<StockItemDto> {
    return this.http.post<StockItemDto>(`${this.baseUrl}/stock-items`, request);
  }

  updateItem(id: number, request: StockItemUpsertRequest): Observable<StockItemDto> {
    return this.http.put<StockItemDto>(`${this.baseUrl}/stock-items/${id}`, request);
  }

  deleteItem(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/stock-items/${id}`);
  }

  // --- Hareketler -----------------------------------------------------------

  movements(itemId: number): Observable<StockMovementDto[]> {
    return this.http.get<StockMovementDto[]>(`${this.baseUrl}/stock-items/${itemId}/movements`);
  }

  /** Hareket sonrasi guncel malzeme (yeni miktar/isLow) doner. */
  addMovement(itemId: number, request: StockMovementCreateRequest): Observable<StockItemDto> {
    return this.http.post<StockItemDto>(`${this.baseUrl}/stock-items/${itemId}/movements`, request);
  }
}
