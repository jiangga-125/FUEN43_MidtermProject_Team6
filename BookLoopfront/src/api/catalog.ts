import http from './http'
import type { Book } from './book'

export type Category = { categoryId: number; name: string }

export async function getCategories() {
  const { data } = await http.get<Category[]>('/categories')
  return data
}

export async function getBanners() {
  try {
    const { data } = await http.get('AdvertisementsApi')
    console.log('✅ 成功載入廣告資料', data)

    return data
  } catch (err) {
    console.error('❌ 無法讀取廣告資料', err)
    return []
  }
}
