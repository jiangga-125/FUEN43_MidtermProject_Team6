<script setup lang="ts">
import { onMounted, ref, watch } from 'vue'
import { getBooks, type Book } from '@/api/catalog'
import ProductCard from './ProductCard.vue'
import UsedListingsGrid from '@/components/UsedListingsGrid.vue' // ★ 新增

/* ★ 從父層接收 categoryId，當它或 tab 改變時重新抓資料 */
const props = defineProps<{ categoryId: number | null }>()

type TabKey = 'new' | 'hot' | 'list'
const tab = ref<TabKey>('new')

const page = ref(1)
const pageSize = ref(8)
const total = ref(0)
const items = ref<Book[]>([])
const loading = ref(false)
const err = ref('')

async function load() {
  // ★ 二手書頁籤不透過 getBooks，交給 UsedListingsGrid
  if (tab.value === 'list') {
    loading.value = false
    err.value = ''
    items.value = []
    return
  }

  try {
    loading.value = true
    err.value = ''
    const { items: list, total: t } = await getBooks({
      tab: tab.value,
      categoryId: props.categoryId,
      page: page.value,
      pageSize: pageSize.value,
    })
    items.value = list
    total.value = t
  } catch (e: any) {
    err.value = e?.response?.data?.message ?? '讀取商品失敗'
  } finally {
    loading.value = false
  }
}

/* ★ 切換 tab/分類/頁數就重新抓 */
watch([() => props.categoryId, tab, page], () => {
  load()
})
onMounted(load)

function setTab(k: TabKey) {
  if (tab.value !== k) {
    tab.value = k
    page.value = 1
  }
}

function next() {
  if (page.value * pageSize.value < total.value) page.value++
}
function prev() {
  if (page.value > 1) page.value--
}

/* 先做可見動作：之後把這裡改成真正的購物車/收藏 API */
function addToCart(b: Book) {
  alert(`加入購物車：${b.title}`)
}
function like(b: Book) {
  alert(`已收藏：${b.title}`)
}
</script>

<template>
  <section class="panel">
    <div class="tabs">
      <button :class="{ active: tab === 'new' }" @click="setTab('new')">新書熱推</button>
      <button :class="{ active: tab === 'hot' }" @click="setTab('hot')">熱門排行</button>
      <button :class="{ active: tab === 'list' }" @click="setTab('list')">二手書</button>
      <div class="spacer" />
       <div class="pager" v-if="tab !== 'list'"><!-- ★ 二手書不用這個分頁器 -->
        <button @click="prev" :disabled="page <= 1">‹</button>
        <span>{{ page }}</span>
        <button @click="next" :disabled="page * pageSize >= total">›</button>
      </div>
    </div>

     <!-- 新書 / 熱門：舊有格狀卡片 -->
    <template v-if="tab !== 'list'">
      <div v-if="loading" class="muted">載入中…</div>
      <div v-else-if="err" class="err">{{ err }}</div>
      <div v-else class="grid">
        <ProductCard v-for="b in items" :key="b.bookId" :book="b" @add="addToCart" @like="like" />
      </div>
    </template>

    <!-- 二手書：直接嵌入共用清單元件 -->
    <section v-else>
      <UsedListingsGrid />
    </section>
  </section>
</template>

<style scoped>
.panel {
  background: #fff;
  border: 1px solid #e9ecef;
  border-radius: 12px;
  padding: 12px;
}
.tabs {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 12px;
}
.tabs button {
  background: #f1f3f5;
  border: 0;
  padding: 8px 12px;
  border-radius: 999px;
  cursor: pointer;
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
