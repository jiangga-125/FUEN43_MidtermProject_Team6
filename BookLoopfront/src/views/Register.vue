<template>
  <div class="auth-shell">
    <form class="card" @submit.prevent="submit">
      <h2 class="title">註冊帳號</h2>

      <label class="field">
        <span>Email（做為登入帳號）</span>
        <input v-model.trim="account" placeholder="you@example.com" autocomplete="username" />
      </label>

      <label class="field">
        <span>顯示名稱</span>
        <input v-model.trim="name" placeholder="你的名稱" />
      </label>

      <label class="field">
        <span>密碼</span>
        <input v-model="password" type="password" autocomplete="new-password" placeholder="至少 6 碼" />
      </label>

      <label class="field">
        <span>確認密碼</span>
        <input v-model="confirm" type="password" autocomplete="new-password" placeholder="再次輸入密碼" />
      </label>

      <div class="row between">
        <label class="field code">
          <span>Email 驗證碼</span>
          <input v-model.trim="code" maxlength="6" placeholder="6 碼" />
        </label>
        <button class="secondary" type="button"
                :disabled="loading || !isEmail(account) || otpCountdown>0"
                @click="sendCode">
          {{ otpCountdown>0 ? `重送(${otpCountdown}s)` : '寄送驗證碼' }}
        </button>
      </div>

      <p v-if="err" class="err">{{ err }}</p>
      <button class="primary" :disabled="loading || !canSubmit" type="submit">建立帳號</button>

      <p class="hint">已經有帳號？<a href="/login">去登入</a></p>
    </form>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue'
import http from '@/lib/http'
import { useAuth } from '@/stores/auth'

const auth = useAuth()

const API_SEND = '/api/auth/email/send'            // { Account, Purpose='Register' }
const API_REGISTER = '/api/auth/register/confirm'  // { Account, Name, Password, Code }

const account = ref('')  // email
const name = ref('')
const password = ref('')
const confirm = ref('')
const code = ref('')
const loading = ref(false)
const err = ref('')

function isEmail(s: string) { return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(s) }
const canSubmit = computed(() =>
  isEmail(account.value) &&
  name.value.trim().length > 0 &&
  password.value.length >= 6 &&
  password.value === confirm.value &&
  /^[0-9]{6}$/.test(code.value)
)

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

async function sendCode() {
  err.value = ''
  try {
    await http.post(API_SEND, { Account: account.value, Purpose: 'Register' })
    startCountdown()
  } catch (e: any) {
    err.value = e?.response?.data?.message || '驗證碼寄送失敗'
  }
}

async function submit() {
  if (!canSubmit.value) return
  loading.value = true; err.value = ''
  try {
    const { data } = await http.post(API_REGISTER, {
      Account: account.value,
      Name: name.value.trim(),
      Password: password.value,
      Code: code.value
    })
    if (data?.token) {
      await auth.setTokenAndLoadMember(data.token)
      location.href = '/'
      return
    }
    // 理論上會回 token；保底用帳密幫登入
    await auth.login(account.value, password.value)
    location.href = '/'
  } catch (e: any) {
    err.value = e?.response?.data?.message || '註冊失敗'
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
@import './_auth-shared.css';
.code input { width: 140px; }
</style>
