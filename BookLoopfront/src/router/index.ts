// src/router/index.ts
import { createRouter, createWebHistory } from 'vue-router'
import { useAuth } from '@/stores/auth'
import { memberRoutes } from './member' // ⬅️ 會員中心巢狀路由（Profile/Security/...）
import Login from '@/views/Login.vue'
import Register from '@/views/Register.vue'
import Forgot from '@/views/Forgot.vue'
import Reset from '@/views/Reset.vue'
import Member from '@/views/Member.vue'
import TwoFASetup from '@/views/TwoFASetup.vue'
import AuthCallback from '@/views/AuthCallback.vue'
import MyNewPage from '@/views/BorrowCenter.vue'
import OrderCenter from '@/views/OrderCenter.vue'
import BookDetail from '@/views/BookDetail.vue'

let bootstrapped = false

const router = createRouter({
  history: createWebHistory(),
  routes: [
    // { path: '/', name: 'Home', component: Home, meta: { public: true } },
      { path: '/login', component: Login, meta: { public: true, title: '登入' } },
      { path: '/register', component: Register, meta: { public: true, title: '註冊' } },
      { path: '/forgot', component: Forgot, meta: { public: true ,title: '忘記密碼' } },
      { path: '/reset', component: Reset, meta: { public: true, title: '重設密碼' } },
      { path: '/auth-callback', component: AuthCallback, meta: { public: true, title: '外部登入跳轉' } },
      { path: '/2fa/setup', component: TwoFASetup, meta: { title: '雙因素驗證設定' } },
    { path: '/member', component: Member },
    { path: '/', component: () => import('@/views/Home.vue'), meta: { public: true } },
    { path: '/order-center', component: OrderCenter },
    { path: '/:pathMatch(.*)*', redirect: '/' },
    { path: '/BorrowCenter', name: 'BRcenter', component: MyNewPage },
    {
      path: '/member/coupons',
      name: 'MemberCoupons',
      component: () => import('@/views/MemberCoupons.vue'),
    },
    // 商品詳細頁（props: true 會把 route.params 當 props 傳入元件）
    { path: '/books/:id', name: 'BookDetail', component: BookDetail, props: true },
    // fallback（務必放最後）
    { path: '/:pathMatch(.*)*', redirect: '/' },
    { path: '/member/coupons',name: 'MemberCoupons',component: () => import('@/views/MemberCoupons.vue')},
    {path: '/review/create',name: 'CreateReview',component: () => import('@/views/CreateReview.vue')}



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
