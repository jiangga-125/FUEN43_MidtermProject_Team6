<template>
  <div class="container py-5">
    <h1>建立評論</h1>

    <!-- ✅ 成功訊息 -->
    <div v-if="message" class="alert alert-success">
      {{ message }}
    </div>

    <!-- 🧾 評論表單 -->
    <form @submit.prevent="submitReview">

      <!-- 🧍‍♂️ 顯示目前登入會員 -->
      <div class="mb-3">
        <label class="form-label">會員 ID</label>
        <input
          v-model="form.memberId"
          type="text"
          class="form-control"
          readonly
        />
      </div>

      <!-- 📚 書名（自動載入購買過的書籍） -->
      <div class="mb-3">
        <label class="form-label">選擇書籍</label>
        <select v-model="form.targetBookId" class="form-select">
          <option value="">-- 請選擇您購買過的書籍 --</option>
          <option v-for="b in purchasedBooks" :key="b.value" :value="b.value">
            {{ b.text }}
          </option>
        </select>
      </div>

      <!-- 🌟 評分 -->
      <div class="mb-3">
        <label class="form-label d-block">評分</label>
        <div class="star-rating">
          <i
            v-for="n in 5"
            :key="n"
            class="bi"
            :class="n <= hoverRating || n <= form.rating ? 'bi-star-fill text-warning active' : 'bi-star text-secondary'"
            @mouseover="hoverRating = n"
            @mouseleave="hoverRating = 0"
            @click="selectRating(n)"
          ></i>
        </div>

        <!-- ⭐ 提示文字 -->
        <div v-if="hoverRating || form.rating" class="rating-hint mt-1 text-muted">
          {{ ratingTexts[hoverRating || form.rating - 1] }}
        </div>
      </div>

      <!-- 💬 評論內容 -->
      <div class="mb-3">
        <label class="form-label">評論內容</label>
        <textarea
          v-model="form.content"
          class="form-control"
          rows="4"
          placeholder="請輸入評論內容"
        ></textarea>
      </div>

      <!-- 🚀 送出按鈕 -->
      <button type="submit" class="btn btn-primary">送出評論</button>
    </form>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import http from '@/lib/http'  // ✅ 改用內建 JWT axios 實例（自動帶 token）

// ✅ 表單資料
const form = ref({
  memberId: '',          // 只顯示，不送出
  targetBookId: '',
  rating: 0,
  content: ''
})

const message = ref('')
const hoverRating = ref(0)
const purchasedBooks = ref<{ value: number; text: string }[]>([])
const ratingTexts = ['非常不滿意 😡', '不太滿意 😕', '普通 🙂', '滿意 😊', '非常滿意 🤩']

// ⭐ 點擊星星動畫
const selectRating = (n: number) => {
  form.value.rating = n
  const stars = document.querySelectorAll('.star-rating .bi')
  const selectedStar = stars[n - 1]
  if (selectedStar) {
    selectedStar.classList.add('pop')
    setTimeout(() => selectedStar.classList.remove('pop'), 300)
  }
}

// ✅ 取得目前登入會員ID + 載入書籍
onMounted(async () => {
  try {
    // 1️⃣ 從 whoami 取得登入會員資訊（JWT 驗證）
    const who = await http.get('/api/MemberCouponsApi/WhoAmI')

    // whoami 回傳格式：{ isAuth, user, claims: [{type, value}, ...] }
    const idClaim = who.data.claims.find((c: any) =>
      ['mid', 'memberId', 'MemberId', 'MemberID'].includes(c.type)
    )
    if (idClaim) {
      form.value.memberId = idClaim.value
      console.log('🧍‍♂️ 當前登入會員 ID:', form.value.memberId)
    } else {
      console.warn('⚠️ 無法從 token 取得會員ID')
    }

    // 2️⃣ 取得會員購買過的書籍
    const res = await http.get('/api/ReviewsApi/GetPurchasedBooks')
    purchasedBooks.value = res.data.data.map((b: any) => ({
      value: b.bookId,
      text: b.title
    }))
  } catch (err) {
    console.error('❌ 載入會員或書籍資料失敗', err)
  }
})

// 📤 送出評論（memberId 不送出）
const submitReview = async () => {
  try {
    const payload = {
      targetBookId: form.value.targetBookId,
      rating: form.value.rating,
      content: form.value.content
    }

    const res = await http.post('/api/ReviewsApi/Create', payload)
    message.value = res.data.message || '✅ 評論已送出，等待管理員審核。'

    // 清空表單
    form.value.targetBookId = ''
    form.value.rating = 0
    form.value.content = ''
  } catch (err: any) {
    console.error('❌ 送出評論失敗', err)
    message.value = '❌ 送出失敗：' + (err.response?.data?.message || err.message)
  }
}
</script>

<style scoped>
.container {
  max-width: 600px;
}

/* 🌟 星星樣式與動畫 */
.star-rating {
  font-size: 2rem;
  cursor: pointer;
}
.star-rating .bi.pop {
  animation: popStar 0.3s ease;
}
@keyframes popStar {
  0% { transform: scale(1); }
  40% { transform: scale(1.4); }
  100% { transform: scale(1); }
}
</style>
