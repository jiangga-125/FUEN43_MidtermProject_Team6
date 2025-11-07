<!-- src/views/MemberCenter.vue -->
<template>
  <div class="member-center">
    <div class="grid">
      <!-- 左：基本資料 + 2FA -->
      <section class="card">
        <h3 class="card-title">基本資料</h3>

        <div class="row">
          <div class="label">名稱</div>
          <div class="value">{{ member?.name || '-' }}</div>
        </div>
        <div class="row">
          <div class="label">Email</div>
          <div class="value">{{ member?.email || '-' }}</div>
        </div>
        <div class="row">
          <div class="label">兩步驗證</div>
          <div class="value">
            <span class="badge" :class="twoFaEnabled ? 'on' : 'off'">{{ twoFaEnabled ? '已啟用' : '未啟用' }}</span>
          </div>
        </div>

        <div class="actions">
          <button class="btn" @click="goTotpSetup">設定 / 綁定 2FA</button>
          <button class="btn danger" @click="logout">登出</button>
        </div>

        <!-- Email OTP（為了便於測試登入流程與 Trusted Device） -->
        <div class="divider"></div>
        <h4 class="sub">Email OTP 測試</h4>
        <p class="muted">
          寄送 6 碼至你的 Email，輸入後可驗證登入；可勾選「記住此裝置 30 天」。
        </p>

        <div class="otp-send">
          <button
            class="btn outline"
            :disabled="sending || !isEmail(member?.email || '') || countdown > 0"
            @click="sendOtp"
          >
            {{ countdown>0 ? `重新寄送 (${countdown}s)` : '寄送驗證碼' }}
          </button>
          <span v-if="devCode" class="dev-code">（devCode：{{ devCode }}）</span>
        </div>

        <div class="otp-verify">
          <input
            class="input"
            placeholder="輸入 6 碼"
            maxlength="6"
            v-model.trim="otpCode"
          />
          <label class="check">
            <input type="checkbox" v-model="rememberDevice" />
            此裝置 30 天免驗證
          </label>
          <button class="btn primary" :disabled="verifying || !/^[0-9]{6}$/.test(otpCode)" @click="verifyOtp">
            驗證
          </button>
        </div>

        <p v-if="msg" :class="['msg', msgOk ? 'ok' : 'err']">{{ msg }}</p>
      </section>

      <!-- 右：變更密碼（保留樣式，實際 API 你再接） -->
      <section class="card">
        <h3 class="card-title">變更密碼</h3>
        <input class="input" type="password" placeholder="舊密碼" v-model="oldPwd" />
        <input class="input" type="password" placeholder="新密碼" v-model="newPwd" />
        <input class="input" type="password" placeholder="確認新密碼" v-model="newPwd2" />
        <button class="btn primary" :disabled="!canChangePwd" @click="changePwd">
          儲存變更
        </button>
        <p v-if="pwdMsg" :class="['msg', pwdOk ? 'ok':'err']">{{ pwdMsg }}</p>
      </section>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import http from '@/lib/http'
import { useAuth } from '@/stores/auth'
import { getDeviceHash } from '@/lib/deviceHash'

const auth = useAuth()
const router = useRouter()

const member = computed(() => auth.member)
// 目前 /api/auth/me 回傳沒有 TwoFactorEnabled；先以 false 顯示未啟用（等你後端補欄位再讀取）
const twoFaEnabled = ref(false)

// ===== Email OTP 狀態 =====
const sending = ref(false)
const verifying = ref(false)
const otpCode = ref('')
const countdown = ref(0)
const timer = ref<number | null>(null)
const rememberDevice = ref(true)
const devCode = ref<string | null>(null) // 開發模式會回傳 devCode
const msg = ref('')
const msgOk = ref(false)

function isEmail(s: string) { return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(s) }

function startCountdown(sec = 60) {
  countdown.value = sec
  if (timer.value) window.clearInterval(timer.value)
  timer.value = window.setInterval(() => {
    countdown.value--
    if (countdown.value <= 0 && timer.value) {
      window.clearInterval(timer.value)
      timer.value = null
    }
  }, 1000)
}

async function sendOtp() {
  if (!member.value?.email || !isEmail(member.value.email)) {
    msg.value = '無效的 Email'
    msgOk.value = false
    return
  }
  sending.value = true
  msg.value = ''; devCode.value = null
  try {
    const r = await http.post('/api/auth/email/send', {
      Account: member.value.email,
      Purpose: 'Login'
    })
    // dev 環境後端會回 devCode
    devCode.value = r.data?.devCode ?? null
    msg.value = '驗證碼已寄出，請查看郵件'
    msgOk.value = true
    startCountdown(60)
  } catch (e: any) {
    msg.value = e?.response?.data?.message || '驗證碼寄送失敗'
    msgOk.value = false
  } finally {
    sending.value = false
  }
}

async function verifyOtp() {
  if (!member.value?.email) return
  verifying.value = true
  msg.value = ''
  try {
    const body = {
      Account: member.value.email,
      Code: otpCode.value,
      RememberDevice: !!rememberDevice.value,
      DeviceHash: getDeviceHash()
    }
    const r = await http.post('/api/auth/2fa/email/verify', body)
    // 後端已同時下發 access token + refresh cookie
    await auth.setTokenAndLoadMember(r.data.token)
    msg.value = '驗證成功！此裝置已記住'
    msgOk.value = true
    otpCode.value = ''
  } catch (e: any) {
    msg.value = e?.response?.data?.message || '驗證失敗'
    msgOk.value = false
  } finally {
    verifying.value = false
  }
}

// ===== 變更密碼（外觀保留；API 你再串） =====
const oldPwd = ref('')
const newPwd = ref('')
const newPwd2 = ref('')
const pwdMsg = ref('')
const pwdOk = ref(false)
const canChangePwd = computed(() =>
  oldPwd.value.length >= 1 && newPwd.value.length >= 6 && newPwd.value === newPwd2.value
)
async function changePwd() {
  try {
    // 範例：await http.post('/api/member/change-password', { oldPwd:..., newPwd:... })
    pwdMsg.value = '（示範）尚未串接 API'
    pwdOk.value = false
  } catch (e: any) {
    pwdMsg.value = e?.response?.data?.message || '變更失敗'
    pwdOk.value = false
  }
}

async function logout() {
  try { await auth.logout() } finally { router.push('/login') }
}

function goTotpSetup() {
  router.push('/2fa/setup') // 之後你做 TOTP 頁面時用這條路徑
}

onMounted(() => {
  // 若未登入，導回登入
  if (!member.value) router.replace('/login')
})
</script>

<style scoped>
.member-center{max-width:1100px;margin:24px auto;padding:0 16px}
.grid{display:grid;grid-template-columns:1fr 1fr;gap:20px}
@media (max-width: 900px){.grid{grid-template-columns:1fr}}

.card{background:#fff;border:1px solid #e9ecef;border-radius:16px;padding:18px 18px 16px;box-shadow:0 4px 18px rgba(0,0,0,.04);display:grid;gap:12px}
.card-title{margin:0 0 4px}
.sub{margin:6px 0 4px}

.row{display:grid;grid-template-columns:120px 1fr;gap:10px;align-items:center}
.label{color:#666}
.value{font-weight:600}

.badge{display:inline-block;padding:2px 8px;border-radius:999px;font-size:12px}
.badge.on{background:#e8f7ef;color:#139a50}
.badge.off{background:#fff3f0;color:#c74a2e;border:1px solid #ffd8cc}

.actions{display:flex;gap:10px;margin-top:4px;flex-wrap:wrap}

.input{padding:10px 12px;border:1px solid #dfe3e8;border-radius:10px;font-size:14px;width:100%}
.btn{padding:10px 12px;border-radius:10px;border:1px solid #e5e7eb;background:#f8fafc;font-weight:600;cursor:pointer}
.btn:hover{filter:brightness(0.98)}
.btn.primary{background:#0d6efd;border-color:#0d6efd;color:#fff}
.btn.outline{background:#fff;border-color:#cfd6e0}
.btn.danger{background:#ffecec;border-color:#ffd9d9;color:#c0392b}

.divider{height:1px;background:#f0f2f5;margin:2px 0 8px}

.otp-send{display:flex;align-items:center;gap:8px}
.dev-code{color:#999;font-size:12px}

.otp-verify{display:flex;gap:10px;align-items:center;flex-wrap:wrap}
.check{display:flex;align-items:center;gap:6px;color:#555}

.msg{margin:2px 0 0;font-size:13px}
.msg.ok{color:#138a36}
.msg.err{color:#c0392b}
.muted{color:#6b7280;font-size:13px}
</style>
