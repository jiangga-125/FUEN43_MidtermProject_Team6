import http from './http'

// -------------------------
// 物流資訊請求型別（更新或建立用）
// -------------------------
export type ShipmentRequest = {
  orderID?: number      // 建立時必填
  provider?: string
  trackingNumber?: string
  status?: number        // 0: 未出貨, 1: 運送中, 2: 已送達
  shippedDate?: string   // ISO 字串
  deliveredDate?: string // ISO 字串
}

// -------------------------
// 物流資訊（回傳型別）
// -------------------------
export type ShipmentInfo = {
  shipmentID: number
  orderID: number
  provider: string
  trackingNumber: string
  status: number
  shippedDate?: string
  deliveredDate?: string
  createdAt: string
  updatedAt: string
}

// -------------------------
// 查詢某訂單的物流資訊
// -------------------------
export async function getShipmentByOrder(orderId: number): Promise<ShipmentInfo> {
  const { data } = await http.get<{ success: boolean; shipment: ShipmentInfo }>(`/shipment/${orderId}`)
  return data.shipment
}

// -------------------------
// 建立物流資訊
// -------------------------
export async function createShipment(shipment: ShipmentRequest): Promise<ShipmentInfo> {
  const { data } = await http.post<{ success: boolean; shipment: ShipmentInfo }>(
    `/shipment/create`,
    {
      OrderID: shipment.orderID,     // 👈 改這裡
      Provider: shipment.provider    // 👈 改這裡
    }
  )
  return data.shipment
}

// -------------------------
// 更新物流資訊
// -------------------------
export async function updateShipment(shipmentId: number, shipment: ShipmentRequest): Promise<ShipmentInfo> {
  const { data } = await http.post<{ success: boolean; shipment: ShipmentInfo }>(`/shipment/update/${shipmentId}`, shipment)
  return data.shipment
}

// -------------------------
// 物流狀態文字（方便模板顯示）
// -------------------------
export function getShipmentStatusText(status: number) {
  switch (status) {
    case 0: return '未出貨'
    case 1: return '運送中'
    case 2: return '已送達'
    default: return '未知'
  }
}