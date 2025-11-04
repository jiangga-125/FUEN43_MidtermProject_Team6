<template>
  <div class="auth-shell">
    <form class="card" @submit.prevent="goReset">
      <h2 class="title">重設密碼</h2>

      <label class="field">
        <span>註冊 Email</span>
        <input v-model.trim="account" placeholder="you@example.com" autocomplete="username" />
      </label>

      <p class="muted">
        送出後請至信箱收信取得「重設驗證碼」，接著前往下一步輸入驗證碼與新密碼。
      </p>

      <p v-if="msg" class="ok">{{ msg }}</p>
      <p v-if="err" class="err">{{ err }}</p>

      <div class="row between">
        <button class="secondary" type="button"
                :disabled="loading || !isEmail(account) || otpCountdown>0"
                @click="sendResetCode">
          {{ otpCountdown>0 ? `已寄出(${otpCountdown}s)` : '寄送重設驗證碼' }}
        </button>
        <button class="primary" :disabled="!isEmail(account)" type="submit">下一步</button>
      </div>

      <p class="hint"><a href="/login">返回登入</a></p>
    </form>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import http from '@/lib/http'

// 對齊新流程：共用 email/send + Purpose=ResetPassword
const API_SEND = '/api/auth/email/send' // { Account, Purpose='ResetPassword' }

const account = ref('')
const loading = ref(false)
const err = ref('')
const msg = ref('')

function isEmail(s: string) { return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(s) }

const otpCountdown = ref(0)
let timer: number | null = null
function startCountdown() {
  otpCountdown.value = 60
  if (timer) clearInterval(timer)
  timer = window.setInterval(() => {
    otpCountdown.value--
    if (otpCountdown.value <= 0 && timer) { clearInterval(timer); timer = null }
  }, 1000)
}

async function sendResetCode() {
  err.value = ''; msg.value = ''
  try {
    loading.value = true
    await http.post(API_SEND, { Account: account.value, Purpose: 'ResetPassword' })
    msg.value = '已寄送，請至信箱收取驗證碼'
    startCountdown()
  } catch (e: any) {
    err.value = e?.response?.data?.message || '寄送失敗'
  } finally {
    loading.value = false
  }
}

function goReset() {
  if (!isEmail(account.value)) return
  location.href = `/reset?email=${encodeURIComponent(account.value)}`
}
</script>

<style scoped>
@import './_auth-shared.css';
</style>
