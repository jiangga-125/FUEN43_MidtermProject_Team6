<template>
  <div class="review-container container my-5 p-4 shadow rounded bg-white">
    <!-- 🔹 標題區 -->
    <h2 class="mb-4 text-center text-primary fw-bold">
      ✍️ 建立書籍評論
    </h2>

    <!-- ✅ 成功訊息 -->
    <div v-if="message" class="alert alert-success text-center">
      {{ message }}
    </div>

    <!-- ⚠️ 錯誤訊息 -->
    <div v-if="error" class="alert alert-danger text-center">
      {{ error }}
    </div>

    <!-- 🧾 評論表單 -->
    <form @submit.prevent="submitReview" class="mt-4">
      

      <!-- 👤 會員 ID -->
      <div class="mb-3">
  <label class="form-label">會員名稱</label>
  <input
    type="text"
    :value="auth.member?.name || '未登入'"
    class="form-control"
    disabled
  />
</div>


      <!-- 📚 書名 -->
      <div class="mb-3">
        <label class="form-label fw-bold">
          <i class="bi bi-book me-1"></i>選擇書籍
        </label>
        <select
          v-model.number="form.TargetBookID"
          class="form-select"
          :disabled="loadingBooks"
        >
          <option value="">-- 請選擇您購買過的書籍 --</option>
          <option
            v-for="b in purchasedBooks"
            :key="b.value"
            :value="b.value"
          >
            {{ b.text }}
          </option>
        </select>
        <div v-if="loadingBooks" class="text-muted mt-1 small">
          ⏳ 載入中...
        </div>
      </div>

      <!-- 🌟 評分 -->
      <div class="mb-3">
        <label class="form-label fw-bold d-block">
          <i class="bi bi-star me-1"></i>評分
        </label>
        <div class="star-rating">
          <i
            v-for="n in 5"
            :key="n"
            class="bi"
            :class="[
              n <= hoverRating || n <= form.rating
                ? 'bi-star-fill text-warning active'
                : 'bi-star text-secondary'
            ]"
            @mouseover="hoverRating = n"
            @mouseleave="hoverRating = 0"
            @click="selectRating(n)"
          ></i>
        </div>
        <div v-if="hoverRating || form.rating" class="rating-hint mt-2 text-muted">
          {{ ratingTexts[(hoverRating || form.rating) - 1] }}
        </div>
      </div>

      <!-- 💬 評論內容 -->
      <div class="mb-4">
        <label class="form-label fw-bold">
          <i class="bi bi-chat-left-dots me-1"></i>評論內容
        </label>
        <textarea
          v-model="form.content"
          class="form-control"
          rows="4"
          placeholder="請輸入您對書籍的看法、心得或建議"
        ></textarea>
      </div>

      <!-- 🚀 送出按鈕 -->
      <div class="text-center">
        <button
          type="submit"
          class="btn btn-primary px-4 py-2"
          :disabled="submitting"
        >
          <i class="bi" :class="submitting ? 'bi-hourglass-split' : 'bi-send'"></i>
          {{ submitting ? '送出中...' : '送出評論' }}
        </button>
      </div>
    </form>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import http from '@/lib/http'
import { useAuth } from '@/stores/auth'

const auth = useAuth()

const form = ref({
  memberId: '',
  TargetBookID: '',
  rating: 0,
  content: ''
})

const message = ref('')
const error = ref('')
const submitting = ref(false)
const loadingBooks = ref(false)

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

// 🚀 初始載入
onMounted(async () => {
  try {
    await auth.tryLoadSession()

    if (!auth.member?.memberId) {
      error.value = '⚠️ 尚未登入，請先登入會員。'
      return
    }

    form.value.memberId = auth.member.memberId.toString()

    loadingBooks.value = true
    const res = await http.get(`/api/ReviewsApi/GetPurchasedBooks/${form.value.memberId}`)

    console.log('📦 後端回傳內容：', res.data)

    const data = Array.isArray(res.data)
      ? res.data
      : Array.isArray(res.data.data)
      ? res.data.data
      : []

    purchasedBooks.value = data.map((b: any) => ({
      value: b.bookId ?? b.BookID ?? b.bookID,
      text: b.title ?? b.Title ?? '(未命名書籍)'
    }))
  } catch (error) {
    console.error('Error loading purchased books:', error)
  } finally {
    loadingBooks.value = false
  }
})

// 📤 送出評論
const submitReview = async () => {
  console.log("🔍 目前送出的表單內容：", form.value)
  if (!form.value.TargetBookID || !form.value.content || !form.value.rating) {
    error.value = '請填寫所有必填欄位。'
    return
  }

// 🔸 字數長度驗證（少於 10 個字就擋下）
  if (form.value.content.trim().length < 10) {
    error.value = '⚠️ 評論內容不能少於 10 個字。'
    return
  }

  submitting.value = true
  error.value = ''
  message.value = ''

  try {
   const payload = {
  MemberID: parseInt(form.value.memberId), // ✅ 首字母大寫
  TargetBookID: Number(form.value.TargetBookID),
  Rating: form.value.rating,
  Content: form.value.content
}


    const res = await http.post('/api/ReviewsApi/Create', payload)
    message.value = res.data.message || '✅ 評論已送出，等待管理員審核。'

    // 清空表單
    form.value.TargetBookID = ''
    form.value.rating = 0
    form.value.content = ''
  } catch (err: any) {
    console.error('❌ 送出評論失敗', err)
    error.value = '❌ 送出失敗：' + (err.response?.data?.message || err.message)
  } finally {
    submitting.value = false
  }
  
}
</script>

<style scoped>
.review-container {
  max-width: 650px;
}

/* 🌟 星星評分 */
.star-rating {
  font-size: 2rem;
  cursor: pointer;
  display: flex;
  justify-content: center;
  gap: 8px;
}
.star-rating .bi {
  transition: transform 0.2s, color 0.2s;
}
.star-rating .bi.active {
  transform: scale(1.2);
}
.star-rating .bi.pop {
  animation: popStar 0.3s ease;
}
@keyframes popStar {
  0% { transform: scale(1); }
  40% { transform: scale(1.4); }
  100% { transform: scale(1); }
}

/* 🎨 表單風格 */
textarea.form-control {
  resize: none;
}
.btn-primary {
  font-weight: 600;
  border-radius: 8px;
  box-shadow: 0 3px 6px rgba(0, 0, 0, 0.15);
}
</style>
