<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import { getBooks, type Book } from '@/api/book'
import ProductCard from './ProductCard.vue'
import { addToCart as addCartAPI } from '@/api/shoppingCart'
import http from '@/lib/http'
import UsedListingsGrid from '@/components/UsedListingsGrid.vue'

// 從父層接收 categoryId
const props = defineProps<{ categoryId: number | null }>()

type TabKey = 'new' | 'hot' | 'list'
const tab = ref<TabKey>('new')

const page = ref(1)
const pageSize = ref(8)
const total = ref(0)
const items = ref<Book[]>([])
const loading = ref(false)
const err = ref('')
const memberId = ref<number | null>(null)
// const memberId = 616 // 確認資料庫有這個會員

async function load() {
  if (tab.value === 'list') {
    loading.value = false
    err.value = ''
    items.value = []
    return
  }

  try {
    loading.value = true
    const { items: list, total: t } = await getBooks({
      tab: tab.value,
      categoryId: props.categoryId,
      page: page.value,
      pageSize: pageSize.value,
    })
    console.log('📦 items loaded:', list) // <- 新增這行
    items.value = list
    total.value = t
  } catch (e: any) {
    err.value = e?.response?.data?.message ?? '讀取商品失敗'
  } finally {
    loading.value = false
  }
}
async function loadMemberInfo() {
  // 優先：請求後端 /api/auth/me（需後端支援，回傳 JSON 包 memberId）
  try {
    const r = await http.get('/api/auth/me')
    memberId.value = r.data?.memberId ?? r.data?.MemberID ?? null
    // console.log('從 /api/auth/me 取得 memberId=', memberId.value)
    return
  } catch (err) {
    // 如果失敗，再嘗試從 localStorage 的 token decode（fallback）
    // console.log('無法從 /api/auth/me 取得，改從 token decode', err)
  }

  // fallback: 從 localStorage（或 sessionStorage）解 JWT
  try {
    const token = localStorage.getItem('token') || localStorage.getItem('access_token')
    if (!token) return
    const payload = JSON.parse(atob(token.split('.')[1]))
    const maybeId = payload.memberId ?? payload.userId ?? payload.sub ?? payload.id
    memberId.value = maybeId ?? null
    // console.log('從 token decode memberId=', memberId.value)
  } catch (e) {
    // console.warn('decode token 失敗', e)
  }
}
watch([() => props.categoryId, tab, page], load)
onMounted(async () => {
  await loadMemberInfo()
  await load()
})
function setTab(k: TabKey) {
  if (tab.value !== k) page.value = 1
  tab.value = k
}

function next() {
  if (page.value * pageSize.value < total.value) page.value++
}
function prev() {
  if (page.value > 1) page.value--
}

// 加入購物車
async function addToCart(b: Book) {
  try {
    const payload: any = {
      BookID: b.id,
      Quantity: 1,
      UnitPrice: b.salePrice ?? b.listPrice ?? 0,
    }

    // 若後端需要 MemberID（臨時做法），只在 memberId 有值時附上
    if (memberId.value) payload.MemberID = memberId.value

    console.log('加入購物車 payload', payload)
    const res = await addCartAPI(payload)
    console.log('購物車回傳資料', res)
    alert(`✅ 已加入購物車：${b.title}`)
  }catch (e: any) {
    console.error('加入購物車錯誤', e)
    if (e?.response) {
      console.group('加入購物車 Axios 錯誤')
      console.log('status:', e.response.status)
      console.log('headers:', e.response.headers)
      console.log('data:', e.response.data)
      console.groupEnd()

      // 只取 message 屬性，不用整個物件
      const msg = e.response.data?.message ?? '加入購物車失敗'
      alert(`❌ ${msg}`)
    } else {
      alert(`❌ 加入購物車失敗: ${e?.message ?? '未知錯誤'}`)
    }
  }
}
</script>

<template>
  <section class="panel">
    <div class="tabs">
      <button :class="{ active: tab === 'new' }" @click="setTab('new')">新書熱推</button>
      <button :class="{ active: tab === 'hot' }" @click="setTab('hot')">熱門排行</button>
      <button :class="{ active: tab === 'list' }" @click="setTab('list')">二手書</button>
      <div class="spacer"></div>
      <div class="pager" v-if="tab !== 'list'">
        <button @click="prev" :disabled="page <= 1">‹</button>
        <span>{{ page }}</span>
        <button @click="next" :disabled="page * pageSize >= total">›</button>
      </div>
    </div>

    <template v-if="tab !== 'list'">
    <div v-if="loading" class="muted">載入中…</div>
    <div v-else-if="err" class="err">{{ err }}</div>
    <div v-else class="grid">
      <ProductCard v-for="b in items" :key="b.id" :book="b" @add="addToCart" />
    </div>
    </template>

    <section v-else>
      <UsedListingsGrid />
    </section>

  </section>
</template>

<style scoped>
.panel {
  background: #fff;
  padding: 12px;
  border-radius: 12px;
  border: 1px solid #e9ecef;
}
.tabs {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 12px;
}
.tabs button {
  padding: 8px 12px;
  border-radius: 999px;
  cursor: pointer;
  border: 0;
  background: #f1f3f5;
}
.tabs button.active {
  background: #0d6efd;
  color: #fff;
}
.spacer {
  flex: 1;
}
.pager {
  display: flex;
  align-items: center;
  gap: 8px;
}
.pager button {
  width: 28px;
  height: 28px;
  border-radius: 8px;
  border: 1px solid #e1e1e1;
  background: #fff;
  cursor: pointer;
}
.grid {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 12px;
}
.muted {
  color: #7a7a7a;
}
.err {
  color: #c00;
}
@media (max-width: 900px) {
  .grid {
    grid-template-columns: repeat(2, 1fr);
  }
}
</style>
