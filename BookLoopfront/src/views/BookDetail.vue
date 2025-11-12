<script setup lang="ts">
import { ref, onMounted, computed, nextTick, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import http from '@/lib/http'
import ProductCard from '@/components/ProductCard.vue'
import { useAuth } from '@/stores/auth'

const route = useRoute()
const router = useRouter()
const loading = ref(true)
const auth = useAuth()
const error = ref<string | null>(null)
const rawItem = ref<any | null>(null)
const book = ref<any | null>(null)
const adding = ref(false)

const fakeReviews = [
  {
    title: '超棒的書！',
    author: '小美',
    rating: 5,
    content: '內容淺顯易懂，讓我快速上手程式設計！',
    createdAt: new Date('2025-10-01').toISOString(),
  },
  {
    title: '值得推薦 📚',
    author: '阿明',
    rating: 4,
    content: '範例豐富，實用性高，唯一缺點是字有點小～',
    createdAt: new Date('2025-10-03').toISOString(),
  },
  {
    title: '不錯的參考書',
    author: '讀者A',
    rating: 5,
    content: '學習時隨時可以翻來查，很方便。',
    createdAt: new Date('2025-10-05').toISOString(),
  },
]

const reviews = ref<
  {
    title: string
    author: string
    rating: number
    content: string
    createdAt: string
    displayName?: string
  }[]
>([])

const loadingReviews = ref(true)
const related = ref<any[]>([])
const sidebarList = ref<any[]>([])
const branchList = ref<any[]>([])

// === 庫存、圖片與欄位計算 ===
const totalAvailable = computed(() => {
  return (branchList.value ?? []).reduce((s: number, b: any) => s + Number(b.available ?? 0), 0)
})

const imgSrc = computed(() => {
  if (!book.value) return '/placeholder.png'
  return (
    book.value.coverUrl ||
    (book.value.id ? `/api/BookImages/book/${book.value.id}/cover` : '/placeholder.png')
  )
})

const isDescriptionString = computed(
  () => typeof book.value?.description === 'string' && book.value?.description.length > 0,
)
const descriptionExtracted = computed(() => {
  const d = book.value?.description
  if (!d || typeof d === 'string') return null
  return d.content ?? d.summary ?? d.html ?? null
})

const totalStock = computed(() => {
  const s = book.value?.stock ?? totalAvailable.value
  if (s === null || s === undefined) return null
  return Number(s)
})

const stockDisplay = computed(() => {
  if (totalStock.value === null) return '—'
  return totalStock.value > 0 ? String(totalStock.value) : '缺貨'
})

function smallCover(b: any) {
  return b.coverUrl ?? (b.id ? `/api/BookImages/${b.id}/cover` : '/placeholder.png')
}

// === 隨機推薦 / 同類熱銷 ===
function shuffleArray<T>(arr: T[]): T[] {
  for (let i = arr.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1))
    const tmp = arr[i]
    arr[i] = arr[j]
    arr[j] = tmp
  }
  return arr
}

async function loadSidebarAndRelated(): Promise<void> {
  const currentId = Number(book.value?.id ?? 0)
  const normalize = (x: Record<string, any>) => ({
    id: x?.id ?? x?.bookId ?? x?.BookID ?? null,
    title: x?.title ?? x?.name ?? '',
    coverUrl: x?.coverUrl ?? x?.imageUrl ?? x?.filePath ?? x?.cover ?? null,
    price: x?.salePrice ?? x?.SalePrice ?? x?.price ?? null,
  })

  sidebarList.value = []
  related.value = []

  try {
    const catId = book.value?.categoryId ?? book.value?.CategoryID ?? null
    if (catId) {
      const res = await http.get(`/api/books?page=1&pageSize=8&tab=hot&categoryId=${catId}`)
      const arr = Array.isArray(res.data?.items) ? res.data.items : res.data ?? []
      sidebarList.value = arr
        .map((x: Record<string, any>) => normalize(x))
        .filter((x: any) => x.id && Number(x.id) !== currentId)
        .slice(0, 5)
    }
  } catch (e) {
    console.warn('load sidebar error', e)
  }

  try {
    const rr = await http.get(`/api/books/random?count=12`)
    const arr = Array.isArray(rr.data?.items) ? rr.data.items : rr.data ?? []
    related.value = shuffleArray(
      arr
        .map((x: Record<string, any>) => normalize(x))
        .filter((r: any) => r.id && Number(r.id) !== currentId)
        .slice(0, 4),
    )
  } catch (e) {
    console.warn('load random recommendations error', e)
  }
}

// === 讀取書籍資料 ===
async function fetchBook() {
  loading.value = true
  error.value = null
  rawItem.value = null
  book.value = null
  branchList.value = []

  const id = String(route.params.id ?? '')
  if (!id) {
    error.value = '無效 id'
    loading.value = false
    return
  }

  try {
    const res = await http.get(`/api/books/${id}`)
    const item = res.data
    book.value = {
      id: item.id,
      title: item.title ?? '無標題',
      author: item.author,
      price: item.salePrice ?? item.Price ?? null,
      coverUrl: item.coverUrl ?? item.imageUrl ?? null,
      description: item.description ?? item.summary ?? null,
      categoryId: item.categoryId,
    }

    await loadSidebarAndRelated()
    loading.value = false
  } catch (e: any) {
    console.error(e)
    error.value = e?.message ?? '讀取錯誤'
    loading.value = false
  }
}

// === 格式化 ===
function formatDateString(s: string | null) {
  if (!s) return null
  const d = new Date(s)
  if (isNaN(d.getTime())) return s
  return d.toLocaleDateString('zh-TW', { year: 'numeric', month: 'long', day: 'numeric' })
}

function formatMoney(v: any) {
  if (v == null) return '—'
  return Number(v).toLocaleString('zh-TW')
}

// === 載入評論（假資料永遠保留）===
async function loadReviews(bookId: number) {
  try {
    const res = await http.get(`/api/ReviewsApi/GetBookReviews/${bookId}`)
    if (Array.isArray(res.data) && res.data.length > 0) {
      // ✅ 合併假資料 + 真實資料
      reviews.value = [...fakeReviews, ...res.data]
    } else {
      // ✅ 即使沒有後端資料，假資料仍保留
      reviews.value = fakeReviews
    }
  } catch (err) {
    console.warn('⚠️ 無法載入評論，改用假資料')
    reviews.value = fakeReviews
  } finally {
    loadingReviews.value = false
  }
}

// === 新增評論時（假資料不消失）===
async function submitReview(newReview: any) {
  try {
    await http.post('/api/ReviewsApi/Create', newReview)
    reviews.value.push(newReview) // ✅ 只加新資料，不清空假資料
    alert('評論已送出！')
  } catch (err) {
    console.error('送出評論失敗', err)
  }
}

// === 生命周期 ===
onMounted(async () => {
  const bookId = Number(route.params.id)
  await fetchBook()
  await loadReviews(bookId)
})

// === 監看路由變化 ===
watch(
  () => route.params.id,
  async (newId, oldId) => {
    if (!newId || newId === oldId) return
    loading.value = true
    await fetchBook()
    await loadReviews(Number(newId))
    loading.value = false
  },
)
</script>

<template>
  <div class="container py-4">
    <div v-if="loading" class="text-center py-5">載入中...</div>
    <div v-else-if="error" class="alert alert-danger">讀取失敗：{{ error }}</div>

    <div v-else-if="book" class="row gx-4">
      <!-- 書籍內容 -->
      <div class="col-12 col-lg-8">
        <h2 class="mb-3">{{ book.title }}</h2>
        <img :src="imgSrc" class="img-fluid mb-3" alt="book cover" />
        <p v-html="book.description || '無詳細說明'"></p>

        <!-- 📚 讀者評論 -->
        <hr />
        <h5>讀者評論</h5>
        <div v-if="loadingReviews" class="p-3 text-muted">⏳ 載入評論中...</div>
        <div v-else class="accordion" id="reviewsAccordion">
          <div class="accordion-item" v-for="(r, idx) in reviews" :key="idx">
            <h2 class="accordion-header" :id="'h' + idx">
              <button
                class="accordion-button collapsed"
                type="button"
                data-bs-toggle="collapse"
                :data-bs-target="'#c' + idx"
              >
                🧑‍💬 {{ r.displayName || r.author }}　⭐ {{ r.rating }}/5
              </button>
            </h2>
            <div
              :id="'c' + idx"
              class="accordion-collapse collapse"
              :data-bs-parent="'#reviewsAccordion'"
            >
              <div class="accordion-body">
                <div class="text-muted small mb-2">
                  {{ new Date(r.createdAt).toLocaleString() }}
                </div>
                <div>{{ r.content }}</div>
              </div>
            </div>
          </div>
        </div>

        <hr />
        <h6>你可能也會喜歡</h6>
        <div class="d-flex gap-3 overflow-auto pb-3">
          <div v-for="r in related" :key="r.id" style="min-width: 140px">
            <ProductCard :book="r" />
          </div>
        </div>
      </div>

      <!-- 側邊欄 -->
      <aside class="col-12 col-lg-4">
        <div class="card mb-3">
          <div class="card-body">
            <h6 class="card-title">同類熱銷</h6>
            <ul class="list-unstyled">
              <li v-for="s in sidebarList" :key="s.id" class="mb-2">
                <router-link
                  :to="s.id ? `/books/${s.id}` : '#'"
                  class="d-flex gap-2 text-decoration-none text-reset align-items-start"
                >
                  <img
                    :src="smallCover(s)"
                    alt="封面"
                    class="small-cover"
                    style="width: 48px; height: 64px; object-fit: cover"
                  />
                  <div class="small ms-1 w-100">
                    <div class="fw-bold text-truncate" style="max-width: 160px">
                      {{ s.title }}
                    </div>
                    <div class="text-danger">NT$ {{ formatMoney(s.price) }}</div>
                  </div>
                </router-link>
              </li>
            </ul>
          </div>
        </div>
      </aside>
    </div>
  </div>
</template>

<style scoped>
.container {
  margin-top: 60px;
}
.small-cover {
  border-radius: 4px;
  transition: transform 0.15s ease;
}
.small-cover:hover {
  transform: translateY(-3px);
}
</style>
