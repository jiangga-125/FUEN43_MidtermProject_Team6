import http from './http'

export interface Listing {
  listingId: number
  title: string
  authorName?: string
  imageUrl?: string
  status: number               // 0=可借,1=保留中,2=已借出
  categoryName?: string
  publisherName?: string
  condition?: string
  isbn?: string
}

export function getFrontListings() {
  return http
    .get<Listing[]>('/listings/front')
    .then(r => r.data)
}
