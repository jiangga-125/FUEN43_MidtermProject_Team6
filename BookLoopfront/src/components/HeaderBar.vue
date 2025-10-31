<!-- src/components/HeaderBar.vue -->
<template>
  <header class="headerbar bg-white py-2 shadow-sm">
    <div class="container d-flex align-items-center gap-3">
      <router-link to="/" class="me-3 text-decoration-none">
        <h4 class="mb-0">簿錄書城</h4>
      </router-link>

      <div class="flex-grow-1">
        <div class="input-group">
          <input v-model="searchText" @keyup.enter="submitSearch" type="search" class="form-control" placeholder="請輸入書名、作者、ISBN..." />
          <button class="btn btn-primary" @click="submitSearch" type="button">搜尋</button>
        </div>
      </div>

      <div class="d-flex align-items-center gap-2">
        <router-link to="/auth/login" class="btn btn-outline-secondary btn-sm">登入</router-link>
        <router-link to="/auth/register" class="btn btn-outline-secondary btn-sm">註冊</router-link>
        <router-link to="/cart" class="btn btn-sm btn-warning position-relative">
          🛒
          <span v-if="cartCount > 0" class="badge bg-danger position-absolute top-0 start-100 translate-middle">{{ cartCount }}</span>
        </router-link>
      </div>
    </div>
  </header>
</template>

<script setup lang="ts">
import { ref, watch, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useCartStore } from '@/stores/cart'

const router = useRouter()
const route = useRoute()
const searchText = ref<string>((route.query.q as string) || '')

// 若安裝 Pinia 就使用 store 的 count；若沒安裝則 cartCount 會是 0
let cartCount = 0
try {
  const cart = useCartStore()
  cartCount = cart.count
  // reactive: 若 store 變了，自動更新（composition api template 可以直接使用）
  // 但在 script setup 我們把 cartCount 綁到 store
} catch (e) {
  // Pinia 未註冊或未安裝，忽略
  cartCount = Number(localStorage.getItem('cartCount') || 0)
}

function submitSearch() {
  const q = (searchText.value || '').trim()
  const basePath = '/listings'
  if (!q) {
    router.push({ path: basePath })
    return
  }
  router.push({ path: basePath, query: { q, page: '1' } })
}

// 當 route.query.q 變化時，同步 input
watch(() => route.query.q, (v) => {
  searchText.value = String(v || '')
})
</script>

<style scoped>
.headerbar { position: sticky; top: 0; z-index: 1050; }
.input-group .form-control { min-width: 420px; }
@media (max-width: 768px) {
  .input-group .form-control { min-width: 150px; }
}
</style>
