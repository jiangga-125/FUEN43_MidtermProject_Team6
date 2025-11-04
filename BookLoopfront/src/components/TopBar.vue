<!-- src/components/TopBar.vue -->
<script setup lang="ts">
import { RouterLink, useRouter } from 'vue-router'
import { storeToRefs } from 'pinia'
import { useAuth } from '@/stores/auth'

const router = useRouter()
const auth = useAuth()

// 直接把 state/getters 變成 ref
const { member, isMemberLoggedIn } = storeToRefs(auth)

async function signout() {
  try { await auth.logout() } finally { router.push('/login') }
}
</script>

<template>
  <div class="topbar">
    <div class="container row">
      <div class="left">
        <RouterLink class="brand" to="/">簿錄書城</RouterLink>
      </div>

      <div class="right">
        <template v-if="isMemberLoggedIn">
          <span class="hi">Hi, {{ member?.name || member?.email }}</span>
          <RouterLink class="link" to="/member">會員中心</RouterLink>
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
.topbar { position: sticky; top: 0; z-index: 1000; background: #f8f9fa; border-bottom: 1px solid #e9ecef; }
.container { max-width: 1200px; margin: 0 auto; padding: 8px 16px; }
.row { display: flex; align-items: center; justify-content: space-between; gap: 12px; }
.brand { font-weight: 600; color: #222; text-decoration: none; }
.right { display: flex; align-items: center; gap: 12px; }
.link { color: #0d6efd; text-decoration: none; font-size: 14px; }
.link:hover { text-decoration: underline; }
.hi { color: #555; font-size: 14px; }
.btn { background: none; border: none; padding: 0; cursor: pointer; }
</style>
