  <!-- src/views/MemberCoupons.vue -->
  <script setup lang="ts">
  import { ref, onMounted } from 'vue'
  import http from '@/lib/http' //使用現有的 axios instance

  const loadingAvailable = ref(false)
  const availableCoupons = ref<Array<any>>([])
  const usable = ref<Array<any>>([])
  const used = ref<Array<any>>([])
  const expired = ref<Array<any>>([])
  const tab = ref('usable')
  //const loading = ref(false)
  //const msg = ref('')
  const code = ref('')
  const claimMsg = ref('')

function unwrapData(res: any) {
  // 若伺服器回 { success, data }，就回 data；否則回 res（可能直接是陣列）
  if (res && typeof res === 'object' && 'success' in res) {
    return res.data ?? null
  }
  return res
}


  async function loadAvailableCoupons() {
  loadingAvailable.value = true
  try {
    const res = await http.get('/api/MemberCouponsApi/available')
    const data = unwrapData(res.data)
    availableCoupons.value = Array.isArray(data) ? data : data?.items || []
  } catch (e: any) {
    console.error('loadAvailableCoupons error', e)
    availableCoupons.value = []
  } finally {
    loadingAvailable.value = false
  }
}


  async function loadCoupons() {
  try {
    const res = await http.get('/api/MemberCouponsApi/list')
    const data = unwrapData(res.data)
    // 假設後端 data = { usable:[], used:[], expired:[] }
    usable.value = data?.usable || []
    used.value = data?.used || []
    expired.value = data?.expired || []
  } catch (e: any) {
    console.error('loadCoupons error', e)
    usable.value = used.value = expired.value = []
  }
}


  async function claimByCode() {
    claimMsg.value = ''
    if (!code.value.trim()) {
      claimMsg.value = '請輸入優惠代碼'
      return
    }
     try {
    const payload = { code: code.value.trim() }
    const res = await http.post('/api/MemberCouponsApi/claimbycode', payload)
    const data = unwrapData(res.data)
    // 假設 success:true 時後端回 { success:true, data:{ message: '...' } } 或 { success:true, message: '...' }
    const message = data?.message ?? res.data?.message ?? '領取成功'
    claimMsg.value = '✅ ' + message
    code.value = ''
    await loadCoupons()
    await loadAvailableCoupons()
  } catch (e: any) {
    console.error('claimByCode error', e)
    claimMsg.value = e?.response?.data?.message || e.message || '領取失敗'
  }
}


  async function claimCoupon(couponId: number) {
  if (!confirm('確定要領取這張優惠券嗎？')) return
  try {
    const res = await http.post('/api/MemberCouponsApi/claim', { couponID: couponId })
    const data = unwrapData(res.data)
    const message = data?.message ?? res.data?.message ?? '領取成功'
    alert('🎉 ' + message)
    await loadAvailableCoupons()
    await loadCoupons()
  } catch (e: any) {
    console.error('claimCoupon error', e)
    alert(e?.response?.data?.message || e.message || '領取失敗')
  }
}


  onMounted(() => {
    loadAvailableCoupons()
    loadCoupons()
  })
  </script>

  <template>
  <div class="coupons-page">
    <h2>🎟️ 我的優惠券</h2>

    <!-- 🔹 返回會員中心 -->
    <div class="top-nav">
      <router-link to="/member" class="link">返回會員中心</router-link>
    </div>

    <!-- 🩵 優惠代碼輸入框 -->
    <div class="claim-box">
      <h3>輸入優惠代碼</h3>
      <div class="row">
        <input v-model="code" placeholder="請輸入優惠代碼" />
        <button class="btn primary" @click="claimByCode">領取</button>
      </div>
      <transition name="fade">
        <p v-if="claimMsg" class="msg" :class="{ success: claimMsg.startsWith('✅'), error: claimMsg.startsWith('❌') }">
          {{ claimMsg }}
        </p>
      </transition>
    </div>

    <!-- 🟡 可領取優惠券 -->
    <div class="card">
      <h3>可領取優惠券</h3>
      <div v-if="loadingAvailable" class="text-muted">載入中...</div>
      <div v-else-if="!availableCoupons.length" class="text-muted">目前沒有可領取的優惠券。</div>
      <div v-else class="coupon-list">
        <div
          v-for="c in availableCoupons"
          :key="c.couponID"
          class="coupon-card claimable"
        >
          <div class="value">
            {{ c.discountType === 0 ? `＄${c.discountValue}` : `${c.discountValue}%` }}
          </div>
          <div class="info">
            <div class="name">{{ c.name }}</div>
            <div class="date">
                            {{ new Date(c.startAt).toLocaleDateString() }} -
              {{ new Date(c.endAt).toLocaleDateString() }}
            </div>
            <button class="btn small primary" @click="claimCoupon(c.couponId)">領取</button>
          </div>
        </div>
      </div>
    </div>

    <!--  我的優惠券 -->
    <div class="card">
      <h3>我的優惠券</h3>
      <ul class="tabs">
        <li :class="{ active: tab === 'usable' }" @click="tab='usable'">可使用</li>
        <li :class="{ active: tab === 'used' }" @click="tab='used'">已使用</li>
        <li :class="{ active: tab === 'expired' }" @click="tab='expired'">已逾期</li>
      </ul>

      <transition name="fade" mode="out-in">
        <div :key="tab">
          <div v-if="tab === 'usable'" class="coupon-list">
            <p v-if="!usable.length" class="text-muted">目前沒有可使用的優惠券。</p>
            <div v-for="c in usable" :key="c.memberCouponID" class="coupon-card usable">
              <div class="value">
                {{ c.discountType === 0 ? `＄${c.discountValue}` : `${c.discountValue}%` }}
              </div>
              <div class="info">
                <div class="name">{{ c.name }}</div>
                <div class="date">              {{ new Date(c.startAt).toLocaleDateString() }} -
              {{ new Date(c.endAt).toLocaleDateString() }}</div>
              </div>
            </div>
          </div>

          <div v-if="tab==='used'" class="coupon-list">
            <p v-if="!used.length" class="text-muted">目前沒有已使用的優惠券。</p>
            <div v-for="c in used" :key="c.memberCouponID" class="coupon-card used">
              <div class="value">已使用</div>
              <div class="info"><div class="name">{{ c.name }}</div></div>
            </div>
          </div>

          <div v-if="tab === 'expired'" class="coupon-list">
            <p v-if="!expired.length" class="text-muted">目前沒有已逾期的優惠券。</p>
            <div v-for="c in expired" :key="c.memberCouponID" class="coupon-card expired">
              <div class="value">已逾期</div>
              <div class="info">
                <div class="name">{{ c.name }}</div>
                <div class="date">             {{ new Date(c.startAt).toLocaleDateString() }} -
             {{ new Date(c.endAt).toLocaleDateString() }}</div>
              </div>
            </div>
          </div>
        </div>
      </transition>
    </div>
  </div>
</template>

  <style scoped>
/* 🎨 全頁樣式 */
.coupons-page {
  max-width: 900px;
  margin: 62px auto;
  padding: 0 16px;
  color: #333;
  font-family: "Noto Sans TC", "微軟正黑體", sans-serif;
}

/* 🧾 標題 */
h2 {
  font-size: 1.8em;
  font-weight: 700;
  margin-bottom: 20px;
  color: #1e3a8a;
  border-left: 6px solid #3b82f6;
  padding-left: 12px;
}

/* 🔗 返回連結 */
.top-nav {
  margin-bottom: 20px;
}
.link {
  color: #2563eb;
  text-decoration: none;
  font-weight: 500;
}
.link:hover {
  text-decoration: underline;
}

/* 🩵 輸入框卡片 */
.claim-box {
  background: #f8fafc;
  border: 1px solid #dbeafe;
  border-left: 6px solid #3b82f6;
  border-radius: 12px;
  padding: 20px;
  margin-bottom: 24px;
  box-shadow: 0 2px 8px rgba(59, 130, 246, 0.08);
}
.claim-box h3 {
  font-size: 1.2em;
  color: #1e3a8a;
  margin-bottom: 10px;
}
.claim-box .row {
  display: flex;
  gap: 10px;
}
.claim-box input {
  flex: 1;
  padding: 10px;
  border: 1px solid #cbd5e1;
  border-radius: 8px;
  font-size: 1em;
}
.msg {
  margin-top: 10px;
  font-weight: 600;
}
.msg.success {
  color: #16a34a;
}
.msg.error {
  color: #dc2626;
}

/* 🧡 卡片區塊 */
.card {
  background: #fff;
  border: 1px solid #e2e8f0;
  border-radius: 12px;
  padding: 20px;
  margin-bottom: 24px;
  box-shadow: 0 2px 6px rgba(0, 0, 0, 0.05);
}

/* 🔖 Tabs */
.tabs {
  display: flex;
  gap: 8px;
  border-bottom: 2px solid #e5e7eb;
  margin-bottom: 12px;
}
.tabs li {
  list-style: none;
  padding: 8px 16px;
  cursor: pointer;
  border-radius: 8px 8px 0 0;
  background: #f3f4f6;
  transition: 0.2s;
  font-weight: 500;
}
.tabs li.active {
  background: #3b82f6;
  color: white;
}

/* 🎫 優惠券卡片 */
.coupon-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.coupon-card {
  display: flex;
  align-items: center;
  border: 1px solid #e5e7eb;
  border-left: 6px solid #ccc;
  border-radius: 10px;
  padding: 14px 16px;
  background: #fafafa;
  transition: all 0.25s ease;
}
.coupon-card:hover {
  transform: translateY(-2px);
  box-shadow: 0 3px 10px rgba(0, 0, 0, 0.08);
}
.coupon-card .value {
  font-size: 1.8em;
  min-width: 70px;
  text-align: center;
  font-weight: bold;
}
.coupon-card .info {
  margin-left: 12px;
}

/* 顏色區分 */
.coupon-card.usable {
  border-left-color: #16a34a;
  background: #f0fdf4;
}
.coupon-card.used {
  border-left-color: #3b82f6;
  background: #eff6ff;
}
.coupon-card.expired {
  border-left-color: #f87171;
  background: #fef2f2;
}
.coupon-card.claimable {
  border-left-color: #f59e0b;
  background: #fffbea;
}

/* 按鈕 */
.btn {
  cursor: pointer;
  border: none;
  border-radius: 6px;
  font-size: 0.95em;
  transition: all 0.2s;
}
.btn.primary {
  background: #3b82f6;
  color: #fff;
  padding: 8px 14px;
}
.btn.primary:hover {
  background: #2563eb;
}
.btn.small {
  padding: 6px 10px;
}

/* 動畫 */
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.3s;
}
.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}
</style>
