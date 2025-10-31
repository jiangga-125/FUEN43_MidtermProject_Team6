import http from './http'

export interface CategoryInfo {
  id: number
  name: string
}

export interface Listing {
  listingId: number
  title: string
  isbn?: string | null
  coverUrl?: string | null
  price?: number | null
  condition?: string | null
  status?: number | null
  createdAt?: string | null
  category?: CategoryInfo | null
  // 若後端有其他欄位會保留在 raw
  raw?: any
}

export interface ListingsResponse {
  items: Listing[]
  total: number
  page: number
  pageSize: number
}

const BasePath = '/listings'
// Controller 用 api/[controller] -> 實際請求會是 /api/listings（取決於 http.baseURL）
const mapItem = (i: any): Listing => {
  if (!i) return { listingId: 0, title: '', raw: i }
  return {
    listingId: i.id ?? i.listingId ?? 0,
    title: i.title ?? i.name ?? '',
    isbn: i.isbn ?? null,
    coverUrl: i.image ?? i.coverUrl ?? null,
    price: i.price ?? null,
    condition: i.condition ?? null,
    status: i.status ?? null,
    createdAt: i.createdAt ?? i.created_at ?? null,
    category: i.category ? { id: i.category.id, name: i.category.name } : (i.categoryName ? { id: 0, name: i.categoryName } : null),
    raw: i
  }
}

const ListingsApi = {
  async list(params: { q?: string; categoryId?: number | string; page?: number; pageSize?: number } = {}) {
    const resp = await http.get(BasePath, { params })
    const payload = resp.data ?? {}

    // 支援後端直接回傳 array（legacy）或回傳 { items, total, page, pageSize }
    const itemsRaw = Array.isArray(payload) ? payload : (payload.items ?? payload.data ?? [])
    const mapped = (itemsRaw || []).map(mapItem)
    const total = payload.total ?? payload.count ?? mapped.length
    const page = payload.page ?? params.page ?? 1
    const pageSize = payload.pageSize ?? params.pageSize ?? 20

    return { items: mapped, total, page, pageSize } as ListingsResponse
  },

  async get(id: number) {
    const resp = await http.get(`${BasePath}/${id}`)
    const p = resp.data ?? null
    if (!p) return null
    return mapItem(p)
  }
}

export default ListingsApi
