<template>
  <div class="card p-3">
    <h6 class="mb-3">全站分類</h6>

    <div v-if="loading" class="text-center small text-muted">載入分類中...</div>

    <div v-else class="d-flex flex-column gap-2">
      <button
        v-for="cat in categoriesWithAll"
        :key="cat.id"
        :class="['btn text-start', selectedId === cat.id ? 'btn-primary text-white' : 'btn-outline-primary']"
        @click="selectCategory(cat.id)"
        type="button"
      >
        {{ cat.name }}
      </button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import http from '@/api/http' // 假設這是你 axios 實例（baseURL = '/api'）

const router = useRouter()
const route = useRoute()

const loading = ref(false)
const categories = ref<{ id: number; name: string }[]>([])

// selectedId 從 query.categoryId 初始化（若沒有為 0 = 全部）
const selectedId = ref<number | null>(route.query.categoryId ? parseInt(String(route.query.categoryId)) : 0)

// 若後端沒給分類，預設只放「全部」作為 fallback
const defaultCategories = [{ id: 0, name: '全部' }]

// 把「全部」保證放第一個
const categoriesWithAll = computed(() => {
  if (!categories.value || categories.value.length === 0) return defaultCategories
  if (categories.value.some(c => c.id === 0)) return categories.value
  return [{ id: 0, name: '全部' }, ...categories.value]
})

// 從後端載入分類
async function loadCategories() {
  loading.value = true
  try {
    // 預設呼叫 /categories（會被 http.baseURL('/api') 前綴）
    const res = await http.get('/categories')
    const payload = res.data
    // 偵錯：若 payload 是陣列就直接用；若是 { data: [...] } 也處理
    if (Array.isArray(payload)) categories.value = payload.map((c:any) => ({ id: c.id, name: c.name }))
    else if (Array.isArray(payload.data)) categories.value = payload.data.map((c:any) => ({ id: c.id, name: c.name }))
    else categories.value = []
  } catch (err) {
    console.error('[CategoryList] loadCategories fail', err)
    categories.value = []
  } finally {
    loading.value = false
  }
}

function selectCategory(id: number) {
  selectedId.value = id === 0 ? null : id
  const q = String(route.query.q || '')
  const query: any = {}
  if (q) query.q = q
  if (selectedId.value) query.categoryId = String(selectedId.value)
  query.page = '1'
  router.push({ path: '/listings', query }).catch(e => console.log(e))
}

onMounted(() => {
  loadCategories()
})
</script>

<style scoped>
.btn { border-radius: 8px; padding: 10px 12px; text-align: left; }
.btn-outline-primary { background: #fff; color: #0d6efd; border: 1px solid #0d6efd; }
</style>
