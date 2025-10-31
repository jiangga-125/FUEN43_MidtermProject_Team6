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
  condition?: string | null
  status?: number | null
  createdAt?: string | null
  category?: CategoryInfo | null
}

export interface ListingsResponse {
  items: Listing[]
  total: number
  page: number
  pageSize: number
}

const BasePath = '/listings'
// Controller 使用 [Route("api/[controller]")], http.baseURL 應為 '/api' -> 所以BasePath為/listings

const ListingsApi = {
  /**
   * list: 取得分頁清單
   * params: { q?, page?, pageSize? }
   * 回傳 mapped 結構，方便前端直接使用 listing.listingId / listing.coverUrl 等欄位
   */
  async list(params: { q?: string; page?: number; pageSize?: number } = {}) {
    const resp = await http.get(BasePath, { params })
    const payload = resp.data || {}

    const mappedItems: Listing[] = (payload.items || []).map((i: any) => ({
      listingId: i.id,
      title: i.title,
      isbn: i.isbn ?? null,
      coverUrl: i.image ?? null,
      condition: i.condition ?? null,
      status: i.status ?? null,
      createdAt: i.createdAt ?? null,
      category: i.category ? { id: i.category.id, name: i.category.name } : null,
    }))

    return {
      items: mappedItems,
      total: payload.total ?? 0,
      page: payload.page ?? params.page ?? 1,
      pageSize: payload.pageSize ?? params.pageSize ?? 20,
    } as ListingsResponse
  },

  /**
   * get: 取得單一 listing 詳細
   * 回傳 controller 的原始資料（但也做小 mapping）
   */
  async get(id: number) {
    const resp = await http.get(`${BasePath}/${id}`)
    const p = resp.data
    if (!p) return null
    return {
      listingId: p.id,
      title: p.title,
      isbn: p.isbn ?? null,
      condition: p.condition ?? null,
      status: p.status ?? null,
      createdAt: p.createdAt ?? null,
      images: p.images ?? [], // controller 回傳 images: [{ ImageID, url, Caption }, ...]
      category: p.category ? { id: p.category.id, name: p.category.name } : null,
    }
  },
}

export default ListingsApi
