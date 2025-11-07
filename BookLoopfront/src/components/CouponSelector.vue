<!-- src/components/CouponSelector.vue -->
<script setup lang="ts">
import { ref, onMounted } from 'vue'
// import axios from 'axios'
import http from '@/lib/http'

const coupons = ref<any[]>([])
const inputCode = ref('')
const message = ref('')
const isSuccess = ref(false)
const subtotal = 1000 // 模擬購物金額
// const memberId = 1 // 模擬登入會員

// 取得可用優惠券
async function loadCoupons() {
  try {
    const res = await http.get('/api/CouponsApi/List') // 後端應從 token 取 memberId
    // 若後端包成 { data } 或直接回陣列，兩種都兼容
    coupons.value = res.data?.data ?? res.data ?? []
  } catch (e) {
    console.error('loadCoupons error', e)
    coupons.value = []
  }
}
// 套用輸入框的優惠碼
async function applyCode() {
  if (!inputCode.value.trim()) return
  await applyCoupon(inputCode.value.trim())
}

// 套用優惠券（從清單或輸入）
async function applyCoupon(code: string) {
  try {
    const res = await http.post('/api/Store/CouponsApi/Apply', {
      code,
      subtotal,
    })
    message.value = `✅ 套用成功！折抵 ${res.data.discount} 元 (${res.data.rule})`
    isSuccess.value = true
  } catch (err: any) {
    message.value = `❌ ${err.response?.data?.message || '套用失敗'}`
    isSuccess.value = false
  }
}

onMounted(() => loadCoupons())
</script>

<template>
  <div class="coupon-box p-4 rounded shadow-md bg-white w-full max-w-lg mx-auto">
    <h3 class="text-lg font-bold mb-3">🎟️ 套用優惠券</h3>

    <!-- 輸入優惠碼 -->
    <div class="flex mb-3 gap-2">
      <input v-model="inputCode" placeholder="輸入優惠碼" class="flex-1 border rounded px-3 py-2" />
      <button @click="applyCode" class="bg-blue-600 text-white px-4 py-2 rounded">套用</button>
    </div>

    <!-- 結果提示 -->
    <div
      v-if="message"
      :class="`p-2 rounded ${isSuccess ? 'bg-green-100 text-green-700' : 'bg-red-100 text-red-700'}`"
    >
      {{ message }}
    </div>

    <hr class="my-4" />

    <!-- 可用優惠券清單 -->
    <h4 class="font-semibold mb-2">可用優惠券</h4>
    <div v-if="coupons.length === 0" class="text-gray-500">目前沒有可用優惠券</div>

    <div class="grid grid-cols-1 md:grid-cols-2 gap-3">
      <div
        v-for="c in coupons"
        :key="c.couponID"
        @click="applyCoupon(c.code)"
        class="border rounded p-3 hover:bg-blue-50 cursor-pointer transition"
      >
        <div class="font-bold">{{ c.name }}</div>
        <div class="text-sm text-gray-500">{{ c.code }}</div>
        <div class="text-sm text-blue-600 mt-1">
          折扣：{{ c.discountValue }}{{ c.discountType === 0 ? '元' : '%' }}
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
