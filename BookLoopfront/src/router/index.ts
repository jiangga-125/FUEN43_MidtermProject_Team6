// src/router/index.ts
import { createRouter, createWebHistory, type RouteLocationNormalized } from 'vue-router'
import { authState, initAuth } from '@/stores/auth'

// lazy-load views
const Login = () => import('@/views/Login.vue')
const Register = () => import('@/views/Register.vue')
const MemberCenter = () => import('@/views/MemberCenter.vue')
const Listings = () => import('@/views/Listings.vue')
// const ListingDetail = () => import('@/views/ListingDetail.vue') // 建議建立對應檔案
// const Cart = () => import('@/views/Cart.vue') // 若尚未建立可先建立空檔或註解此路由

const routes = [
  // 根路由導向 /listings，避免 No match for "/"
  { path: '/', redirect: '/listings' },

  // 列表與詳細頁
  { path: '/listings', name: 'Listings', component: Listings },
  // { path: '/listings/:id', name: 'ListingDetail', component: ListingDetail, props: true },

  // 登入 / 註冊 / 會員中心
  { path: '/login', name: 'login', component: Login },
  { path: '/register', name: 'register', component: Register },
  { path: '/member', name: 'member', component: MemberCenter, meta: { requiresAuth: true } },

  // 購物車
  // { path: '/cart', name: 'cart', component: Cart },

  // fallback：所有未知路由導回首頁（或改成 404 page）
  { path: '/:pathMatch(.*)*', redirect: '/' },
]

const router = createRouter({
  history: createWebHistory(import.meta.env.BASE_URL),
  routes,
  // 切換路由時自動捲到頂端
  scrollBehavior() {
    return { top: 0 }
  },
})

// 全域前置：初始化 auth，並保護需要驗證的 route
router.beforeEach(async (to: RouteLocationNormalized) => {
  // 確保 authState 已初始化（例如從 localStorage 還原 token/user）
  if (!authState.inited) {
    try {
      await initAuth()
    } catch (e) {
      console.error('[router] initAuth fail', e)
    }
  }

  // 如果該 route 需要登入，但目前沒有 user，導到 login，並帶 redirect
  if (to.meta?.requiresAuth && !authState.user) {
    return { name: 'login', query: { redirect: to.fullPath } }
  }

  // 如果已登入但去 login 頁面，可選擇導回會員中心（可視需求開啟）
  // if (to.name === 'login' && authState.user) {
  //   return { name: 'member' }
  // }
})

export default router
