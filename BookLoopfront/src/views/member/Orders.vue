<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useAuth } from '@/stores/auth'
import { getOrdersByMember, Order } from '@/api/order'
import { useRouter } from 'vue-router'

const router = useRouter()
const auth = useAuth()

// 會員 ID
const memberId = computed<number | null>(() => {
  const m = (auth as any).member
  const id = m?.memberId ?? m?.MemberID ?? m?.id ?? null
  if (id == null) return null
  return typeof id === 'string' ? Number(id) : id
})

const orders = ref<Order[]>([])

// 訂單狀態對應
const orderStatusMap: Record<number, string> = {
  0: '待付款',
  1: '已付款',
  2: '已出貨',
  3: '完成訂單',
  4: '已取消',
}

async function loadOrders() {
  if (!memberId.value) return
  try {
    const list = await getOrdersByMember(memberId.value)
    orders.value = list
  } catch (err) {
    console.error('載入訂單失敗', err)
  }
}

onMounted(() => {
  loadOrders()
})

// 前往訂單明細
function viewOrderDetail(orderId: number) {
  router.push({ path: '/order-center', query: { selectedOrderId: orderId.toString() } })
}
</script>

<template>
  <div class="container py-4">
    <h3>📦 訂單紀錄</h3>
    <div v-if="!memberId" class="alert alert-info">
      您尚未登入，請先登入。
      <button class="btn btn-sm btn-primary ms-2" @click="router.push({ name: 'Login' } as any)">
        前往登入
      </button>
    </div>

    <div v-else>
      <div v-if="orders.length === 0" class="text-center text-muted py-4">目前沒有訂單紀錄</div>

      <div v-else class="row g-3">
        <div v-for="order in orders" :key="order.OrderID" class="col-md-6">
          <div class="card p-3 shadow-sm rounded-3">
            <h5 class="card-title">訂單 #{{ order.OrderID }}</h5>
            <p class="card-text">
              下單時間: {{ order.OrderDate ? new Date(order.OrderDate).toLocaleString() : '-' }}
            </p>
            <p class="card-text">
              💰 總金額: NT$ {{ order.TotalAmount ?? 0 }} <br />
              📦 狀態:
              <span class="badge bg-primary text-light">{{
                orderStatusMap[order.Status ?? 0]
              }}</span>
            </p>
            <button class="btn btn-sm btn-primary" @click="viewOrderDetail(order.OrderID!)">
              查看明細
            </button>
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
}
</style>
