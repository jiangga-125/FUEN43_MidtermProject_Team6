// src/api/authApi.ts
import http from '@/lib/http'

export const login = (email: string, password: string) =>
  http.post('/api/auth/token', { Account: email, Password: password })

export const me = () => http.get('/api/auth/me')
export const refresh = () => http.post('/api/auth/refresh')
export const logout = () => http.post('/api/auth/logout')

// Email OTP（加上 Purpose，可選：'Login' | 'Register' | 'ResetPassword'）
export const sendEmailCode = (email: string, purpose: 'Login' | 'Register' | 'ResetPassword' = 'Login') =>
  http.post('/api/auth/email/send', { Account: email, Purpose: purpose })

export const loginWithEmailCode = (email: string, code: string) =>
  http.post('/api/auth/2fa/email/verify', { Account: email, Code: code })

// TOTP
export const totpBind = (email: string) =>
  http.post('/api/auth/2fa/totp/bind', { Account: email })

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
