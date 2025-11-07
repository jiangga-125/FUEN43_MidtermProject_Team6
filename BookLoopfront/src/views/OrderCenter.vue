<script setup lang="ts">
import { ref, onMounted, watch, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { getOrdersByMember, getOrderDetail, cancelOrder, deleteOrder, Order } from '@/api/order'
import { getReturnsByOrder } from '@/api/return'
import { useAuth } from '@/stores/auth'

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

const currentTab = ref<'orders' | 'details' | 'returns'>('orders')
const orders = ref<Order[]>([])
const selectedOrder = ref<Order | null>(null)
const returns = ref<any[]>([])

function goHome() {
  router.push('/') // 導向首頁
}
// 訂單狀態對應
const orderStatusMap: Record<number, string> = {
  0: '待付款',
  1: '已下訂',
  2: '已出貨',
  3: '完成訂單',
  4: '已取消',
}

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
  } catch (err) {
    console.error('載入訂單失敗', err)
    // 視需要顯示錯誤給使用者
    // alert('載入訂單失敗')
  }
}

async function onPay(orderId: number) {
  alert(`導向付款流程：OrderID ${orderId}`)
  // 這裡之後可以整合 ECPay 或其他付款流程
}

async function onReturn(orderId: number) {
  alert(`導向退貨流程：OrderID ${orderId}`)
  // 這裡可以開退貨頁或彈出退貨理由 modal
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

onMounted(async () => {
  await loadOrders()

  const selectedId = route.query.selectedOrderId
  if (selectedId) {
    const orderId = parseInt(selectedId as string)
    if (!isNaN(orderId)) {
      await viewOrderDetail(orderId)
    }
  }
})
</script>

<template>
  <div class="container py-5">
    <!-- 在訂單中心標題旁邊或上方加回首頁按鈕 -->
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
        class="btn btn-outline-warning"
        :disabled="!selectedOrder"
        :class="{ active: currentTab === 'returns' }"
        @click="currentTab = 'returns'"
      >
        退貨紀錄
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
                  📦 狀態：<span class="badge bg-secondary">{{
                    orderStatusMap[order.Status ?? 0]
                  }}</span>
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
                    :disabled="(order.Status ?? 0) < 2"
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

    <!-- 訂單明細 -->
    <div v-if="currentTab === 'details' && selectedOrder" class="mt-4">
      <h4>訂單明細：#{{ selectedOrder.OrderID }}</h4>
      <div class="card shadow-sm bg-white p-3">
        <table class="table table-hover mb-0">
          <thead class="table-light">
            <tr>
              <th></th>
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

    <!-- 退貨紀錄 -->
    <div v-if="currentTab === 'returns' && selectedOrder" class="mt-4">
      <h4>退貨紀錄：訂單 #{{ selectedOrder.OrderID }}</h4>
      <div v-if="returns.length === 0" class="text-muted py-3">目前沒有退貨紀錄</div>
      <ul class="list-group">
        <li v-for="r in returns" :key="r.ReturnID" class="list-group-item">
          退貨編號：{{ r.ReturnID }} | 原因：{{ r.ReturnReason }} | 狀態：{{ r.Status }}
        </li>
      </ul>
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
