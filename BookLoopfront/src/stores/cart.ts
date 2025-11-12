import { defineStore } from 'pinia'
import type { Book } from '@/api/book'
import { ref, computed } from 'vue'
import { useRouter } from 'vue-router'
import * as orderApi from '@/api/order'
import * as cartApi from '@/api/shoppingCart'
import type { Order } from '@/api/order'

export type CartItem = {
  itemId: number | null // 後端購物車項目 ID
  book: Book
  quantity: number
}

export const useCartStore = defineStore('cart', () => {
  const router = useRouter()
  const items = ref<CartItem[]>([])
  const memberId = ref<number | null>(null)
  const appliedCoupon = ref<{
    code: string | null
    discount: number
    memberCouponId?: number | null
  } | null>(null)

  function setAppliedCoupon(c: { code: string; discount: number; memberCouponId?: number | null }) {
    appliedCoupon.value = {
      code: c.code,
      discount: Number(c.discount || 0),
      memberCouponId: c.memberCouponId ?? null,
    }
  }

  function clearAppliedCoupon() {
    appliedCoupon.value = null
  }
  // 初始化購物車
  async function initCart(member: number) {
    memberId.value = member
    await fetchCart()
  }

  // 取得購物車內容
  async function fetchCart() {
    if (!memberId.value) {
      items.value = []
      return
    }

    const data = await cartApi.getCart(memberId.value)
    items.value = data.map((i) => ({
      itemId: i.itemId,
      book: {
        id: i.bookId,
        title: i.title,
        coverUrl: i.coverUrl || '', // 若後端有 coverUrl
        salePrice: i.price,
      },
      quantity: i.quantity,
    }))
  }

  // 加入商品
  async function addItem(book: Book, qty = 1) {
    if (!memberId.value) throw new Error('請先登入')
    await cartApi.addToCart({
      MemberID: memberId.value,
      BookID: book.id,
      Quantity: qty,
      UnitPrice: book.salePrice || book.listPrice || 0,
    })
    await fetchCart()
  }

  // 移除商品（使用 itemId）
  async function removeItemByItemId(itemId: number | null) {
    if (!memberId.value || itemId == null) return
    await cartApi.removeCartItem(itemId)
    await fetchCart()
  }

  // 移除商品（使用 bookId，從 items 找到 itemId 再刪）
  async function removeItem(bookId: number) {
    const item = items.value.find((i) => i.book.id === bookId)
    if (!item) return
    await removeItemByItemId(item.itemId)
  }

  // 清空購物車（前端 store 與後端都刪）
  async function clearCart() {
    if (items.value.length > 0) {
      await Promise.all(
        items.value.map((i) => (i.itemId ? cartApi.removeCartItem(i.itemId) : Promise.resolve())),
      )
    }
    items.value = []
  }

  const totalItems = computed(() => items.value.reduce((sum, i) => sum + i.quantity, 0))
  const totalPrice = computed(() =>
    items.value.reduce((sum, i) => sum + (i.book.salePrice || 0) * i.quantity, 0),
  )

  // -----------------------------
  // 結帳
  // -----------------------------
  async function checkout(): Promise<number> {
    if (!memberId.value) throw new Error('請先登入')
    if (!items.value.length) throw new Error('購物車空的')

    const orderPayload: any = {
      MemberID: memberId.value,
      TotalAmount: totalPrice.value,
      OrderDate: new Date().toISOString(),
      OrderDetails: items.value.map((i) => ({
        BookID: i.book.id,
        Quantity: i.quantity > 0 ? i.quantity : 1,
        UnitPrice: i.book.salePrice && i.book.salePrice > 0 ? i.book.salePrice : 1,
      })),
    }

    // ✅ 把 coupon 加進 payload（若有）
    if (appliedCoupon.value) {
      // 傳 coupon code 與可能的 memberCouponId；後端務必再次驗證
      orderPayload.CouponCode = appliedCoupon.value.code
      if (appliedCoupon.value.memberCouponId != null) {
        orderPayload.MemberCouponId = appliedCoupon.value.memberCouponId
      }
    }

    console.log('📦 結帳傳送的訂單資料:', orderPayload)

    const orderId = await orderApi.createOrder(orderPayload) // 後端應回傳 orderID 與最終價格
    console.log('✅ 後端回傳 OrderID:', orderId)

    if (!orderId) throw new Error('後端沒有回傳 OrderID')

    // 若下單成功，清除購物車與已套用優惠券（store 層也清）
    await clearCart()
    clearAppliedCoupon()
    console.log('🧹 購物車已清空（連後端資料）')

    goToOrders(orderId)
    return orderId
  }

  // 跳到訂單中心，順便帶 selectedOrderId
  function goToOrders(orderId?: number) {
    router.push({
      path: '/order-center',
      query: orderId ? { selectedOrderId: orderId.toString() } : {},
    })
  }

  return {
    items,
    memberId,
    initCart,
    fetchCart,
    addItem,
    removeItem,
    removeItemByItemId,
    clearCart,
    totalItems,
    totalPrice,
    checkout,
    goToOrders,
    appliedCoupon,
    setAppliedCoupon,
    clearAppliedCoupon,
  }
})
