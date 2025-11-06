<script setup lang="ts">
import { computed } from 'vue'
import { useRoute, RouterLink } from 'vue-router'
import { useAuth } from '@/stores/auth'

const route = useRoute()
const auth = useAuth()
const member = computed(() => auth.member)

const tabs = [
  { to: { name: 'member-profile' },  text: '會員資料維護', icon: '👤' },
  { to: { name: 'member-security' }, text: '帳號密碼與安全性', icon: '🔒' },
  { to: { name: 'member-orders' },   text: '訂單紀錄', icon: '🧾' },
  { to: { name: 'member-borrows' },  text: '借書紀錄', icon: '📚' },
  { to: { name: 'member-coupons' },  text: '優惠券',   icon: '🎟️' },
  { to: { name: 'member-favorites' },text: '收藏',     icon: '💖' },
  { to: { name: 'member-cart' },     text: '購物車',   icon: '🛒' },
  { to: { name: 'member-reviews' },  text: '評論',     icon: '✍️' },
]
</script>

<template>
  <div class="member-center">
    <h2 class="page-title">會員中心</h2>

    <div v-if="member" class="shell">
      <!-- 左側（桌機）或上方（手機）導覽列 -->
      <nav class="nav">
        <RouterLink
          v-for="t in tabs" :key="t.text"
          :to="t.to"
          class="nav-item"
          active-class="active"
        >
          <span class="icon">{{ t.icon }}</span>
          <span class="text">{{ t.text }}</span>
        </RouterLink>
      </nav>

      <!-- 內容區：對應各子頁面 -->
      <section class="content">
        <!-- 可酌量用 keep-alive 保留頁面狀態 -->
        <keep-alive include="Profile,Security,Orders,Borrows,Coupons,Favorites,Cart,Reviews">
          <router-view />
        </keep-alive>
      </section>
    </div>

    <div v-else class="guest">
      尚未登入，請先 <RouterLink to="/login">登入</RouterLink>。
    </div>
  </div>
</template>

<style scoped>
.member-center{max-width:1200px;margin:24px auto;padding:0 16px}
.page-title{margin-bottom:16px}

.shell{display:grid;grid-template-columns:260px 1fr;gap:16px}
@media (max-width: 992px){
  .shell{grid-template-columns:1fr}
}

/* 導覽列：桌機垂直、手機橫向捲動 */
.nav{
  background:#fff;border:1px solid #e9ecef;border-radius:16px;padding:10px;
  box-shadow:0 4px 18px rgba(0,0,0,.04);
  display:flex;flex-direction:column;gap:6px;min-height:100%;
}
@media (max-width: 992px){
  .nav{flex-direction:row;overflow:auto;white-space:nowrap}
}

.nav-item{
  display:flex;gap:10px;align-items:center;padding:10px 12px;border-radius:10px;
  text-decoration:none;color:#333;border:1px solid transparent;
}
.nav-item:hover{background:#f8fafc}
.nav-item.active{background:#0d6efd;color:#fff;border-color:#0d6efd}
.icon{width:22px;text-align:center}
.content{
  background:#fff;border:1px solid #e9ecef;border-radius:16px;padding:18px;
  box-shadow:0 4px 18px rgba(0,0,0,.04);
}
</style>
