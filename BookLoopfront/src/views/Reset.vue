<!-- src/views/Reset.vue -->
<template>
  <div class="auth-shell">
    <form class="card" @submit.prevent="submit">
      <h2 class="title">設定新密碼</h2>

      <label class="field">
        <span>Email</span>
        <input v-model.trim="account" placeholder="you@example.com" />
      </label>

      <label class="field">
        <span>Email 驗證碼</span>
        <input v-model.trim="code" maxlength="6" placeholder="6 碼" />
      </label>

      <label class="field">
        <span>新密碼</span>
        <input v-model="password" type="password" placeholder="至少 6 碼" autocomplete="new-password" />
      </label>

      <label class="field">
        <span>確認新密碼</span>
        <input v-model="confirm" type="password" placeholder="再次輸入" autocomplete="new-password" />
      </label>

      <p v-if="msg" class="ok">{{ msg }}</p>
      <p v-if="err" class="err">{{ err }}</p>

      <button class="primary" :disabled="loading || !canSubmit" type="submit">更新密碼</button>

      <p class="hint"><a href="/login">回登入</a></p>
    </form>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import http from '@/lib/http'

// 統一用 /api/auth/reset/confirm （與註冊驗證碼同風格的 confirm 端點）
const API_CONFIRM = '/api/auth/reset/confirm' // { Account, Code, Password }

const account = ref('')
const code = ref('')
const password = ref('')
const confirm = ref('')
const loading = ref(false)
const err = ref('')
const msg = ref('')

function isEmail(s: string) { return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(s) }
const canSubmit = computed(() =>
  isEmail(account.value) &&
  /^[0-9]{6}$/.test(code.value) &&
  password.value.length >= 6 &&
  password.value === confirm.value
)

onMounted(() => {
  const email = new URLSearchParams(location.search).get('email')
  if (email) account.value = email
})

async function submit() {
  if (!canSubmit.value) return
  loading.value = true; err.value = ''; msg.value = ''
  try {
    await http.post(API_CONFIRM, {
      Account: account.value,
      Code: code.value,
      Password: password.value
    })
    msg.value = '✅ 密碼已更新，請使用新密碼登入'
  } catch (e: any) {
    err.value = e?.response?.data?.message || '重設失敗'
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
@import './_auth-shared.css';
</style>
