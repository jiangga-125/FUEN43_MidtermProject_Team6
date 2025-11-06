<script setup lang="ts">
import { computed, defineProps, defineEmits, ref, watch, Ref } from 'vue'
import { useCartStore } from '@/stores/cart'
import { useRouter } from 'vue-router'
import http from '@/lib/http'
import { useAuth  } from '@/stores/auth'

// --- 傳入父層 props 與事件 ---
const props = defineProps<{ visible: boolean; memberId: number }>()
const emit = defineEmits<{ (e: 'update:visible', value: boolean): void }>()
const close = () => emit('update:visible', false)

const cartStore = useCartStore()
const router = useRouter()
const auth = useAuth()

// 綁定購物車資料
const cartItems = computed(() => cartStore.items)
const totalItems = computed(() => cartStore.totalItems)
const totalPrice = computed(() => cartStore.totalPrice)

// 優惠券輸入與折扣資料
const couponCode = ref('')
const discountAmount = ref(0)
const discountInfo = ref<string>('')
const memberCoupons: Ref<any[]> = ref([]) // ✅ 小寫統一，型別明確化

// 當購物車開啟時載入資料
watch(
  () => props.visible,
  async (visible) => {
    if (!visible) return

    console.log('🟡 購物車開啟，準備載入優惠券...')
    // ✅ 1️⃣ 確保 Token 已灌入 header
    auth.__hydrateHttpAuthHeaderOnce()

    // ✅ 2️⃣ 等待登入狀態載入（確保 token 有效）
    const ok = await auth.tryLoadSession()
    if (!ok) {
      alert('請先登入會員再查看優惠券')
      return
    }

    // ✅ 3️⃣ 開始載入購物車與優惠券
    console.log('📦 Fetching cart for member', props.memberId)
    await cartStore.initCart(props.memberId)
    await loadMemberCoupons()
  },
  { immediate: true }
)

// ✅ 取得會員已領取的優惠券清單
async function loadMemberCoupons() {
  try {
    // 🟢 若沒有 token，就不發 request
    const token =
      localStorage.getItem('access_token') || sessionStorage.getItem('access_token')
    if (!token) {
      console.warn('⚠️ 無法載入優惠券：尚未登入')
      memberCoupons.value = []
      return
    }

    // 🟢 呼叫 API
    const { data } = await http.get('/api/MemberCouponsApi/List')
    memberCoupons.value = data?.usable ?? []
    console.log('🎟 已領取優惠券：', memberCoupons.value)
  } catch (err: any) {
    console.error('❌ 無法載入會員優惠券', err)
    memberCoupons.value = []
  }
}


// ✅ 使用代碼領取優惠券
async function claimByCode() {
  if (!couponCode.value.trim()) {
    alert('請輸入優惠代碼')
    return
  }
  try {
    const { data } = await http.post('/api/MemberCouponsApi/ClaimByCode', {
      Code: couponCode.value.trim(),
    })
    alert(data.message || '領取成功！')
    couponCode.value = ''
    await loadMemberCoupons()
  } catch (err: any) {
    alert(err.response?.data?.message || '領取失敗，請確認優惠代碼是否正確')
  }
}

// ✅ 套用優惠券折扣
async function applyCoupon() {
  if (!couponCode.value.trim()) {
    alert('請輸入優惠碼')
    return
  }
  try {
    const subtotal = totalPrice.value
    const { data } = await http.post('/api/CouponsApi/Apply', {
      Code: couponCode.value,
      Subtotal: subtotal,
    })
    if (data.success) {
      discountAmount.value = data.data.discount
      discountInfo.value = data.data.rule
    } else {
      discountAmount.value = 0
      discountInfo.value = data.message || '無效的優惠券'
    }
  } catch (err: any) {
    console.error('applyCoupon error:', err)
    alert('套用優惠券時發生錯誤，請重新登入')
  }
}

// ✅ 點擊優惠券 → 自動填入並立即套用
async function useCoupon(coupon: any) {
  couponCode.value = coupon.code
  await applyCoupon()
}

// ✅ 更新商品數量
function updateItem(bookId: number, qty: number) {
  const item = cartStore.items.find(i => i.book.id === bookId)
  if (!item) return
  if (qty <= 0) cartStore.removeItem(bookId)
  else item.quantity = qty
}

// ✅ 移除商品
function removeItem(itemId: number | null) {
  if (itemId != null) cartStore.removeItemByItemId(itemId)
}

// ✅ 清空購物車
function clearCart() {
  cartStore.clearCart()
  discountAmount.value = 0
  discountInfo.value = ''
  couponCode.value = ''
}

// ✅ 結帳
async function checkoutCart() {
  try {
    if (!cartStore.memberId) {
      alert('請先登入會員')
      return
    }

    const orderId = await cartStore.checkout()
    console.log('checkoutCart OrderID:', orderId)
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
        <!-- 🩶 空購物車 -->
        <div v-if="cartItems.length === 0" class="text-center py-5 text-muted fs-5">
          購物車是空的
        </div>

        <!-- 🟦 有商品 -->
        <div v-else>
          <div class="cart-list mb-4">
            <div
              v-for="item in cartItems"
              :key="item.itemId || item.book.id"
              class="cart-item d-flex justify-content-between align-items-center border-bottom py-3"
            >
              <div class="d-flex align-items-center gap-3 flex-grow-1">
                <img
                  :src="item.book.coverUrl && item.book.coverUrl.startsWith('http')
                        ? item.book.coverUrl
                        : `/api/BookImages/${item.book.id}/cover`"
                  :alt="item.book.title || 'Book Cover'"
                  @error="(e) => ((e.target as HTMLImageElement).src = '/placeholder.png')"
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

          <!-- 🧾 總金額區塊 -->
          <div class="summary-box mb-4">
            <div class="d-flex justify-content-between fs-5 mb-2">
              <span>🧺 總數量：</span>
              <strong>{{ totalItems }}</strong>
            </div>
            <div class="d-flex justify-content-between fs-5 mb-2">
              <span>💰 小計：</span>
              <strong>NT$ {{ totalPrice }}</strong>
            </div>

            <!-- 🎫 優惠券輸入 -->
            <div class="d-flex gap-2 align-items-center mb-3">
              <input
                v-model="couponCode"
                type="text"
                class="form-control"
                placeholder="輸入優惠碼"
              />
              <button class="btn btn-outline-primary" @click="applyCoupon">套用</button>
            </div>

            <!-- ✅ 新增：會員優惠券清單 -->
            <div class="member-coupons mb-3">
              <h6 class="fw-bold mb-2">🎟 你已領取的優惠券：</h6>

              <!-- 有優惠券 -->
              <ul v-if="memberCoupons.length > 0" class="list-group small">
                <li
                  v-for="c in memberCoupons"
                  :key="c.CouponId"
                  class="list-group-item d-flex justify-content-between align-items-center"
                >
                  <div>
                    <div class="fw-bold">{{ c.name }}</div>
                    <div class="text-muted small">
  有效期限：
  <span v-if="c.startAt && c.endAt">
    {{ c.startAt }} ~ {{ c.endAt }}
  </span>
  <span v-else>無期限</span>
</div>

                  </div>
                  <div class="text-end">
                    <span v-if="c.DiscountType === 0">折抵 NT$ {{ c.DiscountValue }}</span>
                    <span v-else>{{ c.DiscountValue }}% 折扣</span>
                    <button
                      class="btn btn-sm btn-outline-success ms-2"
                      @click="useCoupon(c)"
                    >
                      使用
                    </button>
                  </div>
                </li>
              </ul>

              <!-- 沒有優惠券 -->
              <p v-else class="text-muted small mb-0">
                尚未領取任何優惠券，請輸入代碼領取。
              </p>
            </div>

            <!-- 優惠券結果提示 -->
            <div v-if="discountInfo" class="text-success small ms-1">
              {{ discountInfo }}
            </div>

            <!-- 折扣金額 -->
            <div v-if="discountAmount > 0" class="d-flex justify-content-between fs-5 mt-2">
              <span>🎉 優惠折抵：</span>
              <strong class="text-success">-NT$ {{ discountAmount }}</strong>
            </div>

            <!-- 實付金額 -->
            <div class="d-flex justify-content-between fs-5 mt-2 border-top pt-2">
              <span>🧾 實付金額：</span>
              <strong class="text-danger fs-4">
                NT$ {{ totalPrice - discountAmount }}
              </strong>
            </div>
          </div>

          <!-- 按鈕列 -->
          <div class="d-flex justify-content-end gap-3">
            <button class="btn btn-outline-secondary px-4" @click="clearCart">
              清空購物車
            </button>
            <button class="btn btn-primary px-4" @click="checkoutCart">
              前往結帳
            </button>
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

.cart-list {
  overflow-y: auto;
  max-height: 600px;
}

.list-group-item {
  transition: all 0.2s ease;
}
.list-group-item:hover {
  background: #f0f9ff;
  transform: translateY(-2px);
}
.btn-outline-success {
  padding: 2px 8px;
  font-size: 0.8rem;
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
</style>
