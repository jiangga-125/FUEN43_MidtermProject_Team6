<!-- src/components/ProductTabs.vue -->
<script setup lang="ts">
import { ref, onMounted, watch, computed } from 'vue'
import { useRouter } from 'vue-router'
import { getBooks, type Book } from '@/api/book'
import ProductCard from './ProductCard.vue'
import { addToCart as addCartAPI } from '@/api/shoppingCart'
import UsedListingsGrid from '@/components/UsedListingsGrid.vue'
// 使用 pinia auth store（你檔案最底有 export useAuth）
import { useAuth } from '@/stores/auth'

const router = useRouter()
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

const auth = useAuth()
// 取得 memberId（保護轉型：可能為 string 或 number）
const memberId = computed<number | null>(() => {
  const id = (auth.member as any)?.memberId ?? (auth.member as any)?.MemberID ?? null
  return typeof id === 'string' ? Number(id) : id
})
// 讀取商品列表
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
    console.log('📦 items loaded:', list)
    items.value = list
    total.value = t
  } catch (e: any) {
    err.value = e?.response?.data?.message ?? '讀取商品失敗'
  } finally {
    loading.value = false
  }
}

onMounted(async () => {
  await load()
})

function setTab(k: TabKey) {
  if (tab.value !== k) page.value = 1
  tab.value = k
  // 立即 reload（watch 也會觸發，但做一次保險）
  load().catch((e) => console.error(e))
}

function next() {
  if (page.value * pageSize.value < total.value) {
    page.value++
    load().catch((e) => console.error(e))
  }
}
function prev() {
  if (page.value > 1) {
    page.value--
    load().catch((e) => console.error(e))
  }
}

// 加入購物車
async function addToCart(b: Book) {
  // 若未登入，提示並導去登入（可改成 modal）
  if (!auth.member) {
    const ok = confirm('你尚未登入，請登入後再加入購物車。要前往登入頁嗎？')
    if (ok) router.push({ name: 'Login' }) // 確認路由名稱是否為 'Login'
    return
  }
  try {
    const payload: any = {
      BookID: b.id,
      Quantity: 1,
      UnitPrice: b.salePrice ?? b.listPrice ?? 0,
    }

    // 短期 fallback：如果後端還要求 MemberID 才存，才附上（長期請後端改由 token 決定）
    if (memberId.value) payload.MemberID = memberId.value

    console.log('加入購物車 payload', payload)
    const res = await addCartAPI(payload)
    console.log('購物車回傳資料', res)
    alert(`✅ 已加入購物車：${b.title}`)
  } catch (e: any) {
    console.error('加入購物車錯誤', e)
    if (e?.response) {
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
