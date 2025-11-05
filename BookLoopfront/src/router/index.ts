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
import OrderCenter from '@/views/OrderCenter.vue'

const router = createRouter({
  history: createWebHistory(),
  routes: [
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
    { path: '/member/coupons',name: 'MemberCoupons',component: () => import('@/views/MemberCoupons.vue')
}

  ],
})

let bootstrapped = false
router.beforeEach(async (to) => {
  const auth = useAuth()
  if (!bootstrapped) { bootstrapped = true; await auth.tryLoadSession() }
  if (to.meta.public) return true
  if (!auth.member) return { path: '/login', query: { redirect: to.fullPath } }
  return true
})

export default router
