<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import { getBooks, type Book } from '@/api/book'
import ProductCard from './ProductCard.vue'
import { addToCart as addCartAPI } from '@/api/shoppingCart'
import { useCartStore } from '@/stores/cart'

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

const memberId = 616 // 確認資料庫有這個會員
const cartStore = useCartStore()// 購物車

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

watch([() => props.categoryId, tab, page], load)

onMounted(async () => {
  await cartStore.initCart(memberId) // ✅ 初始化時設定 memberId
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
    const payload = {
      MemberID: memberId,
      BookID: b.id,
      Quantity: 1,
      UnitPrice: b.salePrice ?? b.listPrice ?? 0,
    }
    await addCartAPI(payload)

    // 🔹 立即刷新購物車資料（HeaderBar會自動更新）
    await cartStore.fetchCart()

    alert(`✅ 已加入購物車：${b.title}`)
  } catch (e: any) {
    console.error('加入購物車錯誤', e)
    alert(`❌ 加入購物車失敗`)
  }
}
</script>

<template>
  <section class="panel">
    <div class="tabs">
      <button :class="{ active: tab === 'new' }" @click="setTab('new')">新書熱推</button>
      <button :class="{ active: tab === 'hot' }" @click="setTab('hot')">熱門排行</button>
      <div class="spacer" />
      <div class="pager" v-if="tab !== 'list'">
        <button @click="prev" :disabled="page <= 1">‹</button>
        <span>{{ page }}</span>
        <button @click="next" :disabled="page * pageSize >= total">›</button>
      </div>
    </div>

    <div v-if="loading" class="muted">載入中…</div>
    <div v-else-if="err" class="err">{{ err }}</div>
    <div v-else class="grid">
      <ProductCard v-for="b in items" :key="b.id" :book="b" @add="addToCart" />
    </div>
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
