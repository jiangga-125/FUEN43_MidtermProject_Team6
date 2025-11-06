<!-- src/components/HeaderBar.vue -->
<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import CartDrawer from '@/components/CartDrawer.vue'
import { useCartStore } from '@/stores/cart'
import { useAuth } from '@/stores/auth'
// Router
const router = useRouter()
const route = useRoute()

const auth = useAuth()
const cartStore = useCartStore()
// 實際登入會員ID
// const memberId = 616

// 搜尋欄
const searchText = ref<string>((route.query.q as string) || '')

// 購物車彈窗控制
const showCart = ref(false)

// 計算式：從 auth store 解析 memberId（可能為 number 或 null）
const memberIdComputed = computed<number | null>(() => {
  const m = (auth as any).member
  const id = m?.memberId ?? m?.MemberID ?? m?.id ?? null
  if (id === undefined || id === null) return null
  const n = Number(id)
  return Number.isNaN(n) ? null : n
})

// Pinia 購物車 store
const cartCount = computed(() => cartStore.totalItems || 0)

// 搜尋功能
function submitSearch() {
  const q = (searchText.value || '').trim()
  const basePath = '/listings'
  if (!q) {
    router.push({ path: basePath })
    return
  }
  router.push({ path: basePath, query: { q, page: '1' } })
}

// 跳到訂單中心
function goToOrders() {
  router.push({ path: '/order-center' })
}

// 開關購物車：開啟時嘗試載入（若已登入）
async function toggleCart() {
  showCart.value = !showCart.value

  if (showCart.value) {
    const mid = memberIdComputed.value
    if (mid != null) {
      try {
        await cartStore.initCart(mid)
      } catch (e) {
        console.error('initCart failed', e)
      }
    } else {
      // 沒登入，可載 guest cart 或清空
      cartStore.clearCart()
    }
  }
}

// 當 route.query.q 變動時同步 input
watch(
  () => route.query.q,
  (val) => {
    searchText.value = String(val || '')
  },
)
// 當使用者登入/登出時，如果購物車面板開著要自動 reload
watch(
  () => (auth as any).member,
  async () => {
    if (showCart.value) {
      const mid = memberIdComputed.value
      if (mid != null) await cartStore.initCart(mid)
      else cartStore.clearCart()
    }
  },
)
</script>

<template>
  <header class="headerbar bg-white py-2 shadow-sm">
    <div class="container d-flex align-items-center gap-3">
      <!-- LOGO -->
      <router-link to="/" class="me-3 text-decoration-none fw-bold fs-4">Logo</router-link>

      <!-- 搜尋欄 + 訂單中心 + 購物車按鈕 -->
      <div class="flex-grow-1 d-flex align-items-center gap-3">
        <!-- 搜尋框 -->
        <div class="input-group flex-grow-1">
          <input
            v-model="searchText"
            @keyup.enter="submitSearch"
            type="search"
            class="form-control"
            placeholder="請輸入書名、作者、ISBN..."
            aria-label="搜尋書籍"
          />
          <button class="btn btn-primary" @click="submitSearch">搜尋</button>
        </div>
        <!-- 訂單中心按鈕 -->
        <button class="btn btn-outline-primary px-4" style="min-width: 140px" @click="goToOrders">
          📦 訂單中心
        </button>

        <!-- 購物車按鈕 -->
        <button
          class="btn btn-danger position-relative px-4"
          style="min-width: 140px"
          @click="toggleCart"
        >
          🛒 購物車
          <span
            v-if="cartCount > 0"
            class="position-absolute top-0 start-100 translate-middle badge rounded-pill bg-warning text-dark"
            style="font-size: 0.75rem"
          >
            {{ cartCount }}
          </span>
        </button>
      </div>
    </div>

    <!-- 購物車彈窗 -->
    <CartDrawer v-model:visible="showCart" :memberId="memberIdComputed ?? undefined" />
  </header>
</template>

<style scoped>
.headerbar {
  position: sticky;
  top: 60px;
  z-index: 1050;
}

.input-group .form-control {
  min-width: 420px;
}

.btn-close {
  background: none;
  border: none;
  cursor: pointer;
}

.cart-count {
  position: absolute;
  top: -6px;
  right: -6px;
  min-width: 20px;
  padding: 2px 6px;
  font-size: 12px;
  color: #fff;
  background: red;
  border-radius: 12px;
  text-align: center;
}

@media (max-width: 768px) {
  .input-group .form-control {
    min-width: 150px;
  }
}
</style>
