import http from './http'

// -------------------------
// 退貨申請請求型別
// -------------------------
export type ReturnRequest = {
  orderID: number      // 對應後端 camelCase
  returnReason: string
  returnType: number   // 0: 部分退貨, 1: 全退貨
}

// -------------------------
// 退貨資訊（回傳型別）
// -------------------------
export type ReturnInfo = {
  returnID: number
  orderID: number
  returnReason: string
  returnType: number
  status: number       // 0: 申請中, 1: 進行中, 2: 完成, 9: 已取消
  returnedDate?: string
}

// -------------------------
// 申請退貨
// -------------------------
export async function createReturn(returnRequest: ReturnRequest): Promise<ReturnInfo> {
  const { data } = await http.post<ReturnInfo>('/return/create', returnRequest)
  return data
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

// -------------------------
// 狀態轉文字（方便模板顯示）
// -------------------------
export function getStatusText(status: number) {
  switch (status) {
    case 0: return '申請中'
    case 1: return '進行中'
    case 2: return '完成'
    case 9: return '已取消'
    default: return '未知'
  }
}
