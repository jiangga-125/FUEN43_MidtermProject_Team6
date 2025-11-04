<!-- src/views/MemberCenter.vue -->
<template>
  <div class="member-center">
    <h2>會員中心（舊檔保留）</h2>
    <div v-if="member">
      <p><b>名稱：</b>{{ member.name || '-' }}</p>
      <p><b>Email：</b>{{ member.email }}</p>
      <button @click="logout">登出</button>
    </div>
    <div v-else>
      <p>尚未登入，請先 <a href="/login">登入</a>。</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useAuth } from '@/stores/auth'
import { useRouter } from 'vue-router'

const auth = useAuth()
const router = useRouter()

const member = computed(() => auth.member)

async function logout() {
  try { await auth.logout() } finally { router.push('/login') }
}
</script>

<style scoped>
.member-center { max-width: 720px; margin: 24px auto; padding: 0 16px; }
</style>
