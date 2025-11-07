// src/stores/auth.ts
import { defineStore } from 'pinia'
import http, { setAccessToken, loadTokenFromStorage, clearAccessToken } from '@/lib/http'

// 後端位址（優先取環境變數，否則用同網域）
const BACKEND = (import.meta.env.VITE_BACKEND_URL as string | undefined)?.replace(/\/$/, '') || ''

export type MemberInfo = {
  memberId: number | string
  name?: string
  email?: string
} | null

type OAuthResult = { accessToken: string; expiresAt: number }

// --- 強化版：popup + timeout + 關閉偵測，避免 Promise 卡住 ---
function openOAuthPopup(url: string, provider: 'Google' | 'Facebook' | 'LINE'): Promise<OAuthResult> {
  const w = window.open(url, `oauth_${provider.toLowerCase()}`, 'width=480,height=640')
  if (!w) return Promise.reject(new Error('無法開啟登入視窗，請關閉彈出視窗封鎖後重試'))

  return new Promise((resolve, reject) => {
    let settled = false

    const cleanup = () => {
      window.removeEventListener('message', handler)
      window.clearTimeout(timeoutId)
      window.clearInterval(closePollId)
      try { w.close() } catch { /* ignore */ }
    }

    const timeoutId = window.setTimeout(() => {
      if (settled) return
      settled = true
      cleanup()
      reject(new Error('外部登入逾時，請再試一次'))
    }, 90_000) // 90 秒超時

    function handler(ev: MessageEvent) {
      const data = ev.data
      if (!data || (data.provider !== provider.toLowerCase() && data.provider !== provider)) return
      if (settled) return
      settled = true
      cleanup()
      if (data.type === 'oauth-success' && data.accessToken) {
        resolve({ accessToken: data.accessToken, expiresAt: data.expiresAt })
      } else {
        reject(new Error(data.message || 'OAuth error'))
      }
    }
    window.addEventListener('message', handler)

    // 使用者關閉視窗的防呆（避免 Promise 卡住）
    const closePollId = window.setInterval(() => {
      if (w && w.closed) {
        window.clearInterval(closePollId)
        if (!settled) {
          settled = true
          cleanup()
          reject(new Error('已關閉登入視窗，未完成授權'))
        }
      }
    }, 500)
  })
}

export const useAuthStore = defineStore('auth', {
  state: () => ({
    token: loadTokenFromStorage() ?? '',
    member: null as MemberInfo,
    loading: false,
    error: '',
    remember: true,
  }),

  getters: {
    isMemberLoggedIn: (s) => !!s.member,
  },

  actions: {
    setRemember(v: boolean) { this.remember = v },

    // ★ 若一開始 state 裡就有 token，要同步進 http 的 Authorization
    __hydrateHttpAuthHeaderOnce() { if (this.token) setAccessToken(this.token, this.remember) },

    async setTokenAndLoadMember(token: string) {
      this.token = token
      setAccessToken(this.token, this.remember)
      const me = await http.get('/api/auth/me')
      this.member = me.data.member
      this.error = ''
    },

    async tryLoadSession() {
      this.__hydrateHttpAuthHeaderOnce()
      if (!this.token) return false

      try {
        const me = await http.get('/api/auth/me')
        this.member = me.data.member
        this.error = ''
        return true
      } catch {
        try {
          const r = await http.post('/api/auth/refresh')
          const token = r.data.token as string
          this.token = token
          setAccessToken(token, this.remember)
          const me2 = await http.get('/api/auth/me')
          this.member = me2.data.member
          this.error = ''
          return true
        } catch {
          this.member = null
          this.token = ''
          clearAccessToken()
          return false
        }
      }
    },

    async login(account: string, password: string) {
      this.loading = true; this.error = ''
      try {
        const r = await http.post('/api/auth/token', { Account: account, Password: password })
        await this.setTokenAndLoadMember(r.data.token)
      } catch (e: any) {
        this.error = e?.response?.data?.message ?? '登入失敗'
        throw e
      } finally { this.loading = false }
    },

    // ✅ 擴充：第三個參數 opt 為可選（不破壞舊呼叫）
    async loginWithEmailCode(
      account: string,
      code: string,
      opt?: { rememberDevice?: boolean; deviceHash?: string }
    ) {
      this.loading = true; this.error = ''
      try {
        const body: any = { Account: account, Code: code }
        if (opt?.rememberDevice !== undefined) body.RememberDevice = !!opt.rememberDevice
        if (opt?.deviceHash) body.DeviceHash = opt.deviceHash

        const r = await http.post('/api/auth/2fa/email/verify', body)
        await this.setTokenAndLoadMember(r.data.token)
      } catch (e: any) {
        this.error = e?.response?.data?.message ?? '驗證碼登入失敗'
        throw e
      } finally { this.loading = false }
    },

    async loginWithTotp(account: string, code: string) {
      this.loading = true; this.error = ''
      try {
        const r = await http.post('/api/auth/2fa/totp/verify', { Account: account, Code: code })
        await this.setTokenAndLoadMember(r.data.token)
      } finally { this.loading = false }
    },

    // ✅ 外部登入（popup + postMessage）：Google / Facebook / LINE
    async loginWithExternal(provider: 'Google' | 'Facebook' | 'LINE', returnPath: string) {
      this.loading = true; this.error = ''
      try {
        const base = BACKEND || window.location.origin
        const url = `${base}/api/auth/external/${provider}?returnUrl=${encodeURIComponent(returnPath)}`
        const { accessToken } = await openOAuthPopup(url, provider)
        await this.setTokenAndLoadMember(accessToken)
      } catch (e: any) {
        this.error = e?.message || '外部登入失敗'
        throw e
      } finally {
        this.loading = false
      }
    },

    // （舊版 redirect flow：已不用）保留但不再在頁面呼叫
    external(_provider: 'Google' | 'Facebook' | 'LINE', _returnUrl: string) {
      console.warn('Deprecated: use loginWithExternal instead.')
    },

    async logout() {
      try { await http.post('/api/auth/logout') } catch { /* ignore */ }
      this.member = null
      this.token = ''
      clearAccessToken()
    },
  }
})

export const useAuth = () => useAuthStore()
