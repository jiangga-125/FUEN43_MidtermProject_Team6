<!-- src/components/ProductCard.vue -->
<script setup lang="ts">
import { toRef, computed, ref } from 'vue'
import { useRouter } from 'vue-router'
import type { Book } from '@/api/book'
import { useAuth } from '@/stores/auth'
import http from '@/lib/http'

const props = defineProps<{ book: Book | null }>()
const emit = defineEmits<{
  (e: 'add', b: Book): void
  (e: 'like', b: Book): void
  (e: 'view', id: string | number): void
}>()
const book = toRef(props, 'book')

const getBookId = (b: Book | null) =>
  (b as any)?.id ?? (b as any)?.bookId ?? (b as any)?.BookID ?? ''
const coverSrc = computed(() => {
  const b = book.value
  if (!b) return '/placeholder.png'
  const anyb = b as any
  if (anyb.coverUrl) return anyb.coverUrl
  if (anyb.image) return anyb.image
  const id = getBookId(b)
  if (id) return `/api/BookImages/book/${id}/cover`
  return '/placeholder.png'
})
const originalSrc = computed(() => {
  const b = book.value as any
  return (
    b?.coverUrl ??
    b?.image ??
    (getBookId(book.value) ? `/api/BookImages/book/${getBookId(book.value)}/cover` : '')
  )
})

// local state
const adding = ref(false)
const liking = ref(false)
const router = useRouter()
const auth = useAuth()

// 取得 memberId 的 helper
async function resolveMemberId(): Promise<number | null> {
  const m = (auth as any).member
  const tryIds = [m?.MemberID, m?.memberId, m?.id]
  for (const v of tryIds) if (v) return Number(v)

  try {
    const r = await http.get('/api/auth/me')
    const data = r?.data ?? r
    const candidate = data?.memberId ?? data?.MemberID ?? data?.id ?? data?.userId
    if (candidate) return Number(candidate)
  } catch {
    // ignore
  }
  return null
}

async function add(e?: Event) {
  e?.stopPropagation()
  if (!book.value) return
  if (adding.value) return
  adding.value = true

  const id = Number(getBookId(book.value))
  if (!id) {
    alert('找不到 book id')
    adding.value = false
    return
  }

  const memberId = await resolveMemberId()
  if (!memberId) {
    alert('請先登入或確認會員資訊（MemberID）')
    adding.value = false
    return
  }

  const unitPrice = (book.value as any)?.salePrice ?? (book.value as any)?.listPrice ?? 0
  const payload = {
    MemberID: memberId,
    BookID: id,
    Quantity: 1,
    UnitPrice: unitPrice,
  }

  try {
    const res = await http.post('/api/ShoppingCart/add', payload)
    const data = res?.data ?? res
    if (data && (data.success === true || res.status === 200 || res.status === 201)) {
      // MODIFIED: 不再 emit('add', book.value) 以避免父層重複呼叫
      // 改成僅顯示成功提示或更新 local state
      alert(data.message ?? '已加入購物車')
    } else {
      alert(data?.message ?? '加入購物車失敗')
    }
  } catch (err: any) {
    console.error('AddToCart error', err)
    const msg = err?.response?.data?.message ?? err?.response?.data ?? err?.message ?? '網路錯誤'
    alert('加入購物車失敗：' + msg)
  } finally {
    adding.value = false
  }
}

async function like(e?: Event) {
  e?.stopPropagation()
  if (!book.value) return
  if (liking.value) return
  liking.value = true
  try {
    emit('like', book.value)
    alert('已加入收藏（示範）')
  } finally {
    liking.value = false
  }
}

function goDetail() {
  const id = getBookId(book.value)
  if (!id) return
  router.push(`/books/${id}`)
  emit('view', id)
}

function onImgError(e: Event) {
  const img = (e.currentTarget ?? e.target) as HTMLImageElement
  if (!img) return
  if (!img.dataset['errored']) {
    img.dataset['errored'] = '1'
    img.src = '/placeholder.png'
  }
}
</script>

<template>
  <div class="card" @click="goDetail" role="button" tabindex="0" @keydown.enter.prevent="goDetail">
    <div class="cover">
      <img
        :src="coverSrc"
        :alt="book?.title || 'cover'"
        :data-orig="originalSrc"
        @error="onImgError"
        loading="lazy"
      />
    </div>

    <div class="info">
      <h5 class="title" :title="book?.title">{{ book?.title }}</h5>
      <div class="prices">
        <span v-if="book?.salePrice != null" class="sale"
          >NT$ {{ Math.round(book!.salePrice) }}
        </span>
        <span
          v-if="book?.listPrice && book?.salePrice && book!.salePrice < book!.listPrice"
          class="list"
        >
          NT$ {{ Math.round(book!.listPrice) }}
        </span>
      </div>

      <div class="actions">
        <button type="button" @click="add" :disabled="adding">加入購物車</button>
        <button class="ghost" @click="like">收藏</button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.card {
  border: 1px solid #eee;
  border-radius: 12px;
  overflow: hidden;
  background: #fff;
  display: grid;
  grid-template-rows: 180px 1fr;
}
.cover {
  background: #f6f7f9;
  display: flex;
  align-items: center;
  justify-content: center;
}
.cover img {
  max-width: 100%;
  max-height: 100%;
  object-fit: contain;
}
.info {
  padding: 10px;
  display: grid;
  gap: 8px;
}
.title {
  font-size: 14px;
  line-height: 1.4;
  height: 40px;
  overflow: hidden;
}
.prices {
  display: flex;
  align-items: center;
  gap: 8px;
}
.sale {
  color: #e63946;
  font-weight: 700;
}
.list {
  color: #999;
  text-decoration: line-through;
}
.actions {
  display: flex;
  gap: 8px;
}
.actions button {
  flex: 1;
  cursor: pointer;
  border-radius: 8px;
  padding: 8px 10px;
  border: 1px solid #0d6efd;
  background: #0d6efd;
  color: #fff;
}
.actions .ghost {
  background: #fff;
  color: #0d6efd;
}
</style>
