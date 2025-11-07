<script setup lang="ts">
import { ref, onMounted, watch, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getOrdersByMember, getOrderDetail, cancelOrder, deleteOrder, Order } from '@/api/order'
import { createReturn, getReturnsByOrder, ReturnInfo, getStatusText } from '@/api/return'
import { cancelReturn } from '@/api/return'
import { useAuth } from '@/stores/auth'
import { getShipmentByOrder, updateShipment, ShipmentInfo ,createShipment} from '@/api/shipment'

const router = useRouter()
const route = useRoute()
const auth = useAuth()
// const memberId = 616
const memberId = computed<number | null>(() => {
  const m = (auth as any).member
  const id = m?.memberId ?? m?.MemberID ?? m?.id ?? null
  if (id == null) return null
  return typeof id === 'string' ? Number(id) : id
})

const shipmentMap = ref<Record<number, ShipmentInfo>>({}) // key = OrderID

async function onChangeShipment(orderId: number, provider: string) {
  if (!provider) return; // 未選擇物流不處理
  try {
    // 統一呼叫 createShipment，由後端自動覆蓋或建立
    const shipment = await createShipment({ orderID: orderId, provider });

    // 後端會自動生成 TrackingNumber 並固定 Status = 0 (待出貨)
    shipmentMap.value[orderId] = shipment;

    alert(`✅ 物流公司已更新\n追蹤號碼：${shipment.trackingNumber}\n狀態：待出貨`);
  } catch (err) {
    console.error('更新物流失敗', err);
    alert('❌ 更新物流失敗，請稍後再試');
  }
}
// function goHome() {
//   router.push('/') // 導向首頁


// 載入物流（初始化時）
async function loadShipment(orderId: number) {
  try {
    const shipment = await getShipmentByOrder(orderId);
    shipmentMap.value[orderId] = shipment;
  } catch (err) {
    // 沒有物流資料不用理會
    console.warn(`尚無物流資料 for OrderID ${orderId}`, err);
  }
}


// Tab: 'orders'=我的訂單, 'details'=單筆明細, 'allDetails'=所有訂單明細, 'returns'=退貨紀錄
const currentTab = ref<'orders' | 'details' | 'allDetails' | 'returns'>('orders')
const orders = ref<Order[]>([])
const selectedOrder = ref<Order | null>(null)
// const returns = ref<any[]>([])
const returns = ref<ReturnInfo[]>([])







function goHome() {
  router.push('/') // 導向首頁
}




// 訂單狀態對應
const orderStatusMap: Record<number, string> = {
  0: '待付款',
  1: '已付款',
  2: '已出貨',
  3: '完成訂單',
  4: '已取消',
}

// function goHome() {
//   router.push('/')
// }

// 載入會員所有訂單
async function loadOrders() {
  // 若沒有登入（無 memberId），就不呼叫 API
  if (memberId.value == null) {
    orders.value = []
    console.warn('loadOrders: memberId is null — skip loading orders')
    return
  }

  try {
    // getOrdersByMember 會直接回傳 Order[]（你在 src/api/order.ts 已經做了轉換）
    const list = await getOrdersByMember(Number(memberId.value))
    orders.value = list
    console.log('orders.value', orders.value)

    await loadAllReturns()
  } catch (err) {
    console.error('載入訂單失敗', err)
    // 視需要顯示錯誤給使用者
    // alert('載入訂單失敗')
  }
}

async function onPay(orderId: number) {
  window.open(`https://localhost:7176/Orders/Orders/GoToPayment?orderId=${orderId}`, '_blank')
  // 整合 ECPay
}
// 載入會員所有退貨紀錄
async function loadAllReturns() {
  if (!orders.value.length) return // 🔹 防護：沒有訂單就不抓
  try {
    const allReturns: ReturnInfo[] = []
    for (const order of orders.value) {
      const rs = await getReturnsByOrder(order.OrderID!)
      allReturns.push(...rs)
    }
    returns.value = allReturns
  } catch (err) {
    console.error('載入退貨紀錄失敗', err)
  }
}

async function cancelReturnOrder(returnId: number) {
  if (!confirm('確定要取消這筆退貨嗎？')) return
  try {
    await cancelReturn(returnId)
    alert('✅ 退貨已取消')
    await loadAllReturns()
  } catch (err) {
    console.error('取消退貨失敗', err)
    alert('❌ 取消退貨失敗，請稍後再試')
  }
}

async function onReturn(orderId: number) {
  const reason = prompt('請輸入退貨理由：')
  if (!reason) return
  const returnRequest = { orderID: orderId, returnReason: reason, returnType: 1 }
  const ret = await createReturn(returnRequest)
  alert(`✅ 退貨申請已送出（退貨編號：${ret.returnID}）`)
  await loadOrders()
  await loadAllReturns()
}

async function viewOrderDetail(orderId: number) {
  try {
    selectedOrder.value = await getOrderDetail(orderId)
    currentTab.value = 'details'
    returns.value = await getReturnsByOrder(orderId)
  } catch (err) {
    console.error('讀取訂單明細失敗', err)
    // 可提示使用者
  }
}

async function onCancel(orderId: number) {
  if (!confirm('確定要取消訂單嗎？')) return
  try {
    await cancelOrder(orderId)
    await loadOrders()
  } catch (err) {
    console.error('取消訂單失敗', err)
    alert('取消訂單失敗')
  }
}

async function onDelete(orderId: number) {
  if (!confirm('確定要刪除訂單嗎？')) return
  try {
    await deleteOrder(orderId)
    await loadOrders()
  } catch (err) {
    console.error('刪除訂單失敗', err)
    alert('刪除訂單失敗')
  }
}

// 若登出或剛登入，watch memberId 自動 reload 或清空 orders
watch(memberId, (v) => {
  if (v == null) {
    orders.value = []
    selectedOrder.value = null
  } else {
    loadOrders().catch((e) => console.error(e))
  }
})
// 計算所有訂單明細，用於 new Tab
const allOrderDetails = computed(() =>
  orders.value.flatMap((order) =>
    order.OrderDetails.map((od) => ({
      ...od,
      parentOrderID: order.OrderID!, // 用 ! 告訴 TS 一定有值
    })),
  ),
)
const allOrderDetailsGroupedArray = computed(() => {
  const map: Record<number, { details: (typeof allOrderDetails.value)[number][]; total: number }> =
    {}
  allOrderDetails.value.forEach((item) => {
    if (!map[item.parentOrderID]) map[item.parentOrderID] = { details: [], total: 0 }
    map[item.parentOrderID].details.push(item)
    map[item.parentOrderID].total += item.Quantity * item.UnitPrice
  })
  // 🔹 將物件轉成陣列，每筆元素包含 orderId 與 group
  return Object.entries(map).map(([orderId, group]) => ({
    orderId,
    ...group,
  }))
})
onMounted(async () => {
  await loadOrders()
  //   await loadAllReturns()
  if (orders.value.length > 0) {
    selectedOrder.value = orders.value[0]
  }
  // 如果有 query 帶入 selectedOrderId
  const selectedId = route.query.selectedOrderId
  if (selectedId) {
    const orderId = parseInt(selectedId as string)
    if (!isNaN(orderId)) {
      await viewOrderDetail(orderId)
    }
  }
  console.log('orders.value', orders.value)
  console.log('allOrderDetails.value', allOrderDetails.value)
  console.log('allOrderDetailsGroupedArray.value', allOrderDetailsGroupedArray.value)
})
</script>

<template>
  <div class="container py-5">
    <!-- 標題與首頁按鈕 -->
    <div class="d-flex justify-content-between align-items-center mb-4">
      <h1>📦 訂單中心</h1>
      <button class="btn btn-outline-primary" @click="goHome">🏠 回首頁</button>
    </div>

    <!-- 若未登入顯示提示 -->
    <div v-if="memberId === null" class="mb-4">
      <div class="alert alert-info">
        您尚未登入，請先登入以查看您的訂單。
        <button
          class="btn btn-sm btn-primary ms-3"
          @click="() => router.push({ name: 'Login' } as any)"
        >
          前往登入
        </button>
      </div>
    </div>

    <!-- Tab 切換 -->
    <div class="mb-4">
      <button
        class="btn btn-outline-primary me-2"
        :class="{ active: currentTab === 'orders' }"
        @click="currentTab = 'orders'"
      >
        我的訂單
      </button>
      <button
        class="btn btn-outline-secondary me-2"
        :disabled="!selectedOrder"
        :class="{ active: currentTab === 'details' }"
        @click="currentTab = 'details'"
      >
        訂單明細
      </button>
      <button
        class="btn btn-outline-warning me-2"
        :disabled="!selectedOrder"
        :class="{ active: currentTab === 'returns' }"
        @click="currentTab = 'returns'"
      >
        退貨紀錄
      </button>
      <button
        class="btn btn-outline-info me-2"
        :disabled="orders.length === 0"
        :class="{ active: currentTab === 'allDetails' }"
        @click="currentTab = 'allDetails'"
      >
        所有訂單明細
      </button>
    </div>

    <!-- 訂單列表 -->
    <div v-if="currentTab === 'orders'">
      <div v-if="memberId === null" class="text-center text-muted py-4">請先登入以檢視訂單</div>

      <div v-else>
        <div v-if="orders.length === 0" class="text-center text-muted py-5">目前沒有訂單</div>
        <div class="row g-3">
          <div v-for="order in orders" :key="order.OrderID" class="col-md-6">
            <div class="card shadow-sm border-0 rounded-4 overflow-hidden">

                  <!-- 物流公司下拉選單 -->
        <select class="form-select form-select-sm position-absolute top-0 end-0 m-2 shadow-sm"
                style="width: 140px;"
                :value="shipmentMap[order.OrderID!]?.provider || ''"
                @change="onChangeShipment(order.OrderID!, ($event.target as HTMLSelectElement).value)">
          <option value="">請選擇物流公司</option>
          <option value="黑貓宅急便">黑貓宅急便</option>
          <option value="宅配通">宅配通</option>
          <option value="新竹物流">新竹物流</option>
        </select>



              <div class="card-body">
                <h5 class="card-title mb-2 fw-bold text-primary">
                  訂單 #{{ order.OrderID ?? '-' }}
                </h5>
                <p class="card-text mb-2 text-muted small">
                  下單時間：{{
                    order.OrderDate ? new Date(order.OrderDate).toLocaleString() : '無資料'
                  }}
                </p>
                <p class="card-text mb-1">
                  💰 總金額：<span class="fw-bold text-success"
                    >NT$ {{ order.TotalAmount ?? 0 }}</span
                  ><br />
                  📦 狀態：<span
                          class="badge"
                          :class="{
                            'bg-warning text-dark': order.Status === 0, // 待付款
                            'bg-primary': order.Status === 1,           // 已付款
                            'bg-info text-dark': order.Status === 2,    // 已出貨
                            'bg-success': order.Status === 3,           // 完成訂單
                            'bg-danger': order.Status === 4,            // 已取消
                            'bg-secondary': order.Status == null        // 其他狀態
                          }"
                        >
                          {{ orderStatusMap[order.Status ?? 0] }}
                        </span>
                </p>

                <!-- 按鈕區 -->
                <div class="d-flex flex-wrap gap-2 mt-3">
                  <!-- 查看明細 -->
                  <button
                    class="btn btn-sm btn-primary flex-grow-1"
                    @click="viewOrderDetail(order.OrderID!)"
                  >
                    查看明細
                  </button>

                  <!-- 付款 -->
                  <button
                    class="btn btn-sm btn-success flex-grow-1"
                    @click="onPay(order.OrderID!)"
                    :disabled="order.Status !== 0"
                  >
                    付款
                  </button>

                  <!-- 退貨 -->
                  <button
                    class="btn btn-sm btn-warning flex-grow-1"
                    @click="onReturn(order.OrderID!)"
                    
                  >
                    退貨
                  </button>

                  <!-- 刪除 -->
                  <button
                    class="btn btn-sm btn-outline-danger flex-grow-1"
                    @click="onDelete(order.OrderID!)"
                  >
                    🗑️ 刪除
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
</div>
    <!-- 單筆訂單明細 -->
    <div v-if="currentTab === 'details' && selectedOrder" class="mt-4">
      <h4>訂單明細：#{{ selectedOrder.OrderID }}</h4>
      <div class="card shadow-sm bg-white p-3">
        <table class="table table-hover mb-0">
          <thead class="table-light">
            <tr>
              <th>書籍</th>
              <th>數量</th>
              <th>單價</th>
              <th>小計</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="item in selectedOrder.OrderDetails" :key="item.BookID">
              <td>
                <div class="d-flex align-items-center gap-3 flex-grow-1">
                  <img
                    :src="
                      item.Book?.coverUrl && item.Book.coverUrl.startsWith('http')
                        ? item.Book.coverUrl
                        : `/api/BookImages/${item.Book?.id}/cover`
                    "
                    :alt="item.Book?.title || 'Book Cover'"
                    error="(e: Event) => {
                        const target = e.currentTarget as HTMLImageElement | null;
                        if(target) target.src='/placeholder.png'
                    }"
                    class="rounded shadow-sm"
                    style="width: 60px; height: 80px; object-fit: cover"
                  />
                  <div>
                    <strong class="fs-6">{{ item.Book?.title || '(已下架)' }}</strong>
                    <div class="text-muted small">
                      NT$ {{ item.Book?.salePrice ?? item.UnitPrice ?? 0 }}
                    </div>
                  </div>
                </div>
              </td>

              <td>{{ item.Quantity }}</td>
              <td>NT$ {{ item.UnitPrice }}</td>
              <td>NT$ {{ item.Quantity * item.UnitPrice }}</td>
            </tr>
          </tbody>
        </table>
        <div class="text-end fs-5 fw-bold mt-3">總金額：NT$ {{ selectedOrder.TotalAmount }}</div>
      </div>
    </div>

    <!-- 所有訂單明細 -->
<div v-if="currentTab === 'allDetails'" class="mt-4">
  <h4>所有訂單明細</h4>
  <div v-if="allOrderDetailsGroupedArray.length === 0">目前沒有訂單明細</div>

  <div
    v-for="group in allOrderDetailsGroupedArray"
    :key="group.orderId"
    class="mb-4"
  >
    <h5 class="mb-3">訂單 #{{ group.orderId }}</h5>

    <div class="card shadow-sm bg-white p-3">
      <table class="table table-hover mb-0">
        <thead class="table-light">
          <tr>
            <th>書籍</th>
            <th>數量</th>
            <th>單價</th>
            <th>小計</th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="item in group.details" :key="item.BookID">
            <td>
              <div class="d-flex align-items-center gap-3 flex-grow-1">
                                <img
                    :src="
                      item.Book?.coverUrl && item.Book.coverUrl.startsWith('http')
                        ? item.Book.coverUrl
                        : `/api/BookImages/${item.Book?.id}/cover`
                    "
                    :alt="item.Book?.title || 'Book Cover'"
                    @error="(e) => {
                      const target = e.target as HTMLImageElement | null
                      if (target) target.src = '/placeholder.png'
                    }"
                    class="rounded shadow-sm"
                    style="width: 60px; height: 80px; object-fit: cover"
                  />
                <div>
                  <strong class="fs-6">
                    {{ item.Book?.title || '(已下架)' }}
                  </strong>
                  <div class="text-muted small">
                    NT$ {{ item.Book?.salePrice ?? item.UnitPrice ?? 0 }}
                  </div>
                </div>
              </div>
            </td>

            <td>{{ item.Quantity }}</td>
            <td>NT$ {{ item.UnitPrice }}</td>
            <td>NT$ {{ item.Quantity * item.UnitPrice }}</td>
          </tr>
        </tbody>
      </table>
      <div class="text-end fs-5 fw-bold mt-3">
        總金額：NT$ {{ group.total }}
      </div>
    </div>
  </div>
</div>

</template>

<style scoped>
.container {
  background-color: #fff;
  border-radius: 8px;
  padding: 2rem;
}
button.active {
  background-color: #0d6efd !important;
  color: white !important;
}
.card {
  border-radius: 8px;
}
.table th,
.table td {
  vertical-align: middle;
}
.text-end {
  text-align: right;
}
</style>
