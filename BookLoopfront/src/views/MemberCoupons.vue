  <!-- src/views/MemberCoupons.vue -->
  <script setup lang="ts">
  import { ref, onMounted } from 'vue'

  const loadingAvailable = ref(false)
  const availableCoupons = ref<Array<any>>([])
  const usable = ref<Array<any>>([])
  const used = ref<Array<any>>([])
  const expired = ref<Array<any>>([])
  const tab = ref('usable')
  const loading = ref(false)
  const msg = ref('')
  const code = ref('')
  const claimMsg = ref('')

  async function loadAvailableCoupons() {
    loadingAvailable.value = true
    try {
      const res = await fetch('/api/members/membercouponsapi/available', { credentials: 'include' })
      if (!res.ok) throw new Error('無法載入可領取優惠券')
      availableCoupons.value = await res.json()
    } catch (e) {
      console.error(e)
    } finally {
      loadingAvailable.value = false
    }
  }

  async function loadCoupons() {
    try {
      const res = await fetch('/api/members/membercouponsapi/list', { credentials: 'include' })
      if (!res.ok) throw new Error('無法載入優惠券')
      const data = await res.json()
      usable.value = data.usable || []
      used.value = data.used || []
      expired.value = data.expired || []
    } catch (e) {
      console.error(e)
    }
  }

  async function claimByCode() {
    claimMsg.value = ''
    if (!code.value.trim()) {
      claimMsg.value = '請輸入優惠代碼'
      return
    }
    try {
      const res = await fetch('/api/members/membercouponsapi/claimbycode', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ code: code.value })
      })
      const data = await res.json()
      if (!res.ok) throw new Error(data.message)
      claimMsg.value = '✅ ' + data.message
      code.value = ''
      loadCoupons()
    } catch (e: any) {
      claimMsg.value = e.message || '領取失敗'
    }
  }

  async function claimCoupon(couponId: number) {
    if (!confirm('確定要領取這張優惠券嗎？')) return
    try {
      const res = await fetch('/api/members/membercouponsapi/claim', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ couponID: couponId })
      })
      const data = await res.json()
      if (!res.ok) throw new Error(data.message || '領取失敗')
      alert('🎉 ' + data.message)
      loadAvailableCoupons()
      loadCoupons()
    } catch (e: any) {
      alert(e.message || '領取失敗')
    }
  }

  onMounted(() => {
    loadAvailableCoupons()
    loadCoupons()
  })
  </script>

  <template>
    <div class="coupons-page">
      <h2>我的優惠券</h2>
      
      <!-- 導覽 -->
      <div class="top-nav">
        <router-link to="/member" class="link">返回會員中心</router-link>
      </div>

  <div class="claim-box">
        <h3>輸入優惠代碼</h3>
        <div class="row">
          <input v-model="code" placeholder="請輸入優惠代碼，如 BOOK50" />
          <button class="btn primary" @click="claimByCode">領取</button>
        </div>
        <p v-if="claimMsg" class="msg">{{ claimMsg }}</p>
      </div>


      <!-- 可領取優惠券 -->
      <div class="card">
        <h3>可領取優惠券</h3>
        <div v-if="loadingAvailable" class="text-muted">載入中...</div>
        <div v-else-if="!availableCoupons.length" class="text-muted">目前沒有可領取的優惠券。</div>
        <div v-else class="coupon-list">
          <div v-for="c in availableCoupons" :key="c.couponID" class="coupon-card claimable">
            <div class="value">{{ c.discountType === 0 ? `＄${c.discountValue}` : `${c.discountValue}%` }}</div>
            <div class="info">
              <div class="name">{{ c.name }}</div>
              <div class="date">
                {{ new Date(c.startAt).toLocaleDateString() }} - {{ new Date(c.endAt).toLocaleDateString() }}
              </div>
              <button class="btn primary" @click="claimCoupon(c.couponID)">領取</button>
            </div>
          </div>
        </div>
      </div>

      <!-- 我的優惠券 -->
      <div class="card">
        <h3>我的優惠券</h3>
        <ul class="tabs">
          <li :class="{ active: tab==='usable' }" @click="tab='usable'">可使用</li>
          <li :class="{ active: tab==='used' }" @click="tab='used'">已使用</li>
          <li :class="{ active: tab==='expired' }" @click="tab='expired'">已逾期</li>
        </ul>

        <div v-if="tab==='usable'" class="coupon-list">
          <p v-if="!usable.length" class="text-muted">目前沒有可使用的優惠券。</p>
          <div v-for="c in usable" :key="c.memberCouponID" class="coupon-card usable">
            <div class="value">{{ c.discountType === 0 ? `＄${c.discountValue}` : `${c.discountValue}%` }}</div>
            <div class="info">
              <div class="name">{{ c.name }}</div>
              <div class="date">{{ new Date(c.startAt).toLocaleDateString() }} - {{ new Date(c.endAt).toLocaleDateString() }}</div>
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

        <div v-if="tab==='expired'" class="coupon-list">
          <p v-if="!expired.length" class="text-muted">目前沒有已逾期的優惠券。</p>
          <div v-for="c in expired" :key="c.memberCouponID" class="coupon-card expired">
            <div class="value">已逾期</div>
            <div class="info">
              <div class="name">{{ c.name }}</div>
              <div class="date">{{ new Date(c.startAt).toLocaleDateString() }} - {{ new Date(c.endAt).toLocaleDateString() }}</div>
            </div>
          </div>
        </div>
      </div>
    </div>
  </template>

  <style scoped>
  .coupons-page { max-width: 900px; margin: 24px auto; padding: 0 16px; }
  .card { background: #fff; border: 1px solid #e9ecef; border-radius: 12px; padding: 16px; margin-bottom: 16px; }
  h2 { margin-bottom: 16px; }
  h3 { margin-bottom: 10px; }

  .top-nav { margin-bottom: 12px; }
  .link { color: #2563eb; text-decoration: none; }
  .link:hover { text-decoration: underline; }

  .tabs {
    display: flex;
    gap: 10px;
    border-bottom: 1px solid #eee;
    margin-bottom: 10px;
  }
  .tabs li {
    list-style: none;
    padding: 8px 14px;
    cursor: pointer;
    border-radius: 8px 8px 0 0;
    background: #f9f9f9;
    transition: 0.2s;
  }
  .tabs li.active {
    background: #fff;
    border: 1px solid #ddd;
    border-bottom: none;
    font-weight: 600;
  }

  .coupon-list { display: flex; flex-direction: column; gap: 10px; }
  .coupon-card { display: flex; align-items: center; border: 1px solid #f0f0f0; border-left: 6px solid #ccc; border-radius: 10px; padding: 10px 12px; background: #fafafa; }
  .coupon-card .value { font-size: 1.8em; min-width: 60px; text-align: center; font-weight: bold; }
  .coupon-card .info { margin-left: 10px; }

  .coupon-card.usable { border-left: 6px solid #16a34a; background: #f0fdf4; }
  .coupon-card.used { border-left: 6px solid #3b82f6; background: #eff6ff; }
  .coupon-card.expired { border-left: 6px solid #f87171; background: #fef2f2; }
  .coupon-card.claimable { border-left: 6px solid #f59e0b; background: #fffbea; }

  .btn.primary { background: #0d6efd; color: #fff; border: none; border-radius: 6px; padding: 6px 10px; margin-top: 6px; }
  </style>
