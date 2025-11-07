<!-- src/components/TopBar.vue -->
<script setup lang="ts">
import { RouterLink, useRouter } from 'vue-router'
import { storeToRefs } from 'pinia'
import { useAuth } from '@/stores/auth'

const router = useRouter()
const auth = useAuth()
const { member, isMemberLoggedIn } = storeToRefs(auth)

async function signout() {
  try { await auth.logout() } finally { router.push('/login') }
}
</script>

<template>
  <div class="topbar">
    <div class="container">
      <!-- 🩵 左邊：Logo + 標題 -->
      <RouterLink class="brand" to="/">
       <img src="@/assets/banner2.png" alt="BookLoop Logo" class="logo">
        <span class="brand-text">簿錄書城</span>
      </RouterLink>

      <!-- 💙 右邊：登入註冊等按鈕 -->
      <div class="right">
        <template v-if="isMemberLoggedIn">
          <span class="hi">Hi, {{ member?.name || member?.email }}</span>
          <RouterLink class="link" to="/member">會員中心</RouterLink>
          <RouterLink class="link" :to="{ name: 'BRcenter' }">二手書紀錄</RouterLink>
          <router-link class="link" to="/member/coupons">優惠券</router-link>
          <button class="link btn" type="button" @click="signout">登出</button>
        </template>

        <template v-else>
          <RouterLink class="link" to="/login">登入</RouterLink>
          <RouterLink class="link" to="/register">註冊</RouterLink>
        </template>
      </div>
    </div>
  </div>
</template>

<style scoped>
/* ====== 📌 整體固定在頂部 ====== */
.topbar {
  position: fixed;
  top: 0;
  left: 0;
  width: 100%;
  background: #f8f9fa;
  border-bottom: 1px solid #e9ecef;
  z-index: 1000;
  box-shadow: 0 2px 6px rgba(0, 0, 0, 0.05);
}

/* ====== 📦 容器設定 ====== */
.container {
  max-width: 1200px;
  margin: 0 auto;
  padding: 6px 16px;
  display: flex;
  align-items: center;
  justify-content: space-between; /* ✅ 左右分佈 */
}

/* ====== 🩵 左側品牌區 ====== */
.brand {
  display: flex;
  align-items: center;
  gap: 6px;
  text-decoration: none;
}

.logo {
  width: 100px;        /* ✅ 調整 Logo 大小 */
  height: 50px;
  object-fit: contain;
}

.brand-text {
  font-family: 'Noto Serif TC', 'Microsoft JhengHei', serif;
  font-size: 20px;
  font-weight: 700;
  color: #004e89;
  letter-spacing: 2px;
  transform: translateY(-3px); /* 微微上移對齊 logo */
}

/* ====== 💙 右側按鈕列 ====== */
.right {
  display: flex;
  align-items: center;
  gap: 14px;
}

.link {
  color: #0d6efd;
  text-decoration: none;
  font-size: 14px;
  font-weight: 500;
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

/* ====== 📱 響應式（可選） ====== */
@media (max-width: 768px) {
  .logo {
    width: 40px;
    height: 40px;
  }
  .brand-text {
    font-size: 18px;
  }
  .container {
    padding: 6px 12px;
  }
}
</style>
