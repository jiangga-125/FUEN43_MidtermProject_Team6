import http, { setAccessToken, clearAccessToken } from './http'

// POST /api/auth/login 發 MVC cookie（非 JWT）
export const loginCookie = (account: string, password: string) =>
  http.post('/auth/login', { account, password }).then((r) => r.data)

// POST /api/auth/token 回 { token, expires }
export const loginToken = async (account: string, password: string) => {
  const r = await http.post('/auth/token', { account, password })
  const token = r.data?.token ?? null
  if (token) {
    setAccessToken(token) // <- 設定到 http.ts 的記憶體
  }
  return r.data
}

// me（取得使用者資訊，後端同時支援 Cookie 或 JWT）
export const me = () => http.get('/auth/me').then((r) => r.data)

// logout：呼 server logout，並清前端 token
export const logout = async () => {
  clearAccessToken()
  return http.post('/auth/logout').then((r) => r.data)
}

// 新增相容性 alias：保持舊程式碼仍可用 `import { login } from '@/api/auth'`
export { loginToken as login }
