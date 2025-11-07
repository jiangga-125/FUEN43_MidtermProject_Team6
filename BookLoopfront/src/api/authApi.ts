// src/api/authApi.ts
import http from '@/lib/http'
import type { Ref } from 'vue'
export interface CurrentUser {
  memberId?: number
  MemberID?: number
  username?: string
  // 根據後端回傳再加欄位
}

// 登入
export const login = (email: string, password: string) =>
  http.post('/api/auth/token', { Account: email, Password: password })
// 重新整理 Token
export const refresh = () => http.post('/api/auth/refresh')
// 取得目前使用者資訊
export async function getCurrentUser(): Promise<CurrentUser | null> {
  try {
    const r = await http.get('/auth/me') // 對應後端: GET /api/auth/me
    return r.data ?? null
  } catch (err) {
    // 若 401 或其他錯誤，回傳 null
    // console.warn('getCurrentUser failed', err)
    return null
  }
}

// 從 JWT 解 payload（簡單版）
export function decodeToken(token: string) {
  try {
    const parts = token.split('.')
    if (parts.length < 2) return null
    const payload = JSON.parse(atob(parts[1].replace(/-/g, '+').replace(/_/g, '/')))
    return payload
  } catch (e) {
    return null
  }
}
// 從 localStorage 取得 token（若有）
function getTokenFromStorage(): string | null {
  return localStorage.getItem('token') || localStorage.getItem('access_token') || null
}

// 把 Authorization header 加到 axios instance（若使用 localStorage 保存 token）
export function attachAuthHeader() {
  const token = getTokenFromStorage()
  if (token) {
    http.defaults.headers = http.defaults.headers ?? {}
    // axios 型別：確保 common 存在
    // @ts-ignore: 這行為確保在 runtime 把 header 設上
    http.defaults.headers.common = http.defaults.headers.common ?? {}
    // 設定 Bearer token
    // @ts-ignore
    http.defaults.headers.common['Authorization'] = `Bearer ${token}`
  } else {
    // @ts-ignore
    if (http.defaults.headers?.common) delete http.defaults.headers.common['Authorization']
  }
}

// 登出（清 token、移除 header）
export function logout() {
  localStorage.removeItem('token')
  localStorage.removeItem('access_token')
  delete http.defaults.headers.common['Authorization']
  // 若你有 refresh token、logout endpoint，可在此呼叫後端
}

/**
 * 核心：嘗試用後端 /auth/me，失敗則 fallback 解 token
 * 使用方式：await loadMemberInfo(memberIdRef)
 */
export async function loadMemberInfo(memberIdRef: Ref<number | null | undefined>) {
  // 1) 優先：呼叫後端驗證的 /api/auth/me
  try {
    const me = await getCurrentUser()
    if (me) {
      const id = me.memberId ?? me.MemberID ?? null
      memberIdRef.value = id ?? null
      return
    }
  } catch (e) {
    // 忽略，走 fallback
  }

  // 2) fallback: 從 localStorage 的 token decode（注意安全性）
  try {
    const token = getTokenFromStorage()
    if (!token) {
      memberIdRef.value = null
      return
    }
    const payload = decodeToken(token)
    if (!payload) {
      memberIdRef.value = null
      return
    }
    const maybeId = payload.memberId ?? payload.userId ?? payload.sub ?? payload.id ?? null
    memberIdRef.value = maybeId ?? null
  } catch (e) {
    memberIdRef.value = null
  }
}

// Email OTP（加上 Purpose，可選：'Login' | 'Register' | 'ResetPassword'）
export const sendEmailCode = (
  email: string,
  purpose: 'Login' | 'Register' | 'ResetPassword' = 'Login',
) => http.post('/api/auth/email/send', { Account: email, Purpose: purpose })

// Email OTP
export const loginWithEmailCode = (email: string, code: string) =>
  http.post('/api/auth/2fa/email/verify', { Account: email, Code: code })

// TOTP
export const totpBind = (email: string) => http.post('/api/auth/2fa/totp/bind', { Account: email })
export const loginWithTotp = (email: string, code: string) =>
  http.post('/api/auth/2fa/totp/verify', { Account: email, Code: code })

// Register / Forgot / Reset
export const register = (email: string, password: string, username?: string) =>
  http.post('/api/auth/register', { Email: email, Password: password, Username: username })

export const forgot = (email: string) =>
  http.post('/api/auth/email/send', { Account: email, Purpose: 'ResetPassword' })

export const reset = (email: string, code: string, newPassword: string) =>
  http.post('/api/auth/reset/confirm', { Account: email, Code: code, Password: newPassword })

// 外部登入導向
export const externalUrl = (provider: 'Google' | 'Facebook' | 'LINE', returnUrl: string) =>
  `/api/auth/external/${provider}?returnUrl=${encodeURIComponent(returnUrl)}`

// ✅ 變更密碼
// 預期後端路由：POST /api/auth/password/change
// Request: { Old: string, New: string }；成功回 200（可帶 message）
export const changePassword = (oldPwd: string, newPwd: string) =>
  http.post('/api/auth/password/change', { Old: oldPwd, New: newPwd })
