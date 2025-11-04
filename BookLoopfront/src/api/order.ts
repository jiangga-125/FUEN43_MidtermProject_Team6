import http from './http'

// -------------------------
// 單筆訂單明細
// -------------------------
export type OrderDetail = {
  BookID: number
  Quantity: number
  UnitPrice: number
}

// -------------------------
// 訂單
// -------------------------
export type Order = {
  OrderID?: number
  MemberID: number
  CustomerID?: number | null
  OrderDate?: string
  TotalAmount: number
  Status?: number
  DiscountAmount?: number
  DiscountCode?: string
  Notes?: string
  OrderDetails: OrderDetail[]
}

// -------------------------
// 建立訂單
// -------------------------
export async function createOrder(order: Order) {
  const { data } = await http.post<{ OrderID: number }>('/order/create', order)
  return data
}

// -------------------------
// 取得會員所有訂單
// -------------------------
export async function getOrdersByMember(memberId: number) {
  const { data } = await http.get<Order[]>(`/order/member/${memberId}`)
  return data
}

// -------------------------
// 取得單筆訂單明細
// -------------------------
export async function getOrderDetail(orderId: number) {
  const { data } = await http.get<Order>(`/order/${orderId}`)
  return data
}

// -------------------------
// 取消訂單（軟刪除）
// -------------------------
export async function cancelOrder(orderId: number) {
  const { data } = await http.post<{ message: string }>(`/order/cancel/${orderId}`)
  return data
}