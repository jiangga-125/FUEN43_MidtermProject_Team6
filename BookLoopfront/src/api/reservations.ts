// src/api/reservations.ts
import http from './http'

// ----- 型別 -----
export interface MemberOption {
  id: number
  name: string
}

export interface PrepareReservationResponse {
  listingId: number
  bookTitle: string
  defaultPickupDate: string   // ISO 日期，如 "2025-11-01T00:00:00"
  defaultPickupTime: string   // "HH:mm:ss"
  members: MemberOption[]
}

// ★ 新增：具名匯出的請求介面
export interface CreateReservationRequest {
  listingId: number
  memberId: number
  requestedPickupDate: string // "YYYY-MM-DD" 或 ISO
  requestedPickupTime: string // "HH:mm:ss"
}

export interface CreateReservationResponse {
  ok: boolean
  message?: string
  listingId: number
  newStatus: number
  readyAt: string             // ISO
  expiresAt: string           // ISO
}

// ----- API -----
export function prepareReservation(listingId: number) {
  return http
    .get<PrepareReservationResponse>(`/reservations/prepare/${listingId}`)
    .then(r => r.data)
}

// ★ 修正：使用 CreateReservationRequest 作為參數型別，並修正大括號位置
export function createReservation(payload: CreateReservationRequest) {
  return http
    .post<CreateReservationResponse>('/reservations', payload)
    .then(r => r.data)
}
