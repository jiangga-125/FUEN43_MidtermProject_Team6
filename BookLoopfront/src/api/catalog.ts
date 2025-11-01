// 商品/分類/搜尋 API（依你後端再調對路徑即可）
import http from './http'

export type Category = { categoryId: number; name: string }
export type Book = {
  bookId: number
  title: string
  author?: string
  listPrice?: number
  salePrice?: number
  coverUrl?: string
}

export async function getCategories() {
  // 後端：GET /api/categories  回傳 Category[]
  const { data } = await http.get<Category[]>('/categories')
  return data
}

export async function getBooks(params: {
  tab: 'new' | 'hot',
  categoryId?: number | null,
  page?: number,
  pageSize?: number,
}) {
  // 後端：GET /api/books?tab=new&categoryId=..&page=..&pageSize=..
  const { tab, categoryId, page = 1, pageSize = 8 } = params
  const { data } = await http.get<{ items: Book[]; total: number }>('/books', {
    params: { tab, categoryId, page, pageSize }
  })
  return data
}

export async function searchBooks(q: string, page = 1, pageSize = 12) {
  // 後端：GET /api/books/search?q=..&page=..&pageSize=..
  const { data } = await http.get<{ items: Book[]; total: number }>('/books/search', {
    params: { q, page, pageSize }
  })
  return data
}

export async function getBanners() {
  // 後端：GET /api/banners 回傳 { id, imageUrl, link? }[]
  const { data } = await http.get<Array<{ id:number; imageUrl:string; link?:string }>>('/banners')
  return data
}
