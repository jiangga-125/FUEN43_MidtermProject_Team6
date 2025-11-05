import http from './http'

export type ApiCartItem = {
  itemId: number    // <-- 新增：後端回傳的購物車項目 ID
  bookId: number
  title: string
  price: number
  quantity: number
  // 如果後端回傳 coverUrl 就可加上
  coverUrl?: string
}

// -------------------------
// 取得購物車內容 - 需要 memberId 傳入
// -------------------------
export async function getCart(memberId: number) {           // **修改**: 接收 memberId
  const { data } = await http.get<ApiCartItem[]>(`/shoppingCart/${memberId}`)
  return data
}

// -------------------------
// 新增商品到購物車
// -------------------------
export async function addToCart(item: { MemberID: number; BookID: number; Quantity: number; UnitPrice: number }) {
  const { data } = await http.post('/shoppingCart/add', item, { withCredentials: true })
  return data
}



// -------------------------
// 從購物車移除商品 - 使用 itemId（購物車項目 ID）
// -------------------------
export async function removeCartItem(itemId: number) {
  const { data } = await http.delete(`/shoppingCart/remove/${itemId}`)
  return data
}


