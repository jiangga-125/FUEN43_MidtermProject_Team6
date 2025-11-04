<!-- src/components/CartDrawer.vue -->
<template>
  <div class="cart-drawer" v-show="visible">
    <div class="drawer-backdrop" @click="close"></div>

    <div class="drawer-content">
      <div class="drawer-header d-flex justify-content-between align-items-center">
        <h5 class="mb-0">購物車</h5>
        <button class="btn-close" @click="close"></button>
      </div>

      <div class="drawer-body">
        <div v-if="cartItems.length === 0" class="text-center py-4">
          購物車是空的
        </div>

        <div v-else>
          <ul class="list-group mb-3">
            <li v-for="item in cartItems" :key="item.book.bookId" class="list-group-item d-flex justify-content-between align-items-center">
              <div class="flex-grow-1">
                <strong>{{ item.book.title }}</strong>
                <div class="text-muted small">NT$ {{ item.book.salePrice || 0 }}</div>
              </div>

              <div class="d-flex align-items-center gap-2">
                <input
                  type="number"
                  class="form-control form-control-sm"
                  style="width: 60px"
                  min="1"
                  v-model.number="item.quantity"
                  @change="updateItem(item.book.bookId, item.quantity)"
                />
                <button class="btn btn-sm btn-outline-danger" @click="removeItem(item.book.bookId)">
                  ✕
                </button>
              </div>
            </li>
          </ul>

          <div class="d-flex justify-content-between mb-3">
            <strong>總數量：</strong> {{ totalItems }}
          </div>
          <div class="d-flex justify-content-between mb-3">
            <strong>總金額：</strong> NT$ {{ totalPrice }}
          </div>

          <div class="d-flex justify-content-end gap-2">
            <button class="btn btn-secondary" @click="clearCart">清空購物車</button>
            <button class="btn btn-primary" @click="checkoutCart">結帳</button>
          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, defineProps, defineEmits } from 'vue'
import { useCartStore } from '@/stores/cart'

const props = defineProps<{ visible: boolean }>()
const emit = defineEmits<{ (e: 'update:visible', value: boolean): void }>()

const close = () => emit('update:visible', false)

const cartStore = useCartStore()

const cartItems = computed(() => cartStore.items)
const totalItems = computed(() => cartStore.totalItems)
const totalPrice = computed(() => cartStore.totalPrice)

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

// 結帳
async function checkoutCart() {
  try {
    // 假設會員ID固定 1，如果有登入系統可替換成真實會員ID
    const memberId = 1
    const res = await cartStore.checkout(memberId)
    
    // 解構出小寫 orderId
    const { OrderID: orderId } = res
    alert('訂單建立成功！訂單編號：' + orderId)
    
    close()
  } catch (err: any) {
    alert(err.message || '結帳失敗')
  }
}
</script>

<script lang="ts">
export default {}
</script>



<style scoped>
.cart-drawer {
  position: fixed;
  top: 0;
  right: 0;
  width: 350px;
  height: 100%;
  z-index: 1200;
  display: flex;
  flex-direction: column;
  transform: translateX(100%);
  transition: transform 0.3s ease;
}

.cart-drawer[v-show="true"] {
  transform: translateX(0);
}

.drawer-backdrop {
  position: fixed;
  inset: 0;
  background: rgba(0,0,0,0.4);
  z-index: 1100;
}

.drawer-content {
  background: white;
  height: 100%;
  box-shadow: -2px 0 8px rgba(0,0,0,0.2);
  display: flex;
  flex-direction: column;
}

.drawer-header {
  padding: 1rem;
  border-bottom: 1px solid #ddd;
}

.drawer-body {
  padding: 1rem;
  overflow-y: auto;
  flex-grow: 1;
}

.btn-close {
  background: none;
  border: none;
  font-size: 1.2rem;
}
</style>