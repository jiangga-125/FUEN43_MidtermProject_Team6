<!-- src/components/CouponSelector.vue -->
<script setup lang="ts">
import { ref, onMounted } from 'vue'
import http from '@/lib/http'

const coupons = ref<any[]>([]) // ✅ 會員已領取的優惠券清單
const inputCode = ref('')
const message = ref('')
const isSuccess = ref(false)
const subtotal = 1000 // 模擬購物金額（實務上可由父層傳入）

// ✅ 取得會員已領取的優惠券
async function loadMemberCoupons() {
  try {
    const res = await http.get('/api/MemberCouponsApi/List')
    coupons.value = res.data?.usable ?? []
  } catch (e) {
    console.error('loadMemberCoupons error', e)
    coupons.value = []
  }
}

// ✅ 使用代碼領取優惠券
async function claimByCode() {
  message.value = ''
  if (!inputCode.value.trim()) {
    message.value = '❌ 請輸入優惠代碼'
    isSuccess.value = false
    return
  }

  try {
    const res = await http.post('/api/MemberCouponsApi/ClaimByCode', {
      Code: inputCode.value.trim(),
    })
    message.value = `✅ ${res.data.message || '成功領取優惠券！'}`
    isSuccess.value = true
    inputCode.value = ''
    await loadMemberCoupons() // 重新載入會員已領券清單
  } catch (err: any) {
    message.value = `❌ ${err.response?.data?.message || '領取失敗'}`
    isSuccess.value = false
  }
}

// ✅ 套用優惠券折扣（由會員已領取的清單）
async function applyCoupon(code: string) {
  try {
    const res = await http.post('/api/Store/CouponsApi/Apply', {
      code,
      subtotal,
    })
    message.value = `✅ 套用成功！折抵 ${res.data.data.discount} 元 (${res.data.data.rule})`
    isSuccess.value = true
  } catch (err: any) {
    message.value = `❌ ${err.response?.data?.message || '套用失敗'}`
    isSuccess.value = false
  }
}

// ✅ 初始化
onMounted(() => loadMemberCoupons())
</script>

<template>
  <div class="coupon-box p-4 rounded shadow-md bg-white w-full max-w-lg mx-auto">
    <h3 class="text-lg font-bold mb-3">🎟️ 我的優惠券</h3>

    <!-- 🔹 輸入優惠代碼 -->
    <div class="flex mb-3 gap-2">
      <input
        v-model="inputCode"
        placeholder="輸入優惠代碼"
        class="flex-1 border rounded px-3 py-2"
      />
      <button @click="claimByCode" class="bg-blue-600 text-white px-4 py-2 rounded">
        領取
      </button>
    </div>

    <!-- 提示訊息 -->
    <div
      v-if="message"
      :class="`p-2 rounded text-sm mt-1 ${
        isSuccess ? 'bg-green-100 text-green-700' : 'bg-red-100 text-red-700'
      }`"
    >
      {{ message }}
    </div>

    <hr class="my-4" />

    <!-- 🔹 已領取的優惠券清單 -->
    <h4 class="font-semibold mb-2">可使用的優惠券</h4>

    <div v-if="coupons.length === 0" class="text-gray-500 text-sm">
      尚未領取任何優惠券。請先輸入優惠代碼領取。
    </div>

    <div class="grid grid-cols-1 md:grid-cols-2 gap-3">
      <div
        v-for="c in coupons"
        :key="c.couponID"
        @click="applyCoupon(c.code)"
        class="border rounded p-3 hover:bg-blue-50 cursor-pointer transition"
      >
        <div class="font-bold">{{ c.name }}</div>
        <div class="text-sm text-gray-500">代碼：{{ c.code }}</div>
        <div class="text-sm text-blue-600 mt-1">
          折扣：{{ c.discountValue }}{{ c.discountType === 0 ? '元' : '%' }}
        </div>
        <div class="text-xs text-gray-400 mt-1">
          有效期限：{{ new Date(c.startAt).toLocaleDateString() }} -
          {{ new Date(c.endAt).toLocaleDateString() }}
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.coupon-box {
  border: 1px solid #e0e0e0;
}
</style>
