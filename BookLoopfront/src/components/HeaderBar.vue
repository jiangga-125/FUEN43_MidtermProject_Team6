<!-- src/components/HeaderBar.vue -->
<template>
  <header class="headerbar bg-white py-2 shadow-sm">
    <div class="container d-flex align-items-center gap-3">
      <!-- LOGO -->
      <router-link to="/" class="me-3 text-decoration-none fw-bold fs-4">Logo</router-link>

      <!-- 搜尋欄 + 購物車按鈕 -->
      <div class="flex-grow-1 d-flex align-items-center gap-3">
        <!-- 搜尋 -->
        <div class="input-group flex-grow-1">
          <input
            v-model="searchText"
            @keyup.enter="submitSearch"
            type="search"
            class="form-control"
            placeholder="請輸入書名、作者、ISBN..."
            aria-label="搜尋書籍"
          />
          <button class="btn btn-primary" @click="submitSearch" type="button">搜尋</button>
        </div>

        <!-- 購物車按鈕 -->
        <button class="btn btn-danger cart-btn position-relative" @click="showCart = true">
          🛒 購物車
          <span v-if="cartCount > 0" class="cart-count">{{ cartCount }}</span>
        </button>
      </div>
    </div>
  </header>

  <!-- 購物車彈窗 / 側邊欄 -->
  <CartDrawer v-model:visible="showCart" />
</template>

<script setup lang="ts">
import { ref, watch, computed } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import CartDrawer from '@/components/CartDrawer.vue'
import { useCartStore } from '@/stores/cart'

const router = useRouter()
const route = useRoute()

// 搜尋框
const searchText = ref<string>((route.query.q as string) || '')

// 購物車彈窗控制
const showCart = ref(false)

// 使用 Pinia 購物車 store
const cartStore = useCartStore()
const cartCount = computed(() => cartStore.totalItems || 0) // 預設 0

// 搜尋功能
function submitSearch() {
  const q = (searchText.value || '').trim()
  const basePath = '/listings'

  if (!q) {
    router.push({ path: basePath })
    return
  }

  router.push({
    path: basePath,
    query: { q, page: '1' }
  })
}

// 當 route.query.q 變動時同步 input
watch(() => route.query.q, val => {
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

@media (max-width: 768px) {
  .input-group .form-control {
    min-width: 150px;
  }
}

.cart-btn {
  font-weight: bold;
  font-size: 16px;
  padding: 8px 16px;
  border-radius: 50px;
  display: flex;
  align-items: center;
  justify-content: center;
  position: relative;
}

.cart-count {
  display: inline-block;
  min-width: 20px;
  padding: 2px 6px;
  font-size: 12px;
  color: white;
  background: red;
  border-radius: 12px;
  text-align: center;
  position: absolute;
  top: -6px;
  right: -6px;
}
</style>