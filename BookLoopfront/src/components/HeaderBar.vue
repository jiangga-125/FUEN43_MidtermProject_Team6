<!-- src/components/HeaderBar.vue -->
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
<button class="btn btn-danger position-relative px-4" style="min-width: 140px" @click="showCart = true">
  🛒 購物車
  <span
    v-if="cartCount > 0"
    class="position-absolute top-0 start-100 translate-middle badge rounded-pill bg-warning text-dark"
    style="font-size: 0.75rem;"
  >
    {{ cartCount }}
  </span>
</button>
      </div>
    </div>

    <!-- 購物車彈窗 -->
    <CartDrawer v-model:visible="showCart" :memberId="memberId" />
  </header>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import CartDrawer from '@/components/CartDrawer.vue'
import { useCartStore } from '@/stores/cart'

// Router
const router = useRouter()
const route = useRoute()

// 實際登入會員ID
const memberId = 616

// 搜尋欄
const searchText = ref<string>((route.query.q as string) || '')

// 購物車彈窗控制
const showCart = ref(false)

// Pinia 購物車 store
const cartStore = useCartStore()
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

// 當 route.query.q 變動時同步 input
watch(() => route.query.q, (val) => {
  searchText.value = String(val || '')
})
</script>

<style scoped>
.headerbar {
  position: sticky;
  top: 0;
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
