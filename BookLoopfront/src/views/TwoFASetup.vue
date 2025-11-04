<!-- src/views/TwoFASetup.vue -->
<template>
  <div class="wrap">
    <h2>設定 TOTP 驗證器</h2>

    <div class="card">
      <div class="row">
        <label class="field">
          <span>帳號（Email）</span>
          <input v-model.trim="account" placeholder="you@example.com" />
        </label>
        <button class="btn" :disabled="loading || !isEmail(account)" @click="begin">
          產生密鑰與 QR
        </button>
      </div>

      <div v-if="secret" class="result">
        <!-- 用 canvas 呈現 QR，避免任何 src="..." -->
        <div class="qr">
          <canvas ref="qrcanvas" width="220" height="220"></canvas>
        </div>

        <p class="muted">或手動輸入密鑰：<code>{{ secret }}</code></p>

        <div class="row">
          <label class="field">
            <span>驗證器 6 碼</span>
            <input v-model.trim="code" maxlength="6" placeholder="123456" />
          </label>
          <button class="btn btn-primary" :disabled="loading || !/^[0-9]{6}$/.test(code)" @click="verify">
            確認綁定
          </button>
        </div>
      </div>

      <p v-if="err" class="err">{{ err }}</p>
      <p v-if="ok" class="ok">✅ 綁定成功，之後即可用 TOTP 登入。</p>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, nextTick } from 'vue'
import * as QRCode from 'qrcode'        // 保險用法
import http from '@/lib/http'

const account = ref('')
const secret = ref<string|null>(null)
const otpauth = ref<string|null>(null)
const code = ref('')
const loading = ref(false)
const err = ref('')
const ok = ref(false)

const qrcanvas = ref<HTMLCanvasElement | null>(null)

function isEmail(s: string) { return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(s) }

async function begin () {
  if (!isEmail(account.value)) { err.value = '請先輸入 Email'; return }
  loading.value = true; err.value = ''; ok.value = false
  try {
    // 後端：POST /api/auth/2fa/totp/bind  → { secret, otpauth }
    const { data } = await http.post('/api/auth/2fa/totp/bind', { Account: account.value })
    secret.value = data.secret
    otpauth.value = data.otpauth

    // 畫 QR 到 canvas
    await nextTick()
    if (qrcanvas.value && otpauth.value) {
      await QRCode.toCanvas(qrcanvas.value, otpauth.value, { margin: 1, scale: 6 })
    }
  } catch (e: any) {
    err.value = e?.response?.data?.message ?? '產生密鑰失敗'
  } finally {
    loading.value = false
  }
}

async function verify () {
  if (!secret.value) return
  loading.value = true; err.value = ''
  try {
    // 後端：POST /api/auth/2fa/totp/verify  → 200 綁定成功
    await http.post('/api/auth/2fa/totp/verify', { Account: account.value, Code: code.value })
    ok.value = true
  } catch (e: any) {
    err.value = e?.response?.data?.message ?? '驗證失敗'
  } finally {
    loading.value = false
  }
}
</script>

<style scoped>
.wrap{min-height:100vh;display:grid;place-items:center;background:#fafafa;padding:24px}
.card{width:min(92vw,560px);background:#fff;border:1px solid #e9ecef;border-radius:16px;padding:16px 16px 12px;box-shadow:0 6px 24px rgba(0,0,0,.06);display:grid;gap:12px}
.row{display:flex;gap:12px;align-items:flex-end}
.field{display:grid;gap:6px;flex:1}
.field input{padding:10px 12px;border:1px solid #dfe3e8;border-radius:10px}
.btn{padding:10px 12px;border-radius:10px;border:1px solid #e5e7eb;background:#f5f5f5;cursor:pointer}
.btn-primary{background:#0d6efd;color:#fff;border-color:#0d6efd}
.qr{display:grid;place-items:center}
.muted{color:#666}
.err{color:#b00020;background:#fdecec;border:1px solid #f5c2c7;padding:8px;border-radius:8px}
.ok{color:#0a7a2f;background:#eaf7ee;border:1px solid #bfe5c9;padding:8px;border-radius:8px}
code{background:#f6f8fa;padding:2px 6px;border-radius:6px}
</style>
