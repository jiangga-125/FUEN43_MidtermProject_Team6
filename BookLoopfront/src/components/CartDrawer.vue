<script setup lang="ts">
import { computed, defineProps, defineEmits, watch } from 'vue'
import { useCartStore } from '@/stores/cart'
import { useRouter } from 'vue-router'
import { useAuth } from '@/stores/auth'

const props = defineProps<{ visible: boolean; memberId?: number | null }>()
const emit = defineEmits<{ (e: 'update:visible', value: boolean): void }>()

const close = () => emit('update:visible', false)
const cartStore = useCartStore()
const auth = useAuth()
// 綁定 store 中的資料
const cartItems = computed(() => cartStore.items)
const totalItems = computed(() => cartStore.totalItems)
const totalPrice = computed(() => cartStore.totalPrice)
const router = useRouter()

/** 嘗試由多個來源解析 memberId（優先順序：prop > cartStore > auth.store > token） */
function resolveMemberId(): number | null {
  // 1) prop 優先：注意要判斷 null/undefined，而非 truthy
  if (props.memberId !== undefined && props.memberId !== null) {
    // 有可能傳來 string（例如模板綁定錯誤），強制轉型
    const n = Number(props.memberId)
    return Number.isNaN(n) ? null : n
  }

  // 2) cart store（若 store 記錄過 member）
  if ((cartStore as any).memberId !== undefined && (cartStore as any).memberId !== null) {
    const n = Number((cartStore as any).memberId)
    return Number.isNaN(n) ? null : n
  }

  // 3) auth store（主來源，因為 main.ts 已呼 tryLoadSession）
  const m = (auth as any).member
  const candidate = m?.memberId ?? m?.MemberID ?? m?.id ?? null
  if (candidate !== undefined && candidate !== null) {
    const n = Number(candidate)
    return Number.isNaN(n) ? null : n
  }

  // 4) 最後 fallback：嘗試從 storage 的 token decode（保險）
  try {
    const token =
      localStorage.getItem('access_token') ??
      localStorage.getItem('token') ??
      sessionStorage.getItem('access_token') ??
      sessionStorage.getItem('token')
    if (token) {
      const parts = token.split('.')
      if (parts.length >= 2) {
        const payload = JSON.parse(atob(parts[1].replace(/-/g, '+').replace(/_/g, '/')))
        const id =
          payload?.memberId ?? payload?.MemberID ?? payload?.userId ?? payload?.sub ?? payload?.id
        if (id !== undefined && id !== null) {
          const n = Number(id)
          return Number.isNaN(n) ? null : n
        }
      }
    }
  } catch (e) {
    // ignore
  }

  return null
}
/** 載入購物車（如果沒有 memberId，可決定顯示 guest cart 或清空） */
async function loadCartIfNeeded() {
  const mid = resolveMemberId()
  if (mid != null) {
    console.log('[CartDrawer] loadCartIfNeeded mid=', mid)
    try {
      await cartStore.initCart(mid)
    } catch (e) {
      console.error('[CartDrawer] initCart failed', e)
    }
  } else {
    console.log('[CartDrawer] no memberId resolved — clearing or loading guest cart')
    // 若你有 guest cart 實作，可在此呼叫
    // e.g. cartStore.loadGuestCart()
    cartStore.clearCart()
  }
}

//監聽 visible：當顯示時嘗試載入購物車
watch(
  () => props.visible,
  async (visible) => {
    if (visible) {
      await loadCartIfNeeded()
    }
  },
  { immediate: true },
)

//監聽 memberId prop 變化（父層後來才傳入）
watch(
  () => props.memberId,
  async () => {
    // 只有在 drawer 已開時才重新載入（避免不必要 requests）
    if (props.visible) await loadCartIfNeeded()
  },
)

watch(
  () => (auth as any).member,
  async () => {
    if (props.visible) await loadCartIfNeeded()
  },
)

// 更新商品數量（只修改前端）
function updateItem(bookId: number, qty: number) {
  const item = cartStore.items.find((i) => i.book.id === bookId)
  if (!item) return
  qty = Math.max(1, Math.floor(Number(qty) || 1))
  // 若 store 提供更新方法，使用 store 的 method（示範）
  if ((cartStore as any).updateQuantity) {
    ;(cartStore as any).updateQuantity(bookId, qty)
  } else {
    item.quantity = qty
    // 若需要同步到後端，可在此呼 cartStore.sync()
  }
}

// 移除商品
function removeItem(itemId: number | null) {
  if (itemId != null) cartStore.removeItemByItemId(itemId)
}

// 清空購物車
function clearCart() {
  cartStore.clearCart()
}

// 結帳
async function checkoutCart() {
  try {
    const mid = resolveMemberId()
    if (mid == null) {
      alert('請先登入會員')
      return
    }

    const orderId = await cartStore.checkout()
    console.log('checkoutCart OrderID:', orderId)

    // Modal 關閉
    close()
  } catch (err) {
    const e = err as any
    alert(e.message || '結帳失敗')
  }
}
</script>

<template>
  <div v-if="visible" class="cart-modal">
    <div class="modal-backdrop" @click="close"></div>

    <div class="modal-content animate-slide-up">
      <div class="modal-header">
        <h1 class="mb-0 fw-bold">🛒 我的購物車</h1>
        <button class="btn-close" @click="close">✕</button>
      </div>

      <div class="modal-body">
        <div v-if="cartItems.length === 0" class="text-center py-5 text-muted fs-5">
          購物車是空的
        </div>

        <div v-else>
          <div class="cart-list mb-4">
            <div
              v-for="item in cartItems"
              :key="item.itemId || item.book.id"
              class="cart-item d-flex justify-content-between align-items-center border-bottom py-3"
            >
              <div class="d-flex align-items-center gap-3 flex-grow-1">
                <img
                  :src="
                    item.book.coverUrl && item.book.coverUrl.startsWith('http')
                      ? item.book.coverUrl
                      : `/api/BookImages/${item.book.id}/cover`
                  "
                  :alt="item.book.title || 'Book Cover'"
                  @error="
                    (e: Event) => {
                      const target = e.currentTarget as HTMLImageElement | null
                      if (target) target.src = '/placeholder.png'
                    }
                  "
                  class="rounded shadow-sm"
                  style="width: 60px; height: 80px; object-fit: cover"
                />
                <div>
                  <strong class="fs-6">{{ item.book.title }}</strong>
                  <div class="text-muted small">NT$ {{ item.book.salePrice || 0 }}</div>
                </div>
              </div>

              <div class="d-flex align-items-center gap-2">
                <input
                  type="number"
                  class="form-control form-control-sm text-center"
                  style="width: 70px"
                  min="1"
                  v-model.number="item.quantity"
                  @change="updateItem(item.book.id, item.quantity)"
                />
                <button class="btn btn-sm btn-outline-danger" @click="removeItem(item.itemId)">
                  ✕
                </button>
              </div>
            </div>
          </div>

          <div class="summary-box mb-4">
            <div class="d-flex justify-content-between fs-5 mb-2">
              <span>🧺 總數量：</span>
              <strong>{{ totalItems }}</strong>
            </div>
            <div class="d-flex justify-content-between fs-5">
              <span>💰 總金額：</span>
              <strong class="text-danger fs-4">NT$ {{ totalPrice }}</strong>
            </div>
          </div>

          <div class="d-flex justify-content-end gap-3">
            <button class="btn btn-outline-secondary px-4" @click="clearCart">清空購物車</button>
            <button class="btn btn-primary px-4" @click="checkoutCart">前往結帳</button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.cart-modal {
  position: fixed;
  inset: 0;
  z-index: 2000;
  display: flex;
  align-items: center;
  justify-content: center;
}

.modal-backdrop {
  position: absolute;
  inset: 0;
  background: rgba(0, 0, 0, 0.55);
  backdrop-filter: blur(6px);
}

.modal-content {
  position: relative;
  z-index: 2100;
  background: #fff;
  width: 900px;
  height: 900px; /* 保留固定初始高度 */
  max-width: 95%;
  max-height: 95%;
  padding: 2.5rem;
  border-radius: 22px;
  box-shadow: 0 12px 36px rgba(0, 0, 0, 0.28);
  display: flex;
  flex-direction: column;
}
.modal-body {
  flex-grow: 1;      /* 撐滿 modal-content 高度 */
  overflow-y: auto;  /* 超過高度自動滾動 */
}

.cart-list {
  overflow-y: auto;
  max-height: 600px;
}

.btn-close {
  position: absolute;
  top: 20px;
  right: 20px;
  font-size: 1.6rem;
  background: none;
  border: none;
  cursor: pointer;
  opacity: 0.7;
  transition: opacity 0.2s;
}
.btn-close:hover {
  opacity: 1;
}

@keyframes slideUp {
  from {
    transform: translateY(50px) scale(0.95);
    opacity: 0;
  }
  to {
    transform: translateY(0) scale(1);
    opacity: 1;
  }
}
.animate-slide-up {
  animation: slideUp 0.35s ease-out;
}

.cart-item:hover {
  background: #f9fafc;
  transition: background 0.2s;
}

@media (max-width: 576px) {
  .modal-content {
    width: 95%;
    max-height: 90%;
    padding: 1rem;
  }
  .cart-list {
    max-height: 300px;
  }
}
</style>
