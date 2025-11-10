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

// 🧩 預設假評論資料（後端無資料時使用）
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

// 模擬讀者評價與相關推薦 TODO: 改為 API 請求
const reviews = ref<{ 
  title: string
  author: string
  rating: number
  content: string
  createdAt: string
  displayName?: string
}[]>([])

const loadingReviews = ref(true)
const related = ref<any[]>([])
const sidebarList = ref<any[]>([]) // 用於顯示「同類熱銷」的書籍列表（related）
const branchList = ref<any[]>([]) // branchList 專門存各據點的庫存資料（避免混用 sidebarList）

const totalAvailable = computed(() => {
  return (branchList.value ?? []).reduce((s: number, b: any) => s + Number(b.available ?? 0), 0)
})
// 圖片
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

// 新增：計算後端或據點回傳的總庫存（數字或 null）
const totalStock = computed(() => {
  const s = book.value?.stock ?? totalAvailable.value
  if (s === null || s === undefined) return null
  return Number(s)
})

// 顯示字串（數字 >0 顯示數字，0 或負數顯示「缺貨」，null 顯示「—」）
const stockDisplay = computed(() => {
  if (totalStock.value === null) return '—'
  return totalStock.value > 0 ? String(totalStock.value) : '缺貨'
})

function smallCover(b: any) {
  return b.coverUrl ?? (b.id ? `/api/BookImages/${b.id}/cover` : '/placeholder.png')
}

// ===== types & helpers =====
interface BookCard {
  id: number | string | null
  slug?: string | null
  title: string
  coverUrl?: string | null
  price?: number | null
  raw?: Record<string, any>
}

function shuffleArray<T>(arr: T[]): T[] {
  for (let i = arr.length - 1; i > 0; i--) {
    const j = Math.floor(Math.random() * (i + 1))
    const tmp = arr[i]
    arr[i] = arr[j]
    arr[j] = tmp
  }
  return arr
}

// 載入同類熱銷（sidebarList）與隨機推薦（related）
async function loadSidebarAndRelated(): Promise<void> {
  const currentId = Number(book.value?.id ?? 0)

  const normalize = (x: Record<string, any>): BookCard => {
    const id = x?.id ?? x?.bookId ?? x?.BookID ?? null
    const slug = x?.slug ?? x?.Slug ?? x?.bookSlug ?? null
    const title = x?.title ?? x?.name ?? ''
    const coverUrl = x?.coverUrl ?? x?.imageUrl ?? x?.filePath ?? x?.cover ?? null
    const price =
      x?.salePrice ??
      x?.SalePrice ??
      x?.listPrice ??
      x?.ListPrice ??
      x?.price ??
      (typeof x?.price === 'string' ? Number(x.price) : null) ??
      null
    return { id, slug, title, coverUrl, price, raw: x }
  }

  // 1) 同類熱銷（sidebarList）
  sidebarList.value = []
  try {
    const raw = book.value?.raw ?? {}
    const catId =
      raw?.CategoryID ??
      raw?.category?.id ??
      raw?.categoryId ??
      raw?.CategoryId ??
      book.value?.categoryId ??
      book.value?.CategoryID ??
      null

    if (catId) {
      let res: any
      try {
        res = await http.get(`/api/books?page=1&pageSize=8&tab=hot&categoryId=${catId}`)
      } catch {
        res = await http.get(`/api/BooksApi/List?tab=hot&categoryId=${catId}&page=1&pageSize=8`)
      }
      const payload = res?.data?.items ?? res?.data ?? res
      const arr = Array.isArray(payload) ? payload : (payload?.items ?? [])
      const mapped = arr
        .map((x: Record<string, any>) => normalize(x))
        .filter((x: any) => x.id && Number(x.id) !== currentId)

      // 去重並取前 5
      const seen = new Set<number | string>()
      const uniq: BookCard[] = []
      for (const it of mapped) {
        const key = it.id ?? ''
        if (!seen.has(key)) {
          seen.add(key)
          uniq.push(it)
        }
        if (uniq.length >= 5) break
      }
      sidebarList.value = uniq
    }
  } catch (e) {
    console.warn('load sidebar error', e)
    sidebarList.value = []
  }

  // 2) 隨機推薦（related）
  related.value = []
  try {
    let rr: any
    try {
      rr = await http.get(`/api/books/random?count=12`)
    } catch {
      rr = await http.get(`/api/BooksApi/Random?count=12`)
    }
    const payload = rr?.data?.items ?? rr?.data ?? rr
    const arr = Array.isArray(payload) ? payload : (payload?.items ?? [])
    let mapped = arr
      .map((m: Record<string, any>) => normalize(m))
      .filter((r: any) => r.id && Number(r.id) !== currentId)

    // 排除已出現在 sidebar 的 id
    const sidebarIds = new Set(sidebarList.value.map((s) => s.id))
    mapped = mapped.filter((m: any) => !sidebarIds.has(m.id))

    // shuffle + 取 4
    mapped = shuffleArray(mapped)
    related.value = mapped.slice(0, 4)
  } catch (e) {
    console.warn('load random recommendations error', e)
    related.value = []
  }
}

async function fetchBook() {
  loading.value = true
  error.value = null
  rawItem.value = null
  book.value = null
  branchList.value = [] // 先清空據點清單

  const id = String(route.params.id ?? '')
  if (!id) {
    error.value = '無效 id'
    loading.value = false
    return
  }

  try {
    let res: any
    try {
      res = await http.get(`/api/books/${id}`)
    } catch (err) {
      // fallback
      res = await http.get(`/api/BooksApi/GetById?id=${id}`)
    }

    rawItem.value = res.data ?? res
    const payload = rawItem.value?.data ?? rawItem.value

    // 格式化(範例)
    const item = Array.isArray(payload) ? payload[0] : payload
    // mapping
    book.value = {
      id: item.id ?? item.bookId ?? id,
      title: item.title ?? item.bookTitle ?? '無標題',
      author: null,
      isbn: item.isbn ?? item.ISBN ?? null,
      price: item.salePrice ?? item.Price ?? null,
      coverUrl: item.coverUrl ?? item.imageUrl ?? null,
      description: item.description ?? item.summary ?? null,
      raw: item,
    }

    // 格式化publisherName欄位
    book.value.publisherName =
      payload?.publisher?.name ??
      item.publisher?.name ??
      item.publisherName ??
      item.PublisherName ??
      (typeof item.publisher === 'string' ? item.publisher : null)

    // 格式化publishDate欄位（可能為 null），之後用 formatDateString 處理
    book.value.publishDateNorm =
      payload?.publishDate ?? item.publishDate ?? item.publishedDate ?? item.PublishDate ?? null

    // authors 支援各種型別（陣列/物件/字串） ===
    if (Array.isArray(payload?.authors) && payload.authors.length) {
      const names = payload.authors.map((a: any) => a.name ?? a.authorName ?? a).filter(Boolean)
      book.value.authors = names
      book.value.author = names.join('、')
    } else if (Array.isArray(item?.authors) && item.authors.length) {
      const names = item.authors
        .map((a: any) => (typeof a === 'string' ? a : (a.name ?? a.authorName ?? a)))
        .filter(Boolean)
      book.value.authors = names
      book.value.author = names.join('、')
    } else if (item.author && typeof item.author === 'object') {
      const n = item.author.name ?? item.author.authorName ?? null
      book.value.authors = n ? [n] : null
      book.value.author = n
    } else if (typeof item.author === 'string' && item.author.trim().length) {
      book.value.authors = [item.author]
      book.value.author = item.author
    } else {
      book.value.authors = null
      book.value.author = null
    }

    // 格式化常見欄位 ( pages / language / categoryName )
    book.value.pages = item.pages ?? item.pageCount ?? item.Pages ?? null
    book.value.language = item.language ?? item.Language ?? item.LanguageCode ?? null
    book.value.categoryName = item.category?.name ?? item.categoryName ?? item.CategoryName ?? null

    // (沒傳 salePrice 就顯示 listPrice)
    if (!book.value.price) {
      book.value.price =
        item.salePrice ?? item.SalePrice ?? item.listPrice ?? item.ListPrice ?? null
    }

    // inventory parsing（支援 payload.inventory 或 item.inventory） ===
    const inv = payload?.inventory ?? item?.inventory ?? null
    if (inv) {
      book.value.inventory = inv

      // 格式化inv.byBranch 欄位
      const rawBranches =
        inv.byBranch ??
        inv.by_branch ??
        inv.branches ??
        item.inventory?.byBranch ??
        item.inventory?.branches ??
        null

      branchList.value = Array.isArray(rawBranches)
        ? rawBranches.map((b: any) => {
            const onHand = Number(b.onHand ?? b.OnHand ?? b.Onhand ?? 0)
            const reserved = Number(b.reserved ?? b.Reserved ?? 0)
            const available = Number(b.available ?? b.Available ?? onHand - reserved)
            return {
              branchId: b.branchId ?? b.BranchID ?? b.BranchId ?? b.branchID ?? null,
              branchName:
                b.branchName ??
                b.BranchName ??
                b.branch ??
                b.Branch ??
                '據點 ' + (b.branchId ?? b.BranchID ?? ''),
              onHand,
              reserved,
              available,
              updatedAt: b.updatedAt ?? b.UpdatedAt ?? null,
            }
          })
        : []

      // 計算總可用庫存
      if (typeof inv.total === 'number') {
        book.value.stock = inv.total
      } else if (branchList.value.length) {
        book.value.stock = branchList.value.reduce((s: number, b: any) => s + (b.available ?? 0), 0)
      } else {
        book.value.stock = item.stock ?? item.Stock ?? null
      }
    } else {
      book.value.inventory = null
      branchList.value = []
      book.value.stock = item.stock ?? item.Stock ?? null
    }

    // related books（保持）
    await loadSidebarAndRelated()
    loading.value = false

    reviews.value = item.reviews ?? [
      { title: '好書推薦', author: '小明', rating: 5, content: '很實用的書，範例詳細。' },
    ]

    loading.value = false
  } catch (e: any) {
    console.error(e)
    error.value = e?.message ?? '讀取錯誤'
    loading.value = false
  }
}

/* ===========================
   格式化工具（template 可直呼）
   =========================== */
// 日期格式化（可為 ISO/日期字串）
function formatDateString(s: string | null) {
  if (!s) return null
  // 嘗試解析；若失敗就直接回原字串
  const d = new Date(s)
  if (isNaN(d.getTime())) return s
  return d.toLocaleDateString('zh-TW', { year: 'numeric', month: 'long', day: 'numeric' })
}

// 金額格式化
function formatMoney(v: any) {
  if (v == null) return '—'
  return Number(v).toLocaleString('zh-TW')
}

// 生命週期：先 load book，再 load reviews
onMounted(async () => {
  const bookId = Number(route.params.id)

  // ✅ 先載入書籍資料
  await fetchBook()

  // ✅ 再載入評論
   try {
  const res = await http.get(`/api/ReviewsApi/GetBookReviews/${bookId}`)
  console.log("📢 後端回傳的 reviews：", res.data) // ✅ 先看這裡有沒有 displayName
  reviews.value = Array.isArray(res.data) && res.data.length > 0
    ? res.data
    : fakeReviews
} catch (err) {
  console.warn('⚠️ 無法載入評論，改用假資料')
  reviews.value = fakeReviews
} finally {
  loadingReviews.value = false
}

})

// 當路由 id 變動時重新載入本頁資料（不重建元件）
watch(
  () => route.params.id,
  async (newId, oldId) => {
    // 若沒有 newId 或沒有變動就不用處理
    if (!newId || String(newId) === String(oldId)) return

    // 重設 UI 狀態
    loading.value = true
    loadingReviews.value = true
    error.value = null

    try {
      // 重新抓書籍與 sidebar/related
      await fetchBook()

      // 重新抓評論（fetchBook 已會呼 loadSidebarAndRelated；評論獨立處理）
      const bookId = Number(newId)
      try {
        const res = await http.get(`/api/ReviewsApi/GetBookReviews/${bookId}`)
        reviews.value = Array.isArray(res.data) && res.data.length > 0 ? res.data : fakeReviews
      } catch {
        reviews.value = fakeReviews
      } finally {
        loadingReviews.value = false
      }

      // 使用者體驗：換頁後滾回頂端（可選）
      window.scrollTo({ top: 0, behavior: 'smooth' })
    } catch (e: any) {
      console.error('watch route id error', e)
      error.value = e?.message ?? '讀取錯誤'
    } finally {
      loading.value = false
    }
  },
)
async function resolveMemberId(): Promise<number | null> {
  try {
    const m = (auth as any).member
    const tryIds = [m?.MemberID, m?.memberId, m?.id]
    for (const v of tryIds) if (v) return Number(v)

    // 若 store 沒有時可以呼叫 /api/auth/me
    const r = await http.get('/api/auth/me')
    const data = r?.data ?? r
    const candidate = data?.memberId ?? data?.MemberID ?? data?.id ?? data?.userId
    if (candidate) return Number(candidate)
  } catch {
    // ignore - 回 null 由呼叫處處理
  }
  return null
}

async function onAddToCart(e?: Event) {
  e?.stopPropagation()
  if (!book.value) return
  if (adding.value) return
  adding.value = true

  const id = Number(book.value.id ?? book.value.bookId ?? book.value.BookID)
  if (!id) {
    alert('找不到 book id')
    adding.value = false
    return
  }

  const memberId = await resolveMemberId()
  if (!memberId) {
    alert('請先登入')
    adding.value = false
    return
  }

  const unitPrice = (book.value as any)?.price ?? (book.value as any)?.salePrice ?? 0
  const payload = {
    MemberID: memberId,
    BookID: id,
    Quantity: 1,
    UnitPrice: unitPrice,
  }

  try {
    // 與你後端 ShoppingCartController 對應的 endpoint
    const res = await http.post('/api/ShoppingCart/add', payload)
    const data = res?.data ?? res
    if (data && (data.success === true || res.status === 200 || res.status === 201)) {
      // 成功：顯示提示（你可改成 toast / 更新 cart store）
      alert(data.message ?? '已加入購物車')
      // 若後端有回 newCount，可在這裡更新全域購物車數字
      // EX: cartStore.count = data.newCount
      adding.value = false
      return true
    } else {
      alert(data?.message ?? '加入購物車失敗')
      adding.value = false
      return false
    }
  } catch (err: any) {
    console.error('onAddToCart error', err)
    const msg = err?.response?.data?.message ?? err?.message ?? '網路錯誤'
    alert('加入購物車失敗：' + msg)
    adding.value = false
    return false
  }
}

async function onBuyNow() {
  // 立即購買：先加入購物車，成功後才跳轉到購物車或結帳頁 TODO: 改為結帳頁
  const ok = await onAddToCart()
  if (ok) {
    router.push('/cart')
  }
}

function onFav() {
  alert('加入收藏（示範）')
}

function onImgError(e: Event) {
  const img = (e.currentTarget ?? e.target) as HTMLImageElement | null
  if (!img) return
  if (!img.dataset.errored) {
    img.dataset.errored = '1'
    img.src = '/placeholder.png'
  }
}

function emitAdd(b: any) {
  /* ProductCard 的 add event handler 若使用 */
}
</script>

<template>
  <div class="container py-4">
    <nav aria-label="breadcrumb" class="mb-3">
      <ol class="breadcrumb">
        <li class="breadcrumb-item"><router-link to="/">首頁</router-link></li>
        <li class="breadcrumb-item"><router-link to="/">書籍</router-link></li>
        <li class="breadcrumb-item active" aria-current="page">{{ book?.title ?? '書籍' }}</li>
      </ol>
    </nav>

    <div v-if="loading" class="text-center py-5">載入中...</div>
    <div v-else-if="error" class="alert alert-danger">讀取失敗：{{ error }}</div>

    <div v-else-if="book" class="row gx-4">
      <!-- 左側主內容 -->
      <div class="col-12 col-lg-8">
        <div class="row">
          <!-- Large cover -->
          <div class="col-12 col-md-5 text-center mb-3">
            <div class="card border-0 shadow-sm" style="max-width: 360px; margin: 0 auto">
              <img :src="imgSrc" :alt="book.title" class="img-fluid" @error="onImgError" />
            </div>
            <small class="text-muted d-block mt-2">書籍封面示意圖</small>
          </div>

          <!-- Title / Meta / Price / Actions -->
          <div class="col-12 col-md-7">
            <div class="d-flex flex-column h-100 justify-content-between">
              <!-- 上：Title -->
              <div>
                <h1 class="h5 mb-2">{{ book.title }}</h1>
              </div>
              <br />
              <!-- 中：垂直 Meta（作者 / ISBN / 總庫存）-->
              <div class="meta-vertical mb-3">
                <div class="meta-item">
                  <div class="meta-label">作者　：</div>
                  <div class="meta-value">
                    {{ book.authors ? book.authors.join('、') : (book.author ?? '-') }}
                  </div>
                </div>
                <hr />
                <div class="meta-item">
                  <div class="meta-label">ISBN　：</div>
                  <div class="meta-value">{{ book.isbn ?? '-' }}</div>
                </div>
                <hr />
                <div class="meta-item">
                  <div class="meta-label">總庫存　：</div>
                  <div
                    class="meta-value"
                    :class="{ 'text-danger fw-bold': totalStock !== null && totalStock <= 0 }"
                  >
                    {{ stockDisplay }}
                  </div>
                </div>
                <hr />
              </div>

              <!-- 右側價格區（垂直顯示，靠右） -->
              <div class="price-and-actions d-flex flex-column align-items-end mb-3">
                <div class="price-main">
                  NT$ <span class="price-num">{{ formatMoney(book.price) }}</span>
                </div>
                <div class="price-sub small text-muted" v-if="book.raw?.listPrice">
                  建議售價：NT$ {{ formatMoney(book.raw.listPrice) }}
                </div>

                <!-- 下：按鈕（靠右顯示） -->
                <div class="mt-3 d-flex gap-2">
                  <button
                    type="button"
                    class="btn btn-primary btn-md"
                    @click="onBuyNow"
                    :disabled="adding"
                  >
                    立即購買
                  </button>

                  <button
                    type="button"
                    class="btn btn-outline-primary btn-md"
                    @click="onAddToCart"
                    :disabled="adding"
                  >
                    {{ adding ? '處理中...' : '加入購物車' }}
                  </button>

                  <button type="button" class="btn btn-light btn-md" @click="onFav">收藏</button>
                </div>
              </div>
            </div>
          </div>
        </div>

        <hr />

        <div class="d-flex justify-content-between align-items-center mb-2">
          <!-- nav tabs 與據點按鈕 -->
          <ul class="nav nav-tabs mb-0" role="tablist">
            <li class="nav-item" role="presentation">
              <button
                class="nav-link active"
                data-bs-toggle="tab"
                data-bs-target="#tab-desc"
                type="button"
                role="tab"
              >
                內容簡介
              </button>
            </li>

            <li class="nav-item" role="presentation">
              <button
                class="nav-link"
                data-bs-toggle="tab"
                data-bs-target="#tab-spec"
                type="button"
                role="tab"
              >
                規格
              </button>
            </li>

            <li class="nav-item" role="presentation">
              <button
                class="nav-link"
                :class="{ disabled: totalAvailable <= 0 }"
                :tabindex="totalAvailable <= 0 ? -1 : 0"
                :aria-disabled="totalAvailable <= 0"
                data-bs-toggle="tab"
                data-bs-target="#tab-branches"
                type="button"
                role="tab"
                title="查看各分店庫存"
              >
                據點總庫存
                <span v-if="totalAvailable > 0" class="badge bg-secondary ms-2">{{
                  totalAvailable
                }}</span>
              </button>
            </li>

            <li class="nav-item" role="presentation">
              <button
                class="nav-link"
                data-bs-toggle="tab"
                data-bs-target="#tab-reviews"
                type="button"
                role="tab"
              >
                讀者評價
              </button>
            </li>
          </ul>
        </div>

        <div class="tab-content">
          <div class="tab-pane fade show active" id="tab-desc" role="tabpanel">
            <div v-if="isDescriptionString" v-html="book.description"></div>
            <div v-else-if="descriptionExtracted" v-html="descriptionExtracted"></div>
            <div v-else class="p-3 bg-light rounded">無詳細說明</div>
          </div>

          <div class="tab-pane fade" id="tab-spec" role="tabpanel">
            <table class="table table-sm">
              <tbody>
                <tr>
                  <th class="w-25">出版社</th>
                  <td>{{ book.publisherName ?? '-' }}</td>
                </tr>
                <tr>
                  <th>出版日期</th>
                  <td>{{ book.publishDateNorm ? formatDateString(book.publishDateNorm) : '-' }}</td>
                </tr>
                <tr>
                  <th>作者</th>
                  <td>{{ book.authors ? book.authors.join('、') : (book.author ?? '-') }}</td>
                </tr>
                <!-- <tr>
                  <th>頁數</th>
                  <td>{{ book.pages ?? '-' }}</td>
                </tr> -->
                <!-- <tr>
                  <th>語言</th>
                  <td>{{ book.language ?? '-' }}</td>
                </tr> -->
                <tr>
                  <th>分類</th>
                  <td>{{ book.categoryName ?? '-' }}</td>
                </tr>
              </tbody>
            </table>
          </div>

          <div class="tab-pane fade" id="tab-reviews" role="tabpanel">
  <div v-if="loadingReviews" class="p-3 text-muted">⏳ 載入評論中...</div>

  <!-- 範例 Accordion -->
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
          <div class="text-muted small mb-2">{{ new Date(r.createdAt).toLocaleString() }}</div>
          <div>{{ r.content }}</div>
        </div>
      </div>
    </div>

            <!-- 範例 Accordion -->
            <div v-else class="accordion" id="reviewsAccordion">
              <div class="accordion-item" v-for="(r, idx) in reviews" :key="idx">
                <h2 class="accordion-header" :id="'h' + idx">
                  <button
                    class="accordion-button collapsed"
                    type="button"
                    data-bs-toggle="collapse"
                    :data-bs-target="'#c' + idx"
                  >
                    🧑‍💬 {{ r.title }}　⭐ {{ r.rating }}/5
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

              <div v-if="!reviews.length" class="p-3 text-muted">目前尚無評價</div>
            </div>
          </div>

          <div class="tab-pane fade" id="tab-branches" role="tabpanel">
            <div class="mt-3 branch-panel">
              <div class="card">
                <div class="card-header d-flex justify-content-between align-items-center">
                  <div>據點庫存明細</div>
                  <!-- <div class="small text-muted">
                    庫存總數：<strong>{{ totalAvailable }}</strong>
                  </div> -->
                </div>
                <div class="card-body p-0">
                  <ul class="list-group list-group-flush">
                    <li
                      v-for="(b, idx) in branchList"
                      :key="b.branchId ?? idx"
                      class="list-group-item d-flex justify-content-between align-items-start"
                    >
                      <div>
                        <div class="fw-bold">{{ b.branchName }}</div>
                        <div class="small text-muted">
                          更新：{{ b.updatedAt ? formatDateString(b.updatedAt) : '-' }}
                        </div>
                      </div>
                      <div class="text-end">
                        <div class="small text-muted">可用</div>
                        <div class="badge rounded-pill bg-primary mt-1">
                          {{ b.available ?? 0 }}
                        </div>
                        <div class="small text-muted mt-1">
                          總 {{ b.onHand ?? 0 }} / 預留 {{ b.reserved ?? 0 }}
                        </div>
                      </div>
                    </li>
                    <li v-if="!branchList.length" class="list-group-item text-muted">
                      目前沒有據點庫存資料。
                    </li>
                  </ul>
                </div>
              </div>
            </div>
          </div>
        </div>
        <hr />

        <!-- 相關推薦（carousel 或橫列） -->
        <h5 class="mb-3">你可能也會喜歡</h5>
        <div class="related-listings d-flex gap-3 overflow-auto pb-3">
          <div v-for="r in related" :key="r.id" style="min-width: 140px">
            <ProductCard :book="r" @add="emitAdd" />
          </div>
        </div>
      </div>

      <!-- 右側側邊欄（sticky） -->
      <aside class="col-12 col-lg-4">
        <div class="position-sticky" style="top: 80px">
          <div class="card mb-3 shadow-sm">
            <div class="card-body">
              <h6 class="card-title">活動與優惠</h6>
              <p class="small text-muted">使用 VIP 折扣或輸入優惠碼可享折扣。</p>
              <button type="button" class="btn btn-outline-secondary w-100 mb-2">查看優惠</button>
              <!-- 按鈕改為 type="button" 並呼叫 onAddToCart 同一函式 -->
              <!-- <button
                type="button"
                class="btn btn-outline-primary w-100"
                @click="onAddToCart"
                :disabled="adding"
              >
                {{ adding ? '處理中...' : '加入購物車' }}
              </button> -->
            </div>
          </div>

          <div class="card shadow-sm">
            <div class="card-body">
              <h6 class="card-title">同類熱銷</h6>
              <ul class="list-unstyled">
                <li v-for="s in sidebarList" :key="s.id" class="mb-2">
                  <router-link
                    :to="s.id ? `/books/${s.id}` : '#'"
                    class="d-flex gap-2 text-decoration-none text-reset align-items-start"
                    aria-label="前往書籍詳情"
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
        </div>
      </aside>
    </div>
  </div>
</template>

<style scoped>
/* 微調：讓右側 sticky 區在大畫面時固定 */
@media (min-width: 992px) {
  aside .position-sticky {
    top: 80px;
  }
}
img.img-fluid {
  display: block;
  max-width: 100%;
  height: auto;
}
.d-flex.overflow-auto {
  -webkit-overflow-scrolling: touch;
}
.container {
  margin-top: 60px;
}

/* === ADDED: inline branch panel 樣式，與 reviews 區塊風格一致 === */
.branch-panel .card {
  border-radius: 8px;
  box-shadow: none;
  border: 1px solid #e9ecef;
}
.branch-panel .card-header {
  background: #fff;
  border-bottom: 1px solid #eee;
  padding: 0.5rem 1rem;
}
.branch-panel .list-group-item {
  border: none;
  border-bottom: 1px dashed #eee;
  padding: 0.75rem 1rem;
}
.branch-panel .badge {
  font-size: 0.85rem;
  padding: 0.45rem 0.6rem;
}
/* meta vertical styling */
.meta-vertical {
  display: flex;
  flex-direction: column;
  gap: 0.55rem;
}

.meta-item {
  display: flex;
  gap: 0.8rem;
  align-items: flex-start;
}
.meta-value {
  font-size: 1.25rem;
  color: #222;
  line-height: 1.25;
}
.meta-label {
  font-size: 0.98rem;
  color: #6c757d;
  min-width: 80px;
}

/* 價格樣式（醒目） */
.price-and-actions {
  min-height: 110px;
} /* 保持價格區高度 */
.price-main {
  color: #dc3545;
  font-weight: 800;
  font-size: 1.6rem;
}
.price-num {
  font-size: 1.9rem;
}

/* 按鈕群微調 */
.price-and-actions .btn {
  padding: 0.45rem 0.85rem;
  font-size: 0.95rem;
}
/* small screen 微調 */
@media (max-width: 576px) {
  .branch-panel .card-header {
    font-size: 0.95rem;
  }
  .branch-panel .badge {
    font-size: 0.8rem;
  }
}
/* meta list styling */
.meta-list {
  margin: 0;
}
.meta-row {
  display: flex;
  gap: 0.5rem;
  align-items: baseline;
  padding: 0.18rem 0;
}
.meta-row dt {
  width: 4.6rem;
  font-weight: 600;
  color: #495057;
  font-size: 0.92rem;
}
.meta-row dd {
  margin: 0;
  color: #6c757d;
  font-size: 0.92rem;
  word-break: break-word;
}

/* price block */
.price-box {
  display: inline-block;
  text-align: right;
}
.price-main {
  font-size: 1.25rem;
  font-weight: 700;
  color: #d6333f;
}
.price-sub {
  margin-top: 0.18rem;
  font-size: 0.85rem;
}

/* 調整封面寬度，縮小左右空白 */
.col-md-5 {
  max-width: 320px;
}

/* 按鈕大小及間距收斂 */
.btn-md {
  padding: 0.45rem 0.8rem;
  font-size: 0.95rem;
}

/* 減少 tab 與內容間距 */
.tab-content {
  margin-top: 0.6rem;
}
.col-12.col-md-7 h1 {
  font-size: 3rem; /* 書名變大 */
  font-weight: 700;
  margin-bottom: 0.4rem;
}
.small-cover {
  cursor: pointer;
  transition:
    transform 0.12s ease,
    box-shadow 0.12s ease;
  border-radius: 4px;
}
.small-cover:hover {
  transform: translateY(-3px);
  box-shadow: 0 6px 14px rgba(0, 0, 0, 0.08);
}

/* 讓 router-link 在列表中填滿，避免內部文字也被截斷 */
.list-unstyled > li > a {
  display: flex;
  align-items: flex-start;
  gap: 0.5rem;
  padding: 0.15rem 0;
}
/* 只針對詳情頁裡的 ProductCard 做覆寫，避免全站影響 */
.related-listings :deep(.card .actions) {
  display: flex;
  align-items: left !important;
}

/* 調整按鈕讓它們不會被壓成兩行 */
.related-listings :deep(.card .actions button) {
  white-space: nowrap !important;
  min-width: 60px; /* 視情況調大或調小 */
  padding: 10px 4px;
  font-size: 0.7rem;
  justify-content: center;
}

/* 小螢幕微調：把價格放到同欄（避免太擠） */
@media (max-width: 767.98px) {
  .col-4.text-end {
    text-align: left !important;
    margin-top: 0.35rem;
  }
  .price-main {
    font-size: 1.05rem;
  }
  .container {
    margin-top: 20px;
  }
}
</style>
