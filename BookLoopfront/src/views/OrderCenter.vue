<script setup lang="ts">
import { ref, onMounted, watch, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getOrdersByMember, getOrderDetail, cancelOrder, deleteOrder, Order } from '@/api/order'
import { getReturnsByOrder } from '@/api/return'
import { useAuth } from '@/stores/auth'
import { createReturn, getReturnsByOrder, ReturnInfo, getStatusText } from '@/api/return'
import { cancelReturn } from '@/api/return'
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

// Tab: 'orders'=我的訂單, 'details'=單筆明細, 'allDetails'=所有訂單明細, 'returns'=退貨紀錄
const currentTab = ref<'orders' | 'details' | 'allDetails' | 'returns'>('orders')
const orders = ref<Order[]>([])
const selectedOrder = ref<Order | null>(null)
const returns = ref<ReturnInfo[]>([])


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
function goHome() {
  router.push('/') // 導向首頁
}

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


// 訂單狀態對應
const orderStatusMap: Record<number, string> = {
  0: '待付款',
  1: '已付款',
  2: '已出貨',
  3: '完成訂單',
  4: '已取消',
}

function goHome() {
  router.push('/')
}

// 載入會員所有訂單
async function loadOrders() {
  // 若沒有登入（無 memberId），就不呼叫 API
  if (memberId.value == null) {
    orders.value = []
    console.warn('loadOrders: memberId is null — skip loading orders')
    return
  }

  try {
    orders.value = await getOrdersByMember(memberId)
  } catch (err) {
    console.error('載入訂單失敗', err)
    // 視需要顯示錯誤給使用者
    // alert('載入訂單失敗')
  }
}

// 載入會員所有退貨紀錄
async function loadAllReturns() {
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

function onPay(orderId: number) {
  window.open(`https://localhost:7176/Orders/Orders/GoToPayment?orderId=${orderId}`, '_blank')
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

// 計算所有訂單明細，用於 new Tab
const allOrderDetails = computed(() =>
  orders.value.flatMap(order =>
    order.OrderDetails.map(od => ({
      ...od,
      parentOrderID: order.OrderID
    }))
  )
)
// 若登出或剛登入，watch memberId 自動 reload 或清空 orders
watch(memberId, (v) => {
  if (v == null) {
    orders.value = []
    selectedOrder.value = null
  } else {
    loadOrders().catch((e) => console.error(e))
  }
})

onMounted(async () => {
  await loadOrders();
  await loadAllReturns();

  for (const order of orders.value) {
    await loadShipment(order.OrderID!);
  }

  // 如果有 query 帶入 selectedOrderId
  const selectedId = route.query.selectedOrderId;
  if (selectedId) {
    const orderId = parseInt(selectedId as string);
    if (!isNaN(orderId)) {
      await viewOrderDetail(orderId);
    }
  }
});
</script>

<template>
  <div class="container py-5">
    <!-- 標題與首頁按鈕 -->
    <div class="d-flex justify-content-between align-items-center mb-4">
      <h1>📦 訂單中心</h1>
      <button class="btn btn-outline-primary" @click="goHome">🏠 回首頁</button>
    </div>

    <!-- Tab 切換 -->
    <div class="mb-4">
      <button class="btn btn-outline-primary me-2" :class="{ active: currentTab==='orders' }" @click="currentTab='orders'">我的訂單</button>
      <button class="btn btn-outline-secondary me-2" :class="{ active: currentTab==='allDetails' }" @click="currentTab='allDetails'">所有訂單明細</button>
      <button class="btn btn-outline-secondary me-2" :disabled="!selectedOrder" :class="{ active: currentTab==='details' }" @click="currentTab='details'">本筆訂單明細</button>
      <button class="btn btn-outline-warning" :disabled="returns.length===0" :class="{ active: currentTab==='returns' }" @click="currentTab='returns'">退貨紀錄</button>
    </div>

<!-- 訂單列表 -->
<div v-if="memberId !== null && currentTab === 'orders'">
  <div v-if="orders.length === 0" class="text-center text-muted py-5">
    目前沒有訂單
  </div>

  <div class="row g-3">
    <div v-for="order in orders" :key="order.OrderID" class="col-md-6">
      <div class="card shadow-sm border-0 rounded-4 overflow-hidden position-relative">
        
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
          <!-- 訂單標題 -->
          <h5 class="card-title fw-bold text-primary">
            訂單 #{{ order.OrderID ?? '-' }}
          </h5>

          <!-- 下單時間 -->
          <p class="card-text text-muted small mb-2">
            下單時間：{{ order.OrderDate ? new Date(order.OrderDate).toLocaleString() : '無資料' }}
          </p>

          <!-- 總金額與訂單狀態 -->
          <p class="card-text mb-1">
            💰 總金額：
            <span class="fw-bold text-success">NT$ {{ order.TotalAmount ?? 0 }}</span>
            <br>
            📦 狀態：
            <span class="badge"
                  :class="{
                    'bg-secondary': order.Status === 0,
                    'bg-primary': order.Status === 1,
                    'bg-info text-dark': order.Status === 2,
                    'bg-success': order.Status === 3,
                    'bg-danger': order.Status === 4
                  }">
              {{ orderStatusMap[order.Status ?? 0] }}
            </span>
          </p>

          <!-- 功能按鈕 -->
          <div class="d-flex flex-wrap gap-2 mt-3">
            <button class="btn btn-sm btn-primary flex-grow-1" 
                    @click="viewOrderDetail(order.OrderID!)">
              查看明細
            </button>
            <button class="btn btn-sm btn-success flex-grow-1" 
                    @click="onPay(order.OrderID!)" 
                    :disabled="order.Status !== 0">
              付款
            </button>
            <button class="btn btn-sm btn-warning flex-grow-1" 
                    @click="onReturn(order.OrderID!)">
              退貨
            </button>
            <button class="btn btn-sm btn-outline-danger flex-grow-1" 
                    @click="onDelete(order.OrderID!)">
              🗑️ 刪除
            </button>
          </div>
        </div>
      </div>
    </div>




    <!-- 單筆訂單明細 -->
    <div v-if="currentTab==='details' && selectedOrder" class="mt-4">
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
                :src="item.Book?.coverUrl && item.Book.coverUrl.startsWith('http')
                      ? item.Book.coverUrl
                      : `/api/BookImages/${item.Book?.id}/cover`"
                :alt="item.Book?.title || 'Book Cover'"
                @error="(e) => { const target = e.target as HTMLImageElement; target.src='/placeholder.png' }"
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
<div v-if="currentTab==='allDetails'" class="mt-4">
  <h4>所有訂單明細</h4>
  <div v-if="allOrderDetails.length===0" class="text-muted py-3">目前沒有訂單明細</div>

  <div v-for="order in orders" :key="order.OrderID" class="mb-4">
    <h5>訂單 #{{ order.OrderID }}</h5>
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
          <tr v-for="item in order.OrderDetails" :key="item.BookID">
            <td>
              <div class="d-flex align-items-center gap-3">
                <img :src="item.Book?.coverUrl && item.Book.coverUrl.startsWith('http') ? item.Book.coverUrl : `/api/BookImages/${item.Book?.id}/cover`"
                     :alt="item.Book?.title || 'Book Cover'" class="rounded shadow-sm" style="width:60px;height:80px;object-fit:cover"/>
                <div>
                  <strong class="fs-6">{{ item.Book?.title || '(已下架)' }}</strong>
                  <div class="text-muted small">NT$ {{ item.Book?.salePrice ?? item.UnitPrice ?? 0 }}</div>
                </div>
              </div>
            </td>
            <td>{{ item.Quantity }}</td>
            <td>NT$ {{ item.UnitPrice }}</td>
            <td>NT$ {{ item.Quantity * item.UnitPrice }}</td>
          </tr>
        </tbody>
      </table>
      <div class="text-end fs-5 fw-bold mt-3">總金額：NT$ {{ order.TotalAmount }}</div>
    </div>
  </div>
</div>
    <!-- 退貨紀錄 -->
    <div v-if="currentTab==='returns'" class="mt-4">
      <h4>退貨紀錄</h4>
      <div v-if="returns.length===0" class="text-muted py-3">目前沒有退貨紀錄</div>
      <div class="row g-3">
        <div v-for="r in returns" :key="r.returnID" class="col-md-6">
          <div class="card shadow-sm border-0 rounded-4 overflow-hidden position-relative">
            <button v-if="r.status !== 9" class="btn btn-sm btn-outline-danger position-absolute top-0 end-0 m-2" @click="cancelReturnOrder(r.returnID)">❌ 取消退貨</button>
            <div class="card-body">
              <h5 class="card-title fw-bold text-primary">退貨編號：#{{ r.returnID }}</h5>
              <p class="card-text mb-1">📝 原因：{{ r.returnReason }}</p>
              <p class="card-text mb-1">📦 狀態：<span class="badge bg-warning text-dark">{{ getStatusText(r.status) }}</span></p>
              <p class="card-text text-muted small">訂單編號：#{{ r.orderID }}</p>
            </div>
          </div>
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
.table th, .table td {
  vertical-align: middle;
}
.text-end {
  text-align: right;
}
</style>
