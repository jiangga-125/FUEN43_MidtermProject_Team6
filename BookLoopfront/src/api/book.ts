// book.ts
import http from './http'

export type Book = {
  //   bookId: number
  id: number
  title: string
  coverUrl?: string
  listPrice?: number
  salePrice?: number
  author?: string // 如果你前端需要
}

// 列表
export async function getBooks(params: {
  tab?: 'new' | 'hot'
  categoryId?: number | null
  page?: number
  pageSize?: number
}) {
  const { data } = await http.get<{ total: number; page: number; pageSize: number; items: Book[] }>(
    '/books',
    {
      params,
    },
  )
  return data
}

// 單本
export async function getBookById(id: number) {
  const { data } = await http.get<Book>(`/books/${id}`)
  return data
}

// 搜尋
export async function searchBooks(q: string, page = 1, pageSize = 12) {
  const { data } = await http.get<{ items: Book[]; total: number }>('/books/search', {
    params: { q, page, pageSize },
  })
  return data
}
