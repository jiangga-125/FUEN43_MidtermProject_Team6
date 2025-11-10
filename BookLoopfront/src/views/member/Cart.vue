<script setup lang="ts">
import { computed, onMounted } from 'vue'
import { useCartStore } from '@/stores/cart'
import { useAuth } from '@/stores/auth'
import { useRouter } from 'vue-router'

const cartStore = useCartStore()
const auth = useAuth()
const router = useRouter()

// 綁定 store 中資料
const cartItems = computed(() => cartStore.items)
const totalItems = computed(() => cartStore.totalItems)
const totalPrice = computed(() => cartStore.totalPrice)

/** 嘗試取得 memberId */
function resolveMemberId(): number | null {
  const m = (auth as any).member
  if (!m) return null
  const candidate = m?.memberId ?? m?.MemberID ?? m?.id ?? null
  if (candidate !== null && candidate !== undefined) return Number(candidate)
  return null
}

/** 載入購物車 */
async function loadCart() {
  const mid = resolveMemberId()
  if (mid != null) {
    try {
      await cartStore.initCart(mid)
    } catch (e) {
      console.error('載入購物車失敗', e)
    }
  } else {
    cartStore.clearCart()
  }
}

/** 更新商品數量 */
function updateItem(bookId: number, qty: number) {
  const item = cartStore.items.find(i => i.book.id === bookId)
  if (!item) return
  item.quantity = Math.max(1, Math.floor(Number(qty) || 1))
  // 可呼叫 store 的同步方法
  if ((cartStore as any).updateQuantity) (cartStore as any).updateQuantity(bookId, item.quantity)
}

/** 移除商品 */
function removeItem(itemId: number | null) {
  if (itemId != null) cartStore.removeItemByItemId(itemId)
}

/** 清空購物車 */
function clearCart() {
  cartStore.clearCart()
}

/** 前往結帳 */
function checkoutCart() {
  const mid = resolveMemberId()
  if (!mid) {
    alert('請先登入會員')
    router.push({ name: 'Login' } as any)
    return
  }
  router.push({ path: '/checkout' })
}

onMounted(loadCart)
</script>

<template>
  <div class="container py-4">
    <h3>🛒 我的購物車</h3>
    <div v-if="cartItems.length === 0" class="text-center py-5 text-muted">
      購物車是空的
    </div>

    <div v-else>
      <div class="cart-list mb-4">
        <div v-for="item in cartItems" :key="item.itemId || item.book.id" class="cart-item d-flex justify-content-between align-items-center border-bottom py-3">
          <div class="d-flex align-items-center gap-3 flex-grow-1">
            <img
              :src="item.book.coverUrl && item.book.coverUrl.startsWith('http') ? item.book.coverUrl : `/api/BookImages/${item.book.id}/cover`"
              :alt="item.book.title || 'Book Cover'"
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
            <button class="btn btn-sm btn-outline-danger" @click="removeItem(item.itemId)">✕</button>
          </div>
        </div>
      </div>

      <!-- 總金額 -->
      <div class="summary-box mb-4 d-flex justify-content-between fs-5 border-top pt-2">
        <span>🧾 總金額：</span>
        <strong>NT$ {{ totalPrice }}</strong>
      </div>

      <div class="d-flex justify-content-end gap-3">
        <button class="btn btn-outline-secondary px-4" @click="clearCart">清空購物車</button>
        <button class="btn btn-primary px-4" @click="checkoutCart">前往結帳</button>
      </div>
    </div>
  </div>
</template>

<style scoped>
.cart-item img {
  width: 60px;
  height: 80px;
  object-fit: cover;
}
</style>
