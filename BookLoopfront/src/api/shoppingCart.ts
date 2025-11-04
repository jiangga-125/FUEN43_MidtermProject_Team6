import http from './http'

export type CartItem = {
  MemberID: number
  BookID: number
  Quantity: number
  UnitPrice: number
}

// -------------------------
// 取得購物車內容
// -------------------------
export async function getCart() {
  const { data } = await http.get<CartItem[]>('/shoppingcart')
  return data
}

// -------------------------
// 新增商品到購物車
// -------------------------
export async function addToCart(item: CartItem) {
  const { data } = await http.post('/shoppingcart/add', item, { withCredentials: true })
  return data
}

// -------------------------
// 更新購物車商品數量
// -------------------------
export async function updateCartItem(item: CartItem) {
  const { data } = await http.put('/shoppingcart/update', item)
  return data
}

// -------------------------
// 從購物車移除商品
// -------------------------
export async function removeCartItem(bookId: number) {
  const { data } = await http.delete(`/shoppingcart/remove/${bookId}`)
  return data
}

// -------------------------
// 清空購物車
// -------------------------
export async function clearCart() {
  const { data } = await http.post('/shoppingcart/clear')
  return data
}
