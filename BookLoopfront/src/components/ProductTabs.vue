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
import { useCartStore } from '@/stores/cart'

const cartStore = useCartStore()
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

async function addToCart(b: Book) {
  // 若未登入，提示並導去登入（可改成 modal）
  if (!auth.member) {
    console.log('[addToCart] user not logged in, prompt to go to login')
    const ok = confirm('你尚未登入，請登入後再加入購物車。要前往登入頁嗎？')
    console.log('[addToCart] confirm result:', ok)
    if (!ok) return

    // 正確使用你實際的 route name（你說的是 'login'）
    router
      .push({ name: 'login' })
      .then(() => {
        console.log('[addToCart] router.push by name succeeded')
      })
      .catch((err) => {
        console.warn('[addToCart] push by name failed:', err)
        // fallback：用 path
        router
          .push({ path: '/login' })
          .then(() => console.log('[addToCart] router.push by path succeeded'))
          .catch((err2) => {
            console.error('[addToCart] push by path failed too:', err2)
            // 最後保險：直接改 window.location.href（會 full reload）
            window.location.href = '/login'
          })
      })
    return
  }

  // ========== 真正加入購物車邏輯 ==========
  try {
    const payload: any = {
      BookID: b.id,
      Quantity: 1,
      UnitPrice: b.salePrice ?? b.listPrice ?? 0,
    }

    if (memberId.value) payload.MemberID = memberId.value

    console.log('加入購物車 payload', payload)
    const res = await addCartAPI(payload)
    console.log('購物車回傳資料', res)

    // ✅ 從後端抓最新購物車，HeaderBar 會同步更新
    if (memberId.value) {
      await cartStore.fetchCart()
    }

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
      <!-- 改成 SVG 按鈕的 pager -->
      <div class="pager" v-if="tab !== 'list'">
        <button @click="prev" :disabled="page <= 1" aria-label="上一頁">
          <svg
            viewBox="0 0 24 24"
            xmlns="http://www.w3.org/2000/svg"
            aria-hidden="true"
            focusable="false"
          >
            <path d="M15 18 L9 12 L15 6" stroke-linecap="round" stroke-linejoin="round"></path>
          </svg>
        </button>

        <span>{{ page }}</span>

        <button @click="next" :disabled="page * pageSize >= total" aria-label="下一頁">
          <svg
            viewBox="0 0 24 24"
            xmlns="http://www.w3.org/2000/svg"
            aria-hidden="true"
            focusable="false"
          >
            <path d="M9 6 L15 12 L9 18" stroke-linecap="round" stroke-linejoin="round"></path>
          </svg>
        </button>
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
  gap: 12px;
  flex-shrink: 0;
  padding: 4px;
}
.pager button {
  width: 42px;
  height: 42px;
  border-radius: 50%;
  border: 1px solid #e6eefc;
  background: #ffffff;
  cursor: pointer;
  font-size: 20px;
  line-height: 1;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  box-shadow: 0 2px 6px rgba(15, 23, 42, 0.04);
  transition:
    transform 0.12s ease,
    box-shadow 0.12s ease,
    border-color 0.12s ease,
    background 0.12s ease;
  padding: 0;
  box-sizing: border-box;
}
/* SVG 在按鈕裡的尺寸與顏色 */
.pager button svg {
  width: 18px;
  height: 18px;
  display: block;
  stroke: #333;
  stroke-width: 2;
  fill: none;
  stroke-linecap: round;
  stroke-linejoin: round;
}

/* hover / focus 效果（除 disabled） */
.pager button:hover:not(:disabled),
.pager button:focus:not(:disabled) {
  transform: translateY(-3px);
  box-shadow: 0 8px 22px rgba(13, 110, 253, 0.12);
  border-color: #0d6efd;
}

/* 點擊時的微動畫 */
.pager button:active:not(:disabled) {
  transform: translateY(-1px) scale(0.98);
}

/* disabled 樣式 */
.pager button:disabled {
  opacity: 0.45;
  cursor: not-allowed;
  transform: none;
  box-shadow: none;
  border-color: #eee;
}

/* 當前頁數視覺（圓角膠囊） */
.pager span {
  min-width: 46px;
  text-align: center;
  font-weight: 600;
  padding: 6px 12px;
  border-radius: 999px;
  background: #f7f9ff;
  border: 1px solid #eef4ff;
  color: #0b5ed7;
  box-shadow: inset 0 -1px 0 rgba(0, 0, 0, 0.02);
  user-select: none;
  font-size: 14px;
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
