<!-- src/components/CartDrawer.vue -->
<script setup lang="ts">
import { computed, defineProps, defineEmits, onMounted } from 'vue'
import { useCartStore } from '@/stores/cart'

const props = defineProps<{ visible: boolean; memberId: number }>()
const emit = defineEmits<{ (e: 'update:visible', value: boolean): void }>()
const close = () => emit('update:visible', false)

const cartStore = useCartStore()
const cartItems = computed(() => cartStore.items)
const totalItems = computed(() => cartStore.totalItems)
const totalPrice = computed(() => cartStore.totalPrice)

// 新增：打開購物車時初始化
onMounted(async () => {
  if (props.memberId) {
    await cartStore.initCart(props.memberId)
  }
})

function updateItem(bookId: number, qty: number) {
  if (qty <= 0) cartStore.removeItem(bookId)
  else cartStore.updateItem(bookId, qty)
}
function removeItem(bookId: number) {
  cartStore.removeItem(bookId)
}
function clearCart() {
  cartStore.clearCart()
}

async function checkoutCart() {
  try {
    // ✅ 使用 store 裡的 memberId
    const memberId = cartStore.memberId
    if (!memberId) {
      alert('請先登入會員')
      return
    }

    // ✅ 新增：呼叫 store 裡的 checkout 方法
    const res = await cartStore.checkout(memberId)

    // 假設後端回傳 { OrderID: 123 }
    const orderId = res.OrderID || res.orderId || '未知'
    alert('訂單建立成功！訂單編號：' + orderId)

    close()
  } catch (err: any) {
    alert(err.message || '結帳失敗')
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
              :key="item.book.id"
              class="cart-item d-flex justify-content-between align-items-center border-bottom py-3"
            >
              <div class="d-flex align-items-center gap-3 flex-grow-1">
                <img
                  :src="item.book.coverUrl || 'https://via.placeholder.com/60x80?text=Book'"
                  alt="Book Cover"
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
                <button class="btn btn-sm btn-outline-danger" @click="removeItem(item.book.id)">
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

/* 背景半透明＋模糊 */
.modal-backdrop {
  position: absolute;
  inset: 0;
  background: rgba(0, 0, 0, 0.55);
  backdrop-filter: blur(6px);
}

/* 主視窗 */
.modal-content {
  position: relative;
  z-index: 2100;
  background: #fff;
  width: 900px;
  height: 900px;
  max-width: 95%;
  max-height: 95%;
  padding: 2.5rem;
  border-radius: 22px;
  box-shadow: 0 12px 36px rgba(0, 0, 0, 0.28);
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

/* 商品滾動區 */
.cart-list {
  overflow-y: auto;
  max-height: 600px;
}

/* 關閉按鈕 */
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

/* 彈出動畫 */
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

/* 商品 hover */
.cart-item:hover {
  background: #f9fafc;
  transition: background 0.2s;
}

/* 手機適應 */
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
