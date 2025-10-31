import { createRouter, createWebHistory, type RouteLocationNormalized } from 'vue-router'
import { authState, initAuth } from '@/stores/auth'

const Login = () => import('@/views/Login.vue')
const Register = () => import('@/views/Register.vue')
const MemberCenter = () => import('@/views/MemberCenter.vue')

const router = createRouter({
  history: createWebHistory(),
  routes: [
    // 首頁由 App.vue 的 isHome 判斷直接渲染，不需要在這裡另外宣告
    { path: '/login', name: 'login', component: Login },
    { path: '/register', name: 'register', component: Register },
    { path: '/member', name: 'member', component: MemberCenter, meta: { requiresAuth: true } },
    { path: '/listings', name: 'Listings', component: () => import('@/views/Listings.vue') },
  ],
  // 切換路由時自動捲到頂部，避免看到「在下面」
  scrollBehavior() {
    return { top: 0 }
  },
})

router.beforeEach(async (to: RouteLocationNormalized) => {
  if (!authState.inited) await initAuth()
  if (to.meta.requiresAuth && !authState.user) {
    return { name: 'login', query: { redirect: to.fullPath } }
  }
})

export default router
