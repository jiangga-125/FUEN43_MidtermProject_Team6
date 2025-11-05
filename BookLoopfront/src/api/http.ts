import axios, { AxiosRequestConfig } from 'axios'

// -------------------------
// token 存放（記憶體）
// -------------------------
let accessToken: string | null = null

/** 設定/更新 access token（由 auth.ts 在登入後呼叫） */
export function setAccessToken(token: string | null) {
  accessToken = token
}

/** 清除 access token（ logout 時呼） */
export function clearAccessToken() {
  accessToken = null
}

// axios instance
const API_BASE = 'https://localhost:7176/api' // 若用 dev proxy，改成 '/api'
const http = axios.create({
  baseURL: API_BASE,
  withCredentials: true, // 讓瀏覽器傳送 HttpOnly refresh cookie
})

// 為 refresh 專用的 client（避免會觸發相同的 interceptor）
const refreshClient = axios.create({
  baseURL: API_BASE,
  withCredentials: true,
})

// request interceptor：自動加 Authorization header（若有 token）
http.interceptors.request.use((config) => {
  if (accessToken) {
    config.headers = config.headers ?? {}
    // 若 header 已有 Authorization 就不要覆蓋
    if (!('Authorization' in config.headers)) {
      config.headers['Authorization'] = `Bearer ${accessToken}`
    }
  }
  return config
})

// response interceptor：遇到 401 嘗試 refresh，重試原請求（支援排隊）
let isRefreshing = false
let failedQueue: Array<{
  resolve: (token?: string) => void
  reject: (err: any) => void
}> = []

const processQueue = (error: any, token: string | null = null) => {
  failedQueue.forEach((p) => {
    if (error) p.reject(error)
    else p.resolve(token ?? undefined)
  })
  failedQueue = []
}

http.interceptors.response.use(
  (resp) => resp,
  async (error) => {
    const originalReq = error.config as AxiosRequestConfig & { _retry?: boolean }

    // 若沒有 response 或不是 401，直接拋錯
    if (!error.response || error.response.status !== 401) {
      return Promise.reject(error)
    }

    // 避免對 refresh endpoint 再次觸發
    if (originalReq && (originalReq.url ?? '').endsWith('/auth/refresh')) {
      // refresh 失敗了 -> forward error
      return Promise.reject(error)
    }

    if (originalReq && originalReq._retry) {
      return Promise.reject(error)
    }

    originalReq._retry = true

    if (isRefreshing) {
      // 已在 refresh 中，加入排隊，等 refresh 完成後重試
      return new Promise((resolve, reject) => {
        failedQueue.push({ resolve, reject })
      }).then((token) => {
        if (!originalReq.headers) originalReq.headers = {}
        if (token) originalReq.headers['Authorization'] = `Bearer ${token}`
        return http(originalReq)
      })
    }

    isRefreshing = true

    try {
      // POST /api/auth/refresh（伺服器用 HttpOnly cookie 讀 refresh token）
      const r = await refreshClient.post('/auth/refresh')
      const newToken = r.data?.token
      if (!newToken) throw new Error('refresh 沒回 token')

      // 設新 token 並讓排隊的 request 重試
      setAccessToken(newToken)
      processQueue(null, newToken)

      // 把 Authorization header 加回原請求並重試
      if (!originalReq.headers) originalReq.headers = {}
      originalReq.headers['Authorization'] = `Bearer ${newToken}`
      return http(originalReq)
    } catch (err) {
      processQueue(err, null)
      // refresh 失敗（可能 cookie 過期或被撤銷） -> 清 token，讓前端導回登入
      clearAccessToken()
      return Promise.reject(err)
    } finally {
      isRefreshing = false
    }
  },
)

export default http
