import http from './http'
import { Book, getBookById } from './book'

// -------------------------
// API 原始回傳型別（小寫欄位）
// -------------------------
type RawOrderDetail = {
  bookID: number
  quantity: number
  unitPrice: number
  book?: Book
}

type RawOrder = {
  orderID?: number
  memberID: number
  customerID?: number | null
  orderDate?: string
  totalAmount: number
  status?: number
  discountAmount?: number
  discountCode?: string
  notes?: string
  orderDetails: RawOrderDetail[]
}

// -------------------------
// 單筆訂單明細（前端用）
// -------------------------
export type OrderDetail = {
  BookID: number
  Quantity: number
  UnitPrice: number
  Book?: Book
}

// -------------------------
// 訂單（前端用）
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
  order.OrderDetails.forEach((od, idx) => {
    console.log(`Detail[${idx}]`, od)
  })

  const { data } = await http.post('/order/create', order)
  console.log('createOrder 回傳:', data)

  // ✅ 後端回傳是 orderId 小寫
  const orderId = data.orderId
  if (!orderId) {
    throw new Error('後端沒有回傳 OrderID')
  }

  return orderId
}
// -------------------------
// 取得會員所有訂單
// -------------------------
export async function getOrdersByMember(memberId: number) {
  const { data } = await http.get<RawOrder[]>(`/order/member/${memberId}`)

  const orders: Order[] = await Promise.all(
    data.map(async o => {
      const details: OrderDetail[] = await Promise.all(
        o.orderDetails.map(async od => {
          let book = od.book
          if (!book) {
            try {
              book = await getBookById(od.bookID)
            } catch {
              book = undefined
            }
          }
          return {
            BookID: od.bookID,
            Quantity: od.quantity,
            UnitPrice: od.unitPrice,
            Book: book
          }
        })
      )

      return {
        OrderID: o.orderID,
        MemberID: o.memberID,
        CustomerID: o.customerID ?? null,
        OrderDate: o.orderDate,
        TotalAmount: o.totalAmount,
        Status: o.status,
        DiscountAmount: o.discountAmount,
        DiscountCode: o.discountCode,
        Notes: o.notes,
        OrderDetails: details
      }
    })
  )

  return orders
}

// -------------------------
// 取得單筆訂單明細
// -------------------------
export async function getOrderDetail(orderId: number) {
  const { data } = await http.get<RawOrder>(`/order/${orderId}`)

  const details: OrderDetail[] = await Promise.all(
    data.orderDetails.map(async od => {
      let book = od.book
      if (!book) {
        try {
          book = await getBookById(od.bookID)
        } catch {
          book = undefined
        }
      }
      return {
        BookID: od.bookID,
        Quantity: od.quantity,
        UnitPrice: od.unitPrice,
        Book: book
      }
    })
  )

  const order: Order = {
    OrderID: data.orderID,
    MemberID: data.memberID,
    CustomerID: data.customerID ?? null,
    OrderDate: data.orderDate,
    TotalAmount: data.totalAmount,
    Status: data.status,
    DiscountAmount: data.discountAmount,
    DiscountCode: data.discountCode,
    Notes: data.notes,
    OrderDetails: details
  }

  return order
}

// -------------------------
// 取消訂單（Status = 0）
// -------------------------
export async function cancelOrder(orderId: number) {
  const { data } = await http.post<{ message: string }>(`/order/cancel/${orderId}`)
  return data
}

// -------------------------
// 軟刪除訂單（Status = 9）
// -------------------------
export async function deleteOrder(orderId: number) {
  const { data } = await http.post<{ message: string }>(`/order/delete/${orderId}`)
  return data
}

// -------------------------
// 付款流程（綠界）
// -------------------------
export type ECPayRequest = {
  MerchantID: string
  MerchantTradeNo: string
  MerchantTradeDate: string
  PaymentType: string
  TotalAmount: string
  TradeDesc: string
  ItemName: string
  ReturnURL: string
  OrderResultURL: string
  ChoosePayment: string
  EncryptType: string
  CheckMacValue: string
}

export async function goToPayment(orderId: number) {
  const { data } = await http.post<ECPayRequest>(`/orders/GoToPayment`, { orderId })
  return data
}
