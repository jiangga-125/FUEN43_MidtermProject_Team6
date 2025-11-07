import http from './http'

// -------------------------
// 退貨申請
// -------------------------
export type ReturnRequest = {
  OrderID: number
  ReturnReason: string
  ReturnType: number   // 0: 部分退貨, 1: 全退貨
}

// 退貨資訊
export type ReturnInfo = {
  ReturnID: number
  OrderID: number
  ReturnReason: string
  ReturnType: number
  Status: number        // 0: 申請中, 1: 進行中, 2: 完成
  ReturnedDate?: string
}

// -------------------------
// 申請退貨
// -------------------------
export async function createReturn(returnRequest: ReturnRequest): Promise<number> {
  const { data } = await http.post<{ returnID: number; message: string }>('/return/create', returnRequest)
  return data.returnID
}

// -------------------------
// 取得某訂單的退貨紀錄
// -------------------------
export async function getReturnsByOrder(orderId: number): Promise<ReturnInfo[]> {
  const { data } = await http.get<ReturnInfo[]>(`/return/order/${orderId}`)
  return data
}

// -------------------------
// 取消退貨
// -------------------------
export async function cancelReturn(returnId: number): Promise<void> {
  await http.post(`/return/cancel/${returnId}`)
}
