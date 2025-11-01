<script setup lang="ts">
/* 用 Router 做頁內導頁，避免整頁重載 */
import { useRouter, RouterLink } from 'vue-router'
import { computed } from 'vue'

/* 登入狀態、登出 API */
import { authState } from '@/stores/auth'
import { logout } from '@/api/auth'

/* 取得 router 實體 */
const router = useRouter()

/* 登入 */
const isAuthed = computed(() => !!authState.user)

/* 統一用程式導頁 */
function go(path: string) {
  router.push(path)
}

/* 登出後用 router 導回 /login */
async function signout() {
  try {
    await logout()
  } catch {}
  router.push('/login')
}
</script>

<template>
  <div class="topbar">
    <div class="container row">
      <div class="left">
        <!-- 用 RouterLink：回首頁 -->
        <RouterLink class="brand" to="/">簿錄書城</RouterLink>
      </div>

      <div class="right">
        <!-- 已登入（前台） -->
        <template v-if="isAuthed">
          <span class="hi">Hi, {{ authState.user?.name }}</span>
          <!-- 用 RouterLink：會員中心 -->
          <RouterLink class="link" to="/member">會員中心</RouterLink>
          <!-- 用 @click 觸發登出 -->
          <button class="link btn" type="button" @click="signout">登出</button>
        </template>

        <!-- 未登入（前台） -->
        <template v-else>
          <!-- 用 RouterLink：登入／註冊 -->
          <RouterLink class="link" to="/login">登入</RouterLink>
          <RouterLink class="link" to="/register">註冊</RouterLink>
        </template>
      </div>
    </div>
  </div>
</template>

<style scoped>
.topbar {
  position: sticky;
  top: 0;
  z-index: 1000; /* ★ 確保不被 Banner/Carousel 蓋住 */
  background: #f8f9fa;
  border-bottom: 1px solid #e9ecef;
}
.container {
  max-width: 1200px;
  margin: 0 auto;
  padding: 8px 16px;
}
.row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
}
.brand {
  font-weight: 600;
  color: #222;
  text-decoration: none;
}
.right {
  display: flex;
  align-items: center;
  gap: 12px;
}
.link {
  color: #0d6efd;
  text-decoration: none;
  font-size: 14px;
}
.link:hover {
  text-decoration: underline;
}
.hi {
  color: #555;
  font-size: 14px;
}
.btn {
  background: none;
  border: none;
  padding: 0;
  cursor: pointer;
}
</style>
