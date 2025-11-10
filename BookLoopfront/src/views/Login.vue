<!-- src/views/Login.vue -->
<template>
  <div class="auth-shell">
    <form class="card" @submit.prevent="loginPassword">
      <h2 class="title">登入 BookLoop</h2>

      <div class="tab-row">
        <button
          type="button"
          :class="['tab', tab === 'password' && 'active']"
          @click="tab = 'password'"
        >
          帳密登入
        </button>
        <button type="button" :class="['tab', tab === 'email' && 'active']" @click="tab = 'email'">
          Email 驗證碼
        </button>
        <button type="button" :class="['tab', tab === 'totp' && 'active']" @click="tab = 'totp'">
          TOTP 驗證碼
        </button>
      </div>

      <!-- 帳密 -->
      <div v-if="tab === 'password'" class="pane">
        <label class="field">
          <span>帳號（Email）</span>
          <input v-model.trim="account" autocomplete="username" placeholder="you@example.com" />
        </label>

        <label class="field">
          <span>密碼</span>
          <div class="password-box">
            <input
              :type="showPwd ? 'text' : 'password'"
              v-model="password"
              autocomplete="current-password"
              placeholder="你的密碼"
            />
            <button class="ghost" type="button" @click="showPwd = !showPwd">
              {{ showPwd ? '🙈' : '👁️' }}
            </button>
          </div>
        </label>

        <label class="row between">
          <span class="muted">
            <input type="checkbox" v-model="remember" @change="onRememberChange" />
            記住我（此裝置）
          </span>
          <a class="link" href="/forgot">忘記密碼？</a>
        </label>

        <button
          class="primary"
          :disabled="auth.loading || !canSubmitPassword"
          @click="loginPassword"
        >
          登入
        </button>
      </div>

      <!-- Email OTP -->
      <div v-if="tab === 'email'" class="pane">
        <label class="field">
          <span>帳號（Email）</span>
          <input v-model.trim="account" autocomplete="username" placeholder="you@example.com" />
        </label>

        <div class="row between">
          <button
            class="secondary"
            type="button"
            :disabled="auth.loading || !isEmail(account)"
            @click="sendEmailOtp"
          >
            寄送驗證碼
          </button>
          <span class="muted" v-if="otpCountdown > 0">已寄出（{{ otpCountdown }}s）</span>
        </div>

        <label class="field">
          <span>驗證碼（6 碼）</span>
          <input v-model.trim="emailCode" placeholder="例如：123456" maxlength="6" />
        </label>

        <!-- 勾選：同一行靠左、不斷行 -->
        <label class="remember2fa">
          <input type="checkbox" v-model="rememberDevice2fa" />
          <span class="muted">此裝置 30 天免驗證</span>
        </label>

        <button
          class="primary"
          type="button"
          :disabled="auth.loading || !canSubmitEmail"
          @click="loginByEmailOtp"
        >
          使用 Email 驗證碼登入
        </button>
      </div>

      <!-- TOTP -->
      <div v-if="tab === 'totp'" class="pane">
        <label class="field">
          <span>帳號（Email）</span>
          <input v-model.trim="account" autocomplete="username" placeholder="you@example.com" />
        </label>

        <label class="field">
          <span>驗證器 6 碼</span>
          <input v-model.trim="totpCode" placeholder="例如：123456" maxlength="6" />
        </label>

        <div class="row between">
          <button
            class="primary"
            type="button"
            :disabled="auth.loading || !canSubmitTotp"
            @click="loginByTotp"
          >
            使用 TOTP 登入
          </button>
          <a class="link" href="/2fa/setup">尚未綁定？去設定</a>
        </div>
      </div>

      <p class="err" v-if="err">{{ err }}</p>

      <div class="divider"><span>或</span></div>

      <!-- ✅ 一鍵登入（前台）— 直接用目前帳密欄位的值送出 -->
      <button
        type="button"
        class="btn-demo"
        :disabled="auth.loading || !account || !password"
        @click="oneClickLogin"
        title="使用上方輸入的 Email/Password 直接登入（快捷鍵 Alt+D）"
      >
        一鍵登入（前台）
      </button>

      <div class="divider small" v-if="hasSso"><span>也可以</span></div>

      <!-- 其他 SSO -->
      <div class="sso-row">
        <button
          type="button"
          class="sso google"
          :disabled="auth.loading"
          @click="external('Google')"
        >
          使用 Google 登入
        </button>
        <button
          type="button"
          class="sso facebook"
          :disabled="auth.loading"
          @click="external('Facebook')"
        >
          使用 Facebook 登入
        </button>
        <button type="button" class="sso line" :disabled="auth.loading" @click="external('LINE')">
          使用 LINE 登入
        </button>
      </div>

      <p class="hint">沒有帳號？<a href="/register">前往註冊</a></p>
    </form>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted, onBeforeUnmount } from 'vue'
import { useRouter } from 'vue-router'
import http from '@/lib/http'
import { useAuth } from '@/stores/auth'
import { getDeviceHash } from '@/lib/deviceHash'

const auth = useAuth()
const router = useRouter()

const features = {
  totp: false,
  google: true,
  facebook: false,
  line: false,
}
const hasSso = computed(() => features.google || features.facebook || features.line)

const tab = ref<'password' | 'email' | 'totp'>('password')
const account = ref('test@gmail.com')
const password = ref('000000')
const emailCode = ref('')
const totpCode = ref('')
const showPwd = ref(false)
const err = ref('')

const remember = ref(true)
const rememberDevice2fa = ref(true)

function onRememberChange() {
  auth.setRemember(remember.value)
  localStorage.setItem('remember_me', remember.value ? '1' : '0')
}

function isEmail(s: string) {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(s)
}
const canSubmitPassword = computed(() => isEmail(account.value) && password.value.length >= 1)
const canSubmitEmail = computed(() => isEmail(account.value) && /^[0-9]{6}$/.test(emailCode.value))
const canSubmitTotp = computed(() => isEmail(account.value) && /^[0-9]{6}$/.test(totpCode.value))

function getRedirectTarget() {
  const q = new URLSearchParams(location.search)
  return q.get('redirect') || '/'
}

const otpCountdown = ref(0)
let otpTimer: number | null = null
function startOtpCountdown() {
  otpCountdown.value = 60
  if (otpTimer) window.clearInterval(otpTimer)
  otpTimer = window.setInterval(() => {
    otpCountdown.value--
    if (otpCountdown.value <= 0 && otpTimer) {
      window.clearInterval(otpTimer)
      otpTimer = null
    }
  }, 1000)
}

async function loginPassword() {
  if (!canSubmitPassword.value) return
  err.value = ''
  try {
    await auth.login(account.value, password.value)
    router.replace(getRedirectTarget())
  } catch (e: any) {
    err.value = (auth as any).error || e?.response?.data?.message || '登入失敗'
  }
}

function oneClickLogin() {
  tab.value = 'password'
  loginPassword()
}
function onKey(e: KeyboardEvent) {
  if (e.altKey && (e.key === 'd' || e.key === 'D')) {
    e.preventDefault()
    oneClickLogin()
  }
}

async function sendEmailOtp() {
  if (!isEmail(account.value)) {
    err.value = '請輸入有效 Email'
    return
  }
  err.value = ''
  try {
    await http.post('/api/auth/email/send', { Account: account.value, Purpose: 'Login' })
    startOtpCountdown()
  } catch (e: any) {
    err.value = e?.response?.data?.message || '驗證碼寄送失敗'
  }
}
async function loginByEmailOtp() {
  if (!canSubmitEmail.value) return
  err.value = ''
  try {
    await auth.loginWithEmailCode(account.value, emailCode.value, {
      rememberDevice: !!rememberDevice2fa.value,
      deviceHash: getDeviceHash(),
    })
    router.replace(getRedirectTarget())
  } catch (e: any) {
    err.value = e?.response?.data?.message || '驗證碼登入失敗'
  }
}

async function loginByTotp() {
  if (!canSubmitTotp.value) return
  err.value = ''
  try {
    /*修改：接住回傳*/
    const res = await auth.loginWithTotp(account.value, totpCode.value)
    /*新增：抽 token + 套用*/
    const token = pickToken(res) || (auth as any)?.token || (auth as any)?.state?.token || null
    if (!token) throw new Error('登入回應沒有 token')
    applyToken(token)

    router.replace(getRedirectTarget())
  } catch (e: any) {
    err.value = e?.response?.data?.message || 'TOTP 登入失敗'
  }
}

// 外部登入（popup）
async function external(provider: 'Google' | 'Facebook' | 'LINE') {
  err.value = ''
  try {
    const redirect = getRedirectTarget()
    await auth.loginWithExternal(provider, redirect)
    router.replace(redirect)
  } catch (e: any) {
    err.value = e?.message || '外部登入失敗，請重試或改用帳密登入'
  }
}

onMounted(() => {
  remember.value = (localStorage.getItem('remember_me') ?? '1') === '1'
  auth.setRemember(remember.value)
  window.addEventListener('keydown', onKey)

  const qs = new URLSearchParams(location.search)
  if (qs.get('err') === 'oauth') err.value = '外部登入未完成授權，請重試或改用帳密登入'
})
onBeforeUnmount(() => {
  window.removeEventListener('keydown', onKey)
})
/*統一存 token + 讓 axios 立刻帶上*/
function applyToken(token: string) {
  localStorage.setItem('token', token)
  http.defaults.headers.common.Authorization = `Bearer ${token}`
}

/*從各種可能的回傳取出 token（後端回的是 data.token）*/
function pickToken(res: any): string | null {
  return res?.token ?? res?.access_token ?? res?.Token ?? null
}
</script>

<style scoped>
.auth-shell {
  min-height: 100vh;
  display: grid;
  place-items: center;
  background:
    radial-gradient(60% 120% at 10% 10%, #eef4ff 0%, transparent 60%),
    radial-gradient(70% 130% at 90% 20%, #fff3f0 0%, transparent 60%), #fafafa;
}
.card {
  width: min(92vw, 460px);
  background: #fff;
  border: 1px solid #e9ecef;
  border-radius: 16px;
  padding: 20px 20px 16px;
  box-shadow: 0 6px 24px rgba(0, 0, 0, 0.06);
  display: grid;
  gap: 12px;
}
.title {
  margin: 0 0 6px;
  text-align: center;
}
.tab-row {
  display: grid;
  grid-template-columns: 1fr 1fr 1fr;
  gap: 6px;
}
.tab {
  border: 1px solid #e5e7eb;
  background: #f8fafc;
  color: #374151;
  border-radius: 10px;
  padding: 8px 10px;
  cursor: pointer;
}
.tab.active {
  background: #0d6efd;
  color: #fff;
  border-color: #0d6efd;
}
.pane {
  display: grid;
  gap: 10px;
  margin-top: 6px;
}
.field {
  display: grid;
  gap: 6px;
}
.field span {
  font-size: 13px;
  color: #555;
}
input {
  padding: 10px 12px;
  border: 1px solid #dfe3e8;
  border-radius: 10px;
  outline: none;
  font-size: 14px;
  background: #fff;
}
.password-box {
  display: grid;
  grid-template-columns: 1fr auto;
  gap: 6px;
}
button {
  padding: 10px 12px;
  border-radius: 10px;
  cursor: pointer;
  border: 1px solid transparent;
  font-weight: 600;
}
.primary {
  background: #0d6efd;
  color: #fff;
}
.primary[disabled] {
  opacity: 0.6;
  cursor: not-allowed;
}
.secondary {
  background: #eef2ff;
  color: #3949ab;
  border-color: #c7d2fe;
}
.ghost {
  background: #f5f5f5;
  color: #444;
  border: 1px solid #e5e7eb;
}
.row {
  display: flex;
  gap: 10px;
  align-items: center;
}
.between {
  justify-content: space-between;
}
.muted {
  color: #666;
  font-size: 13px;
}
.link {
  color: #0d6efd;
  text-decoration: none;
}
.link:hover {
  text-decoration: underline;
}
.err {
  color: #c0392b;
  background: #fdecea;
  border: 1px solid #fadbd8;
  padding: 8px;
  border-radius: 8px;
}
.divider {
  display: grid;
  place-items: center;
  margin-top: 2px;
}
.divider span {
  color: #999;
  font-size: 12px;
}
.divider.small {
  margin-top: 0;
}
.sso-row {
  display: grid;
  gap: 8px;
  margin-top: 2px;
}
.sso {
  display: grid;
  place-items: center;
  font-weight: 600;
}
.sso.google {
  background: #fff;
  border: 1px solid #e3e7ee;
}
.sso.facebook {
  background: #1877f2;
  color: #fff;
  border: 0;
}
.sso.line {
  background: #06c755;
  color: #fff;
  border: 0;
}
.hint {
  font-size: 13px;
  color: #666;
  text-align: center;
  margin-top: 4px;
}

/* 一鍵登入按鈕（前台） */
.btn-demo {
  background: linear-gradient(90deg, #ffb86b, #ff7a59);
  color: #111;
  border: 0;
  border-radius: 10px;
  height: 42px;
  font-weight: 700;
}
.btn-demo[disabled] {
  opacity: 0.6;
  cursor: not-allowed;
}

/* 2FA 勾選微調 */
.remember2fa {
  gap: 8px;
  margin-top: -2px;
}
</style>
