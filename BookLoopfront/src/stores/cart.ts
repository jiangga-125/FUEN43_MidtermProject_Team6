import { defineStore } from 'pinia'
import type { Book } from '@/api/book'
import { ref, computed } from 'vue'
import * as orderApi from '@/api/order' // 下單用
import * as cartApi from '@/api/shoppingCart' // 下單用
import type { Order } from '@/api/order' // 引入正確的 Order 型別

export type CartItem = {
  book: Book
  quantity: number
}

export const useCartStore = defineStore('cart', () => {
  const items = ref<CartItem[]>([]) // 購物車商品清單
  const memberId = ref<number | null>(null) // 會員 ID，用於 API

  //初始化購物車
  async function initCart(member: number) {
    memberId.value = member
    await fetchCart()
  }

  async function fetchCart() {
    if (!memberId.value) return
    const data = await cartApi.getCart()
    // 後端回傳的格式對應 CartItem
    items.value = data.map((i: any) => ({
      book: {
        id: i.bookId,
        title: i.title,
        coverUrl: i.coverUrl,
        salePrice: i.price,
        // 其他欄位可視需要加上
      },
      quantity: i.quantity,
    }))
  }

  // 加入購物車
  async function addItem(book: Book, qty = 1) {
    if (!memberId.value) throw new Error('請先登入')
    await cartApi.addToCart({
      MemberID: memberId.value,
      BookID: book.id,
      Quantity: qty,
      UnitPrice: book.salePrice || 0,
    })
    await fetchCart()
  }

  // 更新數量
  async function updateItem(bookId: number, qty: number) {
    if (!memberId.value) return
    await cartApi.updateCartItem({
      MemberID: memberId.value,
      BookID: bookId,
      Quantity: qty,
      UnitPrice: 0, // 後端只更新數量
    })
    await fetchCart()
  }

  // 移除商品
  async function removeItem(bookId: number) {
    if (!memberId.value) return
    await cartApi.removeCartItem(bookId)
    await fetchCart()
  }

  // 清空購物車
  async function clearCart() {
    if (!memberId.value) return
    await cartApi.clearCart()
    items.value = []
  }

  // 計算總數量
  const totalItems = computed(() => items.value.reduce((sum, i) => sum + i.quantity, 0))

  // 計算總價
  const totalPrice = computed(() =>
    items.value.reduce((sum, i) => sum + (i.book.salePrice || 0) * i.quantity, 0),
  )
  // 結帳
  async function checkout() {
    if (!memberId.value) throw new Error('請先登入')
    if (!items.value.length) throw new Error('購物車空的')

    const order: Order = {
      MemberID: memberId.value,
      TotalAmount: totalPrice.value,
      OrderDetails: items.value.map((i) => ({
        BookID: i.book.id,
        Quantity: i.quantity,
        UnitPrice: i.book.salePrice || 0,
      })),
    }

    const res = await orderApi.createOrder(order)
    await clearCart()
    return res
  }

  return {
    items,
    memberId,
    initCart,
    fetchCart,
    addItem,
    updateItem,
    removeItem,
    clearCart,
    totalItems,
    totalPrice,
    checkout,
  }
})
