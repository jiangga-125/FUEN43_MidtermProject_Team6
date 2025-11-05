// src/router/index.ts
import { createRouter, createWebHistory } from 'vue-router'
import { useAuth } from '@/stores/auth'

import Login from '@/views/Login.vue'
import Register from '@/views/Register.vue'
import Forgot from '@/views/Forgot.vue'
import Reset from '@/views/Reset.vue'
import Member from '@/views/Member.vue'
import TwoFASetup from '@/views/TwoFASetup.vue'
import AuthCallback from '@/views/AuthCallback.vue'
import MyNewPage from '@/views/BorrowCenter.vue';
import OrderCenter from '@/views/OrderCenter.vue'
import BookDetail from '@/views/BookDetail.vue'

let bootstrapped = false

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/', name: 'Home', component: Home, meta: { public: true } },
    { path: '/login', component: Login, meta: { public: true } },
    { path: '/register', component: Register, meta: { public: true } },
    { path: '/forgot', component: Forgot, meta: { public: true } },
    { path: '/reset', component: Reset, meta: { public: true } },
    { path: '/auth-callback', component: AuthCallback, meta: { public: true } },
    { path: '/2fa/setup', component: TwoFASetup },
    { path: '/member', component: Member },
    { path: '/', component: () => import('@/views/Home.vue'), meta: { public: true } },
    { path: '/order-center', component: OrderCenter },
    { path: '/:pathMatch(.*)*', redirect: '/' },
    { path: '/BorrowCenter',name: 'BRcenter',component: MyNewPage},
    { path: '/member/coupons',name: 'MemberCoupons',component: () => import('@/views/MemberCoupons.vue'),},
    // 商品詳細頁（props: true 會把 route.params 當 props 傳入元件）
    { path: '/books/:id', name: 'BookDetail', component: BookDetail, props: true },
   // fallback（務必放最後）
    { path: '/:pathMatch(.*)*', redirect: '/' },
]
})

const routes = [
  { path: '/', name: 'Home', component: Home },
  // 詳細頁 route，使用 params 傳 id
  { path: '/books/:id', name: 'BookDetail', component: BookDetail, props: true },
]

router.beforeEach(async (to) => {
  const auth = useAuth()
  if (!bootstrapped) {
    bootstrapped = true
    // 嘗試載入 session（若你有實作）
    if (typeof auth.tryLoadSession === 'function') {
      await auth.tryLoadSession()
    }
  }

  // 若 route 標記為 public（不需登入）就放行
  if (to.meta && (to.meta as any).public) return true

  // 否則檢查是否為登入狀態
  if (!auth.member) {
    return { path: '/login', query: { redirect: to.fullPath } }
  }

  return true
  })

export default router
