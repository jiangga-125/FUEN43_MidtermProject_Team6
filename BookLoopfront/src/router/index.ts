// src/router/index.ts
import { createRouter, createWebHistory } from 'vue-router'
import { useAuth } from '@/stores/auth'
import { memberRoutes } from './member' // ⬅️ 會員中心巢狀路由（Profile/Security/...）

// ✅ 建議頁面改為 lazy-load，縮小首屏體積
const Login = () => import('@/views/Login.vue')
const Register = () => import('@/views/Register.vue')
const Forgot = () => import('@/views/Forgot.vue')
const Reset = () => import('@/views/Reset.vue')
const TwoFASetup = () => import('@/views/TwoFASetup.vue')
const AuthCallback = () => import('@/views/AuthCallback.vue')
const Home = () => import('@/views/Home.vue')

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', component: Home, meta: { public: true, title: '首頁' } },

    // 🔐 Auth flows
    { path: '/login', component: Login, meta: { public: true, title: '登入' } },
    { path: '/register', component: Register, meta: { public: true, title: '註冊' } },
    { path: '/forgot', component: Forgot, meta: { public: true, title: '忘記密碼' } },
    { path: '/reset', component: Reset, meta: { public: true, title: '重設密碼' } },
    { path: '/auth-callback', component: AuthCallback, meta: { public: true, title: '外部登入跳轉' } },

    // 2FA 設定頁（需登入）
    { path: '/2fa/setup', component: TwoFASetup, meta: { title: '雙因素驗證設定' } },

    // 🧑‍💻 會員中心（整包子頁：profile/security/orders/...）
    memberRoutes,

    // 兜底：回首頁
    { path: '/:pathMatch(.*)*', redirect: '/' },
  ],

  // 捲動行為：切頁回到頂端
  scrollBehavior() {
    return { top: 0 }
  },
})

/* ----------------- 全域守門 ----------------- */
let bootstrapped = false
router.beforeEach(async (to) => {
  const auth = useAuth()

  // 第一次進站嘗試載入會話（例如從 localStorage 取 token 後載入會員）
  if (!bootstrapped) {
    bootstrapped = true
    // 與你現有 store 對齊（你原本就有 tryLoadSession）
    await auth.tryLoadSession?.()
  }

  // 公開頁面直接通過
  if (to.meta?.public) return true

  // 需要登入：沒會員就導去登入並附上 returnUrl
  if (!auth.member) {
    return { path: '/login', query: { returnUrl: to.fullPath } }
  }

  // 如之後擴充權限：可在這裡判斷 to.meta.perm 與 auth 的 claims/roles

  return true
})

/* ----------------- 動態標題 ----------------- */
router.afterEach((to) => {
  // 以子路由 meta.title 為優先
  const title = (to.meta?.title as string) ?? 'BookLoop'
  document.title = `${title} - BookLoop`
})

export default router
