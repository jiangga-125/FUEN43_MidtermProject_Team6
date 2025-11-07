// src/stores/auth.ts
import { defineStore } from 'pinia'
import http, { setAccessToken, loadTokenFromStorage, clearAccessToken } from '@/lib/http'

export type MemberInfo = {
  memberId: number | string
  name?: string
  email?: string
} | null

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

    // ★ 這行是關鍵：若一開始 state 裡就有 token，要同步進 http 的 Authorization
    __hydrateHttpAuthHeaderOnce() { if (this.token) setAccessToken(this.token, this.remember) },

    async setTokenAndLoadMember(token: string) {
      this.token = token
      console.log('🟢 設定 token:', token.slice(0, 20) + '...')
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

    async loginWithEmailCode(account: string, code: string) {
      this.loading = true; this.error = ''
      try {
        const r = await http.post('/api/auth/2fa/email/verify', { Account: account, Code: code })
        await this.setTokenAndLoadMember(r.data.token)
      } finally { this.loading = false }
    },

    async loginWithTotp(account: string, code: string) {
      this.loading = true; this.error = ''
      try {
        const r = await http.post('/api/auth/2fa/totp/verify', { Account: account, Code: code })
        await this.setTokenAndLoadMember(r.data.token)
      } finally { this.loading = false }
    },

    external(provider: 'Google' | 'Facebook' | 'LINE', returnUrl: string) {
      location.href = `/api/auth/external/${provider}?returnUrl=${encodeURIComponent(returnUrl)}`
    },

    async logout() {
      try { await http.post('/api/auth/logout') } catch { }
      this.member = null
      this.token = ''
      clearAccessToken()
    },
  }
})

export const useAuth = () => useAuthStore()
