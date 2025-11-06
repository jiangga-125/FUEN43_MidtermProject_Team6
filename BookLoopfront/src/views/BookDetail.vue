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
            <div class="text-muted mb-2">
              <span>作者：{{ book.author ?? '-' }}</span>
              <span class="mx-2">|</span>
              <span>ISBN：{{ book.isbn ?? '-' }}</span>
            </div>

            <div class="mb-3">
              <div class="h4 text-danger">NT$ {{ book.price ?? '—' }}</div>
              <div class="small text-muted" v-if="book.raw?.listPrice">
                建議售價：NT$ {{ book.raw.listPrice }}
              </div>
            </div>

            <div class="d-flex align-items-center gap-2 mb-3">
              <button class="btn btn-primary btn-lg" @click="onBuyNow">立即購買</button>
              <button class="btn btn-outline-primary btn-lg" @click="onAddToCart">
                加入購物車
              </button>
              <button class="btn btn-light" @click="onFav"><i class="bi bi-heart"></i> 收藏</button>
            </div>

            <ul class="list-inline small text-muted">
              <li class="list-inline-item">運送：24 小時內出貨</li>
              <li class="list-inline-item">│</li>
              <li class="list-inline-item">庫存：{{ book.raw?.stock ?? '充足' }}</li>
            </ul>
          </div>
        </div>

        <hr />

        <ul class="nav nav-tabs mb-3" role="tablist">
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
              data-bs-toggle="tab"
              data-bs-target="#tab-reviews"
              type="button"
              role="tab"
            >
              讀者評價
            </button>
          </li>
        </ul>

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
                  <td>{{ book.raw?.publisher ?? '-' }}</td>
                </tr>
                <tr>
                  <th>出版日期</th>
                  <td>{{ book.raw?.publishedDate ?? '-' }}</td>
                </tr>
                <tr>
                  <th>頁數</th>
                  <td>{{ book.raw?.pages ?? '-' }}</td>
                </tr>
                <tr>
                  <th>語言</th>
                  <td>{{ book.raw?.language ?? '-' }}</td>
                </tr>
              </tbody>
            </table>
          </div>

          <div class="tab-pane fade" id="tab-reviews" role="tabpanel">
            <!-- 範例 Accordion -->
            <div class="accordion" id="reviewsAccordion">
              <div class="accordion-item" v-for="(r, idx) in reviews" :key="idx">
                <h2 class="accordion-header" :id="'h' + idx">
                  <button
                    class="accordion-button collapsed"
                    type="button"
                    data-bs-toggle="collapse"
                    :data-bs-target="'#c' + idx"
                  >
                    {{ r.title }} — {{ r.author }}
                  </button>
                </h2>
                <div
                  :id="'c' + idx"
                  class="accordion-collapse collapse"
                  :data-bs-parent="'#reviewsAccordion'"
                >
                  <div class="accordion-body">
                    <div class="small text-muted mb-2">評分：{{ r.rating }}/5</div>
                    <div>{{ r.content }}</div>
                  </div>
                </div>
              </div>
              <div v-if="reviews.length === 0" class="p-3 text-muted">目前尚無評價</div>
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
              <button class="btn btn-outline-secondary w-100 mb-2">查看優惠</button>
              <button class="btn btn-outline-primary w-100" @click="onAddToCart">加入購物車</button>
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
                    <div class="text-danger">NT$ {{ s.price }}</div>
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

<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import http from '@/lib/http'
import ProductCard from '@/components/ProductCard.vue' // 你已經有的卡片

const route = useRoute()
const router = useRouter()

const loading = ref(true)
const error = ref<string | null>(null)
const rawItem = ref<any | null>(null)
const book = ref<any | null>(null)

// 模擬讀者評價與相關推薦（你可改為 API 請求）
const reviews = ref<any[]>([])
const related = ref<any[]>([])
const sidebarList = ref<any[]>([])

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

async function fetchBook() {
  loading.value = true
  error.value = null
  rawItem.value = null
  book.value = null

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

    // normalize（簡單範例）
    const item = Array.isArray(payload) ? payload[0] : payload
    book.value = {
      id: item.id ?? item.bookId ?? id,
      title: item.title ?? item.bookTitle ?? '無標題',
      author: item.author ?? item.authors?.join?.(', ') ?? null,
      isbn: item.isbn ?? item.ISBN ?? null,
      price: item.salePrice ?? item.Price ?? null,
      coverUrl: item.coverUrl ?? item.imageUrl ?? null,
      description: item.description ?? item.summary ?? null,
      raw: item,
    }

    // 範例：載入相關商品（改成真實 API）
    try {
      const rel = await http.get(`/api/BooksApi/Related/${book.value.id}`)
      related.value = (rel.data ?? []).slice(0, 8)
    } catch (__) {
      // ADDED: 若沒有相關 endpoint，就用空陣列（避免拋錯）
      related.value = []
    }

    // 假資料：sidebarList / reviews
    sidebarList.value = related.value.slice(0, 5)
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

onMounted(() => {
  fetchBook()
})

// actions
function onAddToCart() {
  if (!book.value) return
  // 你們已有購物車功能：直接呼叫 API 或 emit
  http
    .post('/api/Cart/Add', { bookId: book.value.id, qty: 1 })
    .then(() => alert('已加入購物車'))
    .catch(() => alert('加入失敗'))
}

function onBuyNow() {
  // 立即購買流程（示範）
  onAddToCart()
  router.push('/cart') // 或跳到結帳
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

<style scoped>
/* 微調：讓右側 sticky 區在大畫面時固定 */
@media (min-width: 992px) {
  aside .position-sticky {
    top: 80px;
  }
}
/* 讓左右區域有合理間距 */
img.img-fluid {
  display: block;
  max-width: 100%;
  height: auto;
}
/* horizontal related overflow 樣式 */
.d-flex.overflow-auto {
  -webkit-overflow-scrolling: touch;
}
</style>
