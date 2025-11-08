<script setup lang="ts">
import { ref, onMounted, computed, nextTick } from 'vue'
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
const reviews = ref<
  {
    title: string
    author: string
    rating: number
    content: string
    createdAt: string
  }[]
>([])

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

function smallCover(b: any) {
  return b.coverUrl ?? (b.id ? `/api/BookImages/${b.id}/cover` : '/placeholder.png')
}

// 載入同類熱銷（sidebarList）與隨機推薦（related）
async function loadSidebarAndRelated() {
  try {
    // 先嘗試從 book.raw 找 category id（容錯多種命名）
    const raw = book.value?.raw ?? {}
    const catId =
      raw?.CategoryID ??
      raw?.category?.id ??
      raw?.categoryId ??
      raw?.CategoryId ??
      raw?.category?.CategoryID ??
      raw?.categoryId ??
      raw?.category?.id ??
      book.value?.categoryId ??
      null

    // 同類熱銷：若有 categoryId 則呼叫 /api/books?tab=hot&categoryId=...
    if (catId) {
      try {
        // 優先呼叫你後端示範的 API 路徑（支援不同回傳格式）
        const res =
          (await http.get(`/api/books?page=1&pageSize=5&tab=hot&categoryId=${catId}`)) ||
          (await http.get(`/api/BooksApi/List?tab=hot&categoryId=${catId}`))
        const payload = res?.data?.items ?? res?.data ?? res
        const arr = Array.isArray(payload) ? payload : (payload?.items ?? [])
        // 將欄位標準化成前端使用的欄位（id,title,coverUrl,price）
        sidebarList.value = arr.map((x: any) => ({
          id: x.id ?? x.bookId ?? x.BookID,
          title: x.title ?? x.name ?? '',
          coverUrl: x.coverUrl ?? x.imageUrl ?? x.filePath ?? x.cover ?? null,
          price: x.salePrice ?? x.SalePrice ?? x.listPrice ?? x.ListPrice ?? x.price ?? null,
        }))
      } catch (err) {
        console.warn('load sidebar error', err)
        sidebarList.value = []
      }
    } else {
      sidebarList.value = []
    }

    // 隨機推薦（你可能也會喜歡）
    try {
      // 優先嘗試 /api/books/random，若無則 fallback 到 /api/BooksApi/Random
      const rr =
        (await http.get(`/api/books/random?count=3`)) ||
        (await http.get(`/api/BooksApi/Random?count=3`))
      const payload = rr?.data?.items ?? rr?.data ?? rr
      const arr = Array.isArray(payload) ? payload : (payload?.items ?? [])
      related.value = arr.map((x: any) => ({
        id: x.id ?? x.bookId ?? x.BookID,
        title: x.title ?? x.name ?? '',
        coverUrl: x.coverUrl ?? x.imageUrl ?? x.filePath ?? x.cover ?? null,
        price: x.salePrice ?? x.SalePrice ?? x.listPrice ?? x.ListPrice ?? x.price ?? null,
      }))
    } catch (err) {
      console.warn('load random recommendations error', err)
      related.value = []
    }
  } catch (e) {
    console.error('loadSidebarAndRelated error', e)
    sidebarList.value = []
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
    reviews.value = Array.isArray(res.data) && res.data.length > 0 ? res.data : fakeReviews
  } catch (err) {
    console.warn('⚠️ 無法載入評論，改用假資料')
    reviews.value = fakeReviews
  } finally {
    loadingReviews.value = false
  }
})

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
            <small class="text-muted d-block mt-2">封面示意圖</small>
          </div>

          <!-- Title / Meta / Price / Actions -->
          <div class="col-12 col-md-7">
            <h1 class="h4 mb-1">{{ book.title }}</h1>
            <hr />
            <div class="text-muted mb-2">
              <span>作者：{{ book.authors ? book.authors.join('、') : (book.author ?? '-') }}</span>
              <br />
              <hr />
              <span>ISBN：{{ book.isbn ?? '-' }}</span>
            </div>
            <hr />
            <div class="mb-3">
              <div class="h4 text-danger">NT$ {{ formatMoney(book.price) }}</div>
              <div class="small text-muted" v-if="book.raw?.listPrice">
                建議售價：NT$ {{ formatMoney(book.raw.listPrice) }}
              </div>
            </div>
            <br />
            <br />
            <br />
            <hr />
            <div class="d-flex align-items-center gap-2 mb-3">
              <!-- type="button" + disabled 綁定 adding，onBuyNow 會等待加入購物車成功才跳轉 -->
              <button
                type="button"
                class="btn btn-primary btn-lg"
                @click="onBuyNow"
                :disabled="adding"
              >
                立即購買
              </button>

              <!-- type="button" + disabled 綁定 adding，點擊會呼 onAddToCart -->
              <button
                type="button"
                class="btn btn-outline-primary btn-lg"
                @click="onAddToCart"
                :disabled="adding"
              >
                {{ adding ? '處理中...' : '加入購物車' }}
              </button>

              <button type="button" class="btn btn-light" @click="onFav">
                <i class="bi bi-heart"></i> 收藏
              </button>
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
                  <div class="small text-muted">
                    可用總數：<strong>{{ totalAvailable }}</strong>
                  </div>
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
        <div class="d-flex gap-3 overflow-auto pb-3">
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
                <li v-for="s in sidebarList" :key="s.id" class="d-flex gap-2 mb-2">
                  <img :src="smallCover(s)" style="width: 48px; height: 64px; object-fit: cover" />
                  <div class="small">
                    <div class="fw-bold text-truncate" style="max-width: 160px">{{ s.title }}</div>
                    <div class="text-danger">NT$ {{ formatMoney(s.price) }}</div>
                  </div>
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

/* small screen 微調 */
@media (max-width: 576px) {
  .branch-panel .card-header {
    font-size: 0.95rem;
  }
  .branch-panel .badge {
    font-size: 0.8rem;
  }
}
</style>
