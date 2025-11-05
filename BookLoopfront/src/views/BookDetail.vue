<template>
  <div class="container py-4">
    <div v-if="loading" class="text-center py-5">載入中...</div>

    <div v-else-if="error" class="alert alert-danger">
      讀取書籍失敗：{{ error }}
    </div>

    <div v-else-if="book" class="row g-4">
      <div class="col-md-4 text-center">
        <img :src="book.coverUrl ?? '/images/default-book.png'"
             :alt="book.title"
             class="img-fluid rounded shadow-sm"
             style="max-width:320px;" />
      </div>

      <div class="col-md-8">
        <h2>{{ book.title }}</h2>
        <p class="text-muted mb-1">作者：{{ book.author ?? '-' }}</p>
        <p class="text-muted">ISBN：{{ book.isbn ?? '-' }}</p>

        <h4 class="text-danger">NT$ {{ book.price ?? '—' }}</h4>

        <div class="my-3">
          <button class="btn btn-primary me-2" @click="addToCart">加入購物車</button>
          <button class="btn btn-outline-secondary" @click="goBack">返回</button>
        </div>

        <hr />

        <h5>書籍說明</h5>
        <div v-if="book.description" v-html="book.description"></div>
        <div v-else class="text-muted">無詳細說明</div>
      </div>
    </div>

    <div v-else class="text-muted">找不到此書。</div>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import http from '@/lib/http' // 你專案的 axios instance

// 若 router 用 props 傳 id，這裡也能用 route 取得備援
const route = useRoute()
const router = useRouter()

const loading = ref(true)
const error = ref<string | null>(null)
const book = ref<any | null>(null)

// route.params.id 通常是 string；若你是用 props: true，也可以從 props 接收
const idRaw = ref(route.params.id ?? null)

// 若 route 變化，更新 idRaw 並重新抓資料
watch(() => route.params.id, (v) => {
  idRaw.value = v ?? null
  fetchBook()
})

// 嘗試多個常見的 API 路徑（依你的後端實作會有一個會成功）
async function fetchBook() {
  loading.value = true
  error.value = null
  book.value = null

  const id = String(idRaw.value ?? '')
  if (!id) {
    error.value = '無效的書籍 id'
    loading.value = false
    return
  }

  const tryUrls = [
    `/api/books/${id}`,
    `/api/BooksApi/Get/${id}`,
    `/api/BooksApi/Detail/${id}`,
    `/api/BooksApi/GetById?id=${id}`,
    `/api/books?id=${id}`
  ]

  let lastErr: any = null
  for (const u of tryUrls) {
    try {
      const res = await http.get(u)
      // 假如你的 API 回傳是 { data: {...} } 或直接回物件，都嘗試處理
      const payload = res?.data ?? res
      // 常見情況：payload 直接是書籍物件，或 payload.data 才是書籍
      const candidate = payload?.data ?? payload
      if (candidate && (candidate.id || candidate.bookId || candidate.bookID || candidate.BookID || candidate.ID)) {
        book.value = normalize(candidate)
        loading.value = false
        return
      }
      // 若 API 回一個陣列或包其他格式，也可嘗試用第一個元素
      if (Array.isArray(candidate) && candidate.length > 0) {
        book.value = normalize(candidate[0])
        loading.value = false
        return
      }
      // 如果 payload 含書籍欄位（依後端可能是 book: {...}）
      if (payload?.book) {
        book.value = normalize(payload.book)
        loading.value = false
        return
      }
      // 若看起來像書籍也接受（至少要有 title）
      if (candidate && candidate.title) {
        book.value = normalize(candidate)
        loading.value = false
        return
      }
    } catch (e: any) {
      lastErr = e
      // 繼續嘗試下一個 URL
    }
  }

  loading.value = false
  error.value = lastErr?.message ?? '找不到對應的 API，請確認後端路徑'
}

// 將不同 API 欄位 normalize 成你前端想用的欄位
function normalize(raw: any) {
  return {
    id: raw.id ?? raw.bookId ?? raw.BookID ?? raw.ID ?? route.params.id,
    title: raw.title ?? raw.bookTitle ?? raw.Name ?? '無標題',
    author: raw.author ?? raw.authors ?? raw.Author ?? '-',
    isbn: raw.isbn ?? raw.ISBN ?? raw.Isbn ?? '-',
    price: raw.price ?? raw.Price ?? raw.SalePrice ?? null,
    coverUrl: raw.coverUrl ?? raw.coverUrlPath ?? raw.imageUrl ?? raw.ImageUrl ?? null,
    description: raw.description ?? raw
