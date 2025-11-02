<script setup lang="ts">
import { ref, computed, onMounted, watch } from 'vue'

// ---- API ----
import { getFrontListings, type Listing } from '@/api/listings'
import {
  prepareReservation,
  createReservation,
  type CreateReservationRequest,
} from '@/api/reservations'
import ReservationDialog from '@/components/ReservationDialog.vue'
import type { MemberOption } from '@/components/ReservationDialog.vue'

// ---- 狀態 ----
const listings = ref<Listing[]>([])
const loading = ref(false)
const err = ref<string | null>(null)

const rowsPerPage = ref(6)
const perPageOptions = [3, 6, 9]
const currentPage = ref(1)

const totalCount = computed(() => listings.value.length)
const totalPages = computed(() => Math.max(1, Math.ceil(totalCount.value / rowsPerPage.value)))
const pagedItems = computed(() => {
  if (currentPage.value > totalPages.value) currentPage.value = totalPages.value
  const start = (currentPage.value - 1) * rowsPerPage.value
  return listings.value.slice(start, start + rowsPerPage.value)
})

const condensedPages = computed(() => {
  const tp = totalPages.value
  const cp = currentPage.value
  const edge = 1
  const around = 1

  if (tp <= 7) {
    return Array.from({ length: tp }, (_, i) => ({
      key: `p${i + 1}`, page: i + 1, active: i + 1 === cp, isEllipsis: false,
    }))
  }

  const keep = new Set<number>()
  for (let i = 1; i <= edge; i++) { keep.add(i); keep.add(tp - i + 1) }
  for (let i = cp - around; i <= cp + around; i++) { if (i >= 1 && i <= tp) keep.add(i) }

  const pages = Array.from(keep).sort((a, b) => a - b)
  const out: Array<{ key: string; page?: number; active?: boolean; isEllipsis: boolean }> = []
  for (let i = 0; i < pages.length; i++) {
    const p = pages[i]
    out.push({ key: `p${p}`, page: p, active: p === cp, isEllipsis: false })
    const next = pages[i + 1]
    if (next && next - p > 1) out.push({ key: `e${p}-${next}`, isEllipsis: true })
  }
  return out
})

function goTo(p:number){ currentPage.value = Math.min(Math.max(1, p), totalPages.value) }
watch(rowsPerPage, () => { currentPage.value = 1 })

function statusText(s:number){ return ({0:'可借',1:'保留中',2:'已借出'} as any)[s] ?? s }
function statusBadgeClass(s:number){ return ({0:'bg-success',1:'bg-primary',2:'bg-warning text-dark'} as any)[s] ?? 'bg-secondary' }

function onImgError(e: Event) {
  const img = e.target as HTMLImageElement | null
  if (img) { img.onerror = null; img.src = '/images/borrow/noimage.jpeg' }
}
const reserving = ref(false)
const showDialog = ref(false)
const dialogMembers = ref<MemberOption[]>([])
const dialogBookTitle = ref('')
const dialogDate = ref('') // YYYY-MM-DD
const dialogTime = ref('') // HH:mm 或 HH:mm:ss
const dialogListingId = ref<number | null>(null)
// 更新列表中單一卡片狀態
function applyReservationToList(listingId:number, newStatus=1){
  listings.value = listings.value.map(it => it.listingId === listingId ? { ...it, status: newStatus } : it)
}



// 把 HH:mm 轉成 HH:mm:ss；若已是 HH:mm:ss 就原樣回傳
function normalizeTime(t: string) {
  if (!t) return t
  const s = t.trim().slice(0, 8)
  return s.length === 5 ? `${s}:00` : s
}

async function doReserve(item: Listing){
  if (reserving.value) return
  reserving.value = true
  try {
    const pre = await prepareReservation(item.listingId)
    dialogListingId.value = pre.listingId
    dialogBookTitle.value = pre.bookTitle
    dialogMembers.value = pre.members as MemberOption[]
    dialogDate.value = pre.defaultPickupDate.slice(0,10)
    dialogTime.value = pre.defaultPickupTime
    showDialog.value = true
  } catch (e:any) {
    alert(e?.message ?? '無法開啟預借視窗')
  } finally {
    reserving.value = false
  }
}
async function onConfirmReserve(payload: { memberId:number; date:string; time:string }){
  if (!dialogListingId.value) return
  try {
    reserving.value = true
    const req: CreateReservationRequest = {
      listingId: dialogListingId.value,
      memberId: payload.memberId,
      requestedPickupDate: payload.date,
      requestedPickupTime: payload.time,
    }
    const res = await createReservation(req)
    if (res.ok) {
      applyReservationToList(res.listingId, res.newStatus ?? 1)
      showDialog.value = false
      alert(res.message ?? '預借成功')
    } else {
      alert(res.message ?? '預借失敗')
    }
  } catch (e:any) {
    alert(e?.message ?? '預借失敗，請稍後再試')
  } finally {
    reserving.value = false
  }
}
// ---- 載入清單 ----
onMounted(async () => {
  try {
    loading.value = true
    err.value = null
    listings.value = await getFrontListings()
    console.log('[UsedListingsGrid] loaded', listings.value?.length, 'items')
  } catch (e:any) {
    err.value = e?.message ?? '讀取失敗'
    console.error('[UsedListingsGrid] load error', e)
    listings.value = []
  } finally {
    loading.value = false
  }
})
</script>

<template>
  <div class="py-3">
    <div class="d-flex flex-wrap gap-2 align-items-center mb-3">
      <div class="ms-auto d-flex align-items-center gap-2">
        <span class="text-muted small">每頁</span>
        <select v-model.number="rowsPerPage" class="form-select form-select-sm" style="width:auto">
          <option v-for="opt in perPageOptions" :key="opt" :value="opt">{{ opt }}</option>
        </select>
        <span class="text-muted small">筆（共 {{ totalCount }} 筆）</span>
      </div>
    </div>

    <div class="row row-cols-1 row-cols-md-2 row-cols-lg-3 g-3">
      <div class="col" v-for="item in pagedItems" :key="item.listingId">
        <div class="card h-100 shadow-sm card-compact">
          <img
            :src="(item.imageUrl || '').trim() || '/images/borrow/noimage.jpeg'"
            :alt="item.title"
            :title="item.title"
            class="card-img-top cover"
            loading="lazy"
            @error="onImgError"
          />

          <div class="card-body d-flex flex-column">
            <h6 class="card-title mb-1 text-truncate-2 fw-semibold">書名 : {{ item.title }}</h6>
            <p class="card-subtitle text-muted mb-2" v-if="item.authorName">作者：{{ item.authorName }}</p>

            <div class="d-flex flex-wrap gap-2 align-items-center mb-2 fs-4">
              <span class="badge" :class="statusBadgeClass(item.status)">{{ statusText(item.status) }}</span>
            </div>

            <dl class="mb-3 small text-muted">
              <div class="d-flex"><dt class="me-2">分類：</dt><dd class="mb-0">{{ item.categoryName || '—' }}</dd></div>
              <div class="d-flex"><dt class="me-2">出版社：</dt><dd class="mb-0">{{ item.publisherName || '—' }}</dd></div>
              <div class="d-flex"><dt class="me-2">書況：</dt><dd class="mb-0">{{ item.condition || '—' }}</dd></div>
              <div class="d-flex"><dt class="me-2">ISBN：</dt><dd class="mb-0">{{ item.isbn || '—' }}</dd></div>
            </dl>

            <div class="mt-auto d-flex gap-2">
              <button
                v-if="item.status===0"
                class="btn btn-primary btn-sm w-25"
                :disabled="reserving"
                @click="doReserve(item)"
              >
                借書
              </button>
              <button v-if="item.status===2" class="btn btn-danger btn-sm w-25" disabled>預約</button>
            </div>
          </div>
        </div>
      </div>
    </div>

    <div v-if="loading" class="text-muted py-4 text-center">載入中…</div>
    <div v-else-if="err" class="text-danger py-4 text-center">{{ err }}</div>
    <div v-if="!totalCount && !loading" class="text-muted py-4 text-center">目前沒有資料</div>

    <nav v-if="totalPages > 1" class="mt-3" aria-label="Page navigation">
      <ul class="pagination pagination-lg justify-content-center flex-wrap">
        <li class="page-item" :class="{ disabled: currentPage===1 }">
          <button class="page-link" @click="goTo(1)">«</button>
        </li>
        <li class="page-item" :class="{ disabled: currentPage===1 }">
          <button class="page-link" @click="goTo(currentPage - 1)">‹</button>
        </li>

        <li
          v-for="item in condensedPages"
          :key="item.key"
          class="page-item"
          :class="{ active: item.active, disabled: item.isEllipsis }"
        >
          <span v-if="item.isEllipsis" class="page-link">…</span>
          <button v-else class="page-link" @click="goTo(item.page!)">{{ item.page }}</button>
        </li>

        <li class="page-item" :class="{ disabled: currentPage===totalPages }">
          <button class="page-link" @click="goTo(currentPage + 1)">›</button>
        </li>
        <li class="page-item" :class="{ disabled: currentPage===totalPages }">
          <button class="page-link" @click="goTo(totalPages)">»</button>
        </li>
      </ul>
    </nav>
  </div>
  <ReservationDialog
    :open="showDialog"
    :book-title="dialogBookTitle"
    :members="dialogMembers"
    :default-date="dialogDate"
    :default-time="dialogTime"
    @close="showDialog=false"
    @confirm="onConfirmReserve"
  />
</template>

<style scoped>
.card-compact .cover {
  width: 100%;
  height: var(--cover-h, 250px);
  object-fit: cover;
  display: block;
  border-top-left-radius: .375rem;
  border-top-right-radius: .375rem;
}
.text-truncate-2 {
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
}
</style>