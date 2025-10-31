<template>
  <div class="wrap">
    <h3>會員中心</h3>
    <p>歡迎，{{ user?.name }}</p>
    <div class="links">
      <a href="/">回首頁</a>
      <button @click="signout">登出</button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { authState } from '@/stores/auth'
import { logout } from '@/api/auth'
import { computed } from 'vue'

const user = computed(() => authState.user)
async function signout() {
  await logout()
  location.href = '/login'
}
</script>

<style scoped>
.wrap {
  max-width: 720px;
  margin: 24px auto;
  padding: 12px;
}
.links {
  display: flex;
  gap: 12px;
  margin-top: 12px;
}
button {
  background: #eee;
  border: 1px solid #ddd;
  border-radius: 8px;
  padding: 8px 12px;
  cursor: pointer;
}
</style>
