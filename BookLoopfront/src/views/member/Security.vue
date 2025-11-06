<script setup lang="ts">
import { computed, ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useAuth } from '@/stores/auth'
import http from '@/lib/http'
import { getDeviceHash } from '@/lib/deviceHash'

const auth = useAuth()
const router = useRouter()
const member = computed(() => auth.member)

// TODO: 之後從 /api/auth/me 讀真正的 TwoFactorEnabled
const twofaEnabled = ref(false)

/* ===== Email OTP 狀態 ===== */
const sending = ref(false)
const verifying = ref(false)
const otpCode = ref('')
const countdown = ref(0)
const timer = ref<number | null>(null)
const rememberDevice = ref(true)
const devCode = ref<string | null>(null)
const otpMsg = ref('')
const otpOk = ref(false)

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
async function sendEmailOtp() {
  if (!member.value?.email || !isEmail(member.value.email)) {
    otpMsg.value = '無效的 Email'
    otpOk.value = false
    return
  }
  sending.value = true
  otpMsg.value = ''
  devCode.value = null
  try {
    const r = await http.post('/api/auth/email/send', {
      Account: member.value.email,
      Purpose: 'Login'
    })
    devCode.value = r.data?.devCode ?? null // 開發環境
    otpMsg.value = '驗證碼已寄出，請查看信箱'
    otpOk.value = true
    startCountdown(60)
  } catch (e:any) {
    otpMsg.value = e?.response?.data?.message || '驗證碼寄送失敗'
    otpOk.value = false
  } finally {
    sending.value = false
  }
}
async function verifyEmailOtp() {
  if (!/^[0-9]{6}$/.test(otpCode.value)) {
    otpMsg.value = '請輸入 6 碼數字'
    otpOk.value = false
    return
  }
  if (!member.value?.email) return
  verifying.value = true
  otpMsg.value = ''
  try {
    const body = {
      Account: member.value.email,
      Code: otpCode.value,
      RememberDevice: !!rememberDevice.value,
      DeviceHash: getDeviceHash()
    }
    const r = await http.post('/api/auth/2fa/email/verify', body)
    await auth.setTokenAndLoadMember(r.data.token)
    otpMsg.value = '驗證成功！此裝置已記住'
    otpOk.value = true
    otpCode.value = ''
  } catch (e:any) {
    otpMsg.value = e?.response?.data?.message || '驗證失敗'
    otpOk.value = false
  } finally {
    verifying.value = false
  }
}

/* ===== 變更密碼（先保留外觀，等你串API） ===== */
const oldPassword = ref('')
const newPassword = ref('')
const confirmPassword = ref('')
const changing = ref(false)
const pwdMsg = ref('')
const pwdOk = ref(false)
const canChangePwd = computed(() =>
  oldPassword.value.length >= 1 &&
  newPassword.value.length >= 6 &&
  newPassword.value === confirmPassword.value
)
async function changePassword () {
  try {
    changing.value = true
    // 範例串法：await http.post('/api/auth/password/change', { Old: oldPassword.value, New: newPassword.value })
    pwdMsg.value = '（示範）尚未串接 API'
    pwdOk.value = false
  } finally {
    changing.value = false
  }
}

function gotoTotpSetup () { router.push('/2fa/setup') }
async function signout () { try { await auth.logout?.() } finally { router.push('/login') } }

onMounted(() => {
  if (!member.value) router.replace('/login')
})
</script>

<template>
  <div class="security">
    <h3 class="title">帳號密碼與安全性</h3>

    <div class="grid">
      <!-- 基本資訊 / 2FA 狀態 / TOTP -->
      <section class="card">
        <h4>基本資訊</h4>
        <div class="row"><span class="k">名稱</span><span class="v">{{ member?.name || '-' }}</span></div>
        <div class="row"><span class="k">Email</span><span class="v">{{ member?.email }}</span></div>
        <div class="row">
          <span class="k">兩步驗證</span>
          <span class="v">
            <span class="badge" :class="twofaEnabled ? 'on' : 'off'">{{ twofaEnabled ? '已啟用' : '未啟用' }}</span>
          </span>
        </div>
        <div class="actions">
          <button type="button" class="btn" @click="gotoTotpSetup">設定 / 綁定 2FA（TOTP）</button>
          <button type="button" class="btn danger" @click="signout">登出</button>
        </div>

        <div class="divider"></div>

        <h4>Email OTP</h4>
        <p class="muted">寄 6 碼到你的 Email，勾選「記住此裝置」可 30 天免驗證。</p>
        <div class="otp-send">
          <button
            type="button" class="btn outline"
            :disabled="sending || !member?.email || countdown>0"
            @click="sendEmailOtp"
            :title="`寄送驗證碼到 ${member?.email ?? ''}`"
          >
            {{ countdown>0 ? `重新寄送 (${countdown}s)` : '寄送驗證碼' }}
          </button>
          <span v-if="devCode" class="dev-code">（devCode：{{ devCode }}）</span>
        </div>

        <div class="otp-verify">
          <input class="input" placeholder="輸入 6 碼" maxlength="6" v-model.trim="otpCode" />
          <label class="check">
            <input type="checkbox" v-model="rememberDevice">
            記住此裝置 30 天
          </label>
          <button type="button" class="btn primary"
                  :disabled="verifying || !/^[0-9]{6}$/.test(otpCode)"
                  @click="verifyEmailOtp">
            驗證（Email OTP）
          </button>
        </div>

        <p v-if="otpMsg" :class="['msg', otpOk ? 'ok':'err']">{{ otpMsg }}</p>
      </section>

      <!-- 變更密碼 -->
      <section class="card">
        <h4>變更密碼</h4>
        <div class="field">
          <label>舊密碼</label>
          <input class="input" v-model="oldPassword" type="password" autocomplete="current-password" />
        </div>
        <div class="field">
          <label>新密碼</label>
          <input class="input" v-model="newPassword" type="password" autocomplete="new-password" />
        </div>
        <div class="field">
          <label>確認新密碼</label>
          <input class="input" v-model="confirmPassword" type="password" autocomplete="new-password" />
        </div>
        <button class="btn primary" :disabled="!canChangePwd || changing" @click="changePassword">儲存變更</button>
        <p v-if="pwdMsg" :class="['msg', pwdOk ? 'ok':'err']">{{ pwdMsg }}</p>
      </section>
    </div>
  </div>
</template>

<style scoped>
.title{margin:0 0 12px}
.grid{display:grid;grid-template-columns:1fr 1fr;gap:16px}
@media (max-width: 900px){.grid{grid-template-columns:1fr}}

.card{background:#fff;border:1px solid #e9ecef;border-radius:16px;padding:18px;box-shadow:0 4px 18px rgba(0,0,0,.04);display:grid;gap:12px}
.row{display:grid;grid-template-columns:120px 1fr;gap:10px;align-items:center}
.k{color:#666}.v{font-weight:600}

.badge{display:inline-block;padding:2px 8px;border-radius:999px;font-size:12px}
.badge.on{background:#e8f7ef;color:#139a50}
.badge.off{background:#fff3f0;color:#c74a2e;border:1px solid #ffd8cc}

.actions{display:flex;gap:10px;margin-top:4px;flex-wrap:wrap}
.input{padding:10px 12px;border:1px solid #dfe3e8;border-radius:10px;width:100%}
.btn{padding:10px 12px;border-radius:10px;border:1px solid #e5e7eb;background:#f8fafc;font-weight:600;cursor:pointer}
.btn.primary{background:#0d6efd;border-color:#0d6efd;color:#fff}
.btn.outline{background:#fff;border-color:#cfd6e0}
.btn.danger{background:#ffecec;border-color:#ffd9d9;color:#c0392b}

.divider{height:1px;background:#f0f2f5;margin:6px 0}
.muted{color:#6b7280;font-size:13px}
.otp-send{display:flex;align-items:center;gap:8px}
.dev-code{color:#999;font-size:12px}
.otp-verify{display:flex;gap:10px;align-items:center;flex-wrap:wrap}
.check{display:flex;align-items:center;gap:6px;color:#555}

.field{display:grid;gap:6px}
.msg{margin:2px 0 0;font-size:13px}
.msg.ok{color:#138a36}
.msg.err{color:#c0392b}
</style>
