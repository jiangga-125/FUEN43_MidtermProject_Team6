// src/lib/http.ts
import axios, { AxiosError, AxiosInstance } from 'axios'

/** 內部快取 & 設定 */
let accessToken = ''
let rememberFlag = true // true = localStorage, false = sessionStorage

const STORAGE_KEY = 'access_token'
const ALT_STORAGE_KEY = 'token' // 兼容舊 key

/** 依 remember 存/讀/清 token */
export function setAccessToken(token: string, remember: boolean = true) {
  accessToken = token
  rememberFlag = remember
  const store = remember ? window.localStorage : window.sessionStorage
  try {
    // 同步清掉另一個儲存體，避免殘留
    ;(remember ? sessionStorage : localStorage).removeItem(STORAGE_KEY)
    ;(remember ? sessionStorage : localStorage).removeItem(ALT_STORAGE_KEY)
    store.setItem(STORAGE_KEY, token)
  } catch {}
  // 同步設定 axios defaults header，避免 race condition
  try {
    // @ts-ignore
    if (!http.defaults.headers) http.defaults.headers = {}
    // @ts-ignore
    http.defaults.headers.common = http.defaults.headers.common ?? {}
    // @ts-ignore
    http.defaults.headers.common['Authorization'] = `Bearer ${token}`
  } catch {}
}

export function loadTokenFromStorage(): string | null {
  const fromLocal = localStorage.getItem(STORAGE_KEY) ?? localStorage.getItem(ALT_STORAGE_KEY)
  const fromSession = sessionStorage.getItem(STORAGE_KEY) ?? sessionStorage.getItem(ALT_STORAGE_KEY)
  let t: string | null = null
  if (fromLocal) {
    t = fromLocal
    accessToken = t
    rememberFlag = true
  } else if (fromSession) {
    t = fromSession
    accessToken = t
    rememberFlag = false
  }
  // 若讀到 token，亦設定 axios defaults（方便立即可用）
  if (t) {
    try {
      // @ts-ignore
      http.defaults.headers = http.defaults.headers ?? {}
      // @ts-ignore
      http.defaults.headers.common = http.defaults.headers.common ?? {}
      // @ts-ignore
      http.defaults.headers.common['Authorization'] = `Bearer ${t}`
    } catch {}
  }
  return t
}

export function clearAccessToken() {
  accessToken = ''
  try {
    localStorage.removeItem(STORAGE_KEY)
    localStorage.removeItem(ALT_STORAGE_KEY)
    sessionStorage.removeItem(STORAGE_KEY)
    sessionStorage.removeItem(ALT_STORAGE_KEY)
  } catch {}
  // 移除 axios defaults header
  try {
    // @ts-ignore
    if (http.defaults?.headers?.common) delete http.defaults.headers.common['Authorization']
  } catch {}
}

/** 建立 axios instance（經由 vite proxy 打到後端） */
const http: AxiosInstance = axios.create({
  baseURL: '/', // 走 /api 會被 Vite 代理到後端
  withCredentials: true, // 讓 refreshToken cookie 帶上
})

/** Request：帶 Authorization */
http.interceptors.request.use((config) => {
  if (accessToken) {
    config.headers = config.headers ?? {}
    ;(config.headers as any).Authorization = `Bearer ${accessToken}`
  }
  return config
})

/** Response：401 時嘗試 refresh（簡易旋轉一次） */
let refreshing = false
let waiters: Array<(newToken?: string | null) => void> = []

async function runRefresh() {
  try {
    const resp = await http.post('/api/auth/refresh')
    // 支援不同欄位名稱
    const newToken = resp.data?.token ?? resp.data?.access_token ?? null
    if (newToken) {
      setAccessToken(newToken, rememberFlag)
      return newToken
    } else {
      clearAccessToken()
      throw new Error('no token in refresh response')
    }
  } catch (e) {
    clearAccessToken()
    throw e
  }
}

http.interceptors.response.use(
  (res) => res,
  async (err: AxiosError) => {
    const resp = err.response
    const original: any = err.config || {}

    // 不是 401 或已重試過 → 直接丟出
    if (resp?.status !== 401 || original._retried) throw err

    // 避免在 /auth 本身無限 refresh
    if (String(original.url || '').includes('/api/auth/')) throw err

    // 只旋轉一次：其他請求等待
    if (!refreshing) {
      refreshing = true
      try {
        await runRefresh()
        refreshing = false
        // 喚醒等待者
        waiters.forEach((w) => w())
        waiters = []
      } catch (e) {
        refreshing = false
        waiters.forEach((w) => w(null))
        waiters = []
        throw err
      }
    } else {
      await new Promise<void>((resolve) => waiters.push(() => resolve()))
    }

    // 標記已重試，帶新 token 再打一次
    original._retried = true
    original.headers = original.headers ?? {}
    if (accessToken) {
      original.headers.Authorization = `Bearer ${accessToken}`
    }
    return http(original)
  },
)

export default http
