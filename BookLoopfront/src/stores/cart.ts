import { defineStore } from 'pinia'
import type { Book } from '@/api/catalog'
import { ref, computed } from 'vue'
import * as orderApi from '@/api/order' // 下單用
import type { Order } from '@/api/order' // 引入正確的 Order 型別

export type CartItem = {
  book: Book
  quantity: number
}

export const useCartStore = defineStore('cart', () => {
  const items = ref<CartItem[]>([]) // 購物車商品清單

  // 加入購物車
  function addItem(book: Book, qty = 1) {
    const existing = items.value.find(i => i.book.bookId === book.bookId)
    if (existing) {
      existing.quantity += qty
    } else {
      items.value.push({ book, quantity: qty })
    }
  }

  // 更新數量
  function updateItem(bookId: number, qty: number) {
    const item = items.value.find(i => i.book.bookId === bookId)
    if (item) {
      item.quantity = qty
      if (item.quantity <= 0) removeItem(bookId)
    }
  }

  // 移除商品
  function removeItem(bookId: number) {
    items.value = items.value.filter(i => i.book.bookId !== bookId)
  }

  // 清空購物車
  function clearCart() {
    items.value = []
  }

  // 計算總數量
  const totalItems = computed(() => items.value.reduce((sum, i) => sum + i.quantity, 0))

  // 計算總價
  const totalPrice = computed(() =>
    items.value.reduce((sum, i) => sum + (i.book.salePrice || 0) * i.quantity, 0)
  )

  // 建立訂單
  async function checkout(memberId: number) {
    if (!items.value.length) throw new Error('購物車空的')

    // 對應後端 Order 型別
    const order: Order = {
      MemberID: memberId,
      TotalAmount: totalPrice.value,
      OrderDetails: items.value.map(i => ({
        BookID: i.book.bookId,
        Quantity: i.quantity,
        UnitPrice: i.book.salePrice || 0
      }))
    }

    const res = await orderApi.createOrder(order)
    clearCart()
    return res
  }

  return {
    items,
    addItem,
    updateItem,
    removeItem,
    clearCart,
    totalItems,
    totalPrice,
    checkout,
  }
})