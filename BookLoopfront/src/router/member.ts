// src/router/member.ts
import { RouteRecordRaw } from 'vue-router'

export const memberRoutes: RouteRecordRaw = {
    path: '/member',
    component: () => import('@/views/member/MemberCenterLayout.vue'),
    meta: { requiresAuth: true }, // 需要登入
    children: [
        { path: '', redirect: { name: 'member-profile' } },
        {
            path: 'profile',
            name: 'member-profile',
            component: () => import('@/views/member/Profile.vue'),
            meta: { title: '會員資料維護' }
        },
        {
            path: 'security',
            name: 'member-security',
            component: () => import('@/views/member/Security.vue'),
            meta: { title: '帳號密碼與安全性' }
        },
        {
            path: 'orders',
            name: 'member-orders',
            component: () => import('@/views/member/Orders.vue'),
            meta: { title: '訂單紀錄' }
        },
        {
            path: 'borrows',
            name: 'member-borrows',
            component: () => import('@/views/member/Borrows.vue'),
            meta: { title: '借書紀錄' }
        },
        {
            path: 'coupons',
            name: 'member-coupons',
            component: () => import('@/views/member/Coupons.vue'),
            meta: { title: '優惠券' }
        },
        {
            path: 'favorites',
            name: 'member-favorites',
            component: () => import('@/views/member/Favorites.vue'),
            meta: { title: '收藏' }
        },
        {
            path: 'cart',
            name: 'member-cart',
            component: () => import('@/views/member/Cart.vue'),
            meta: { title: '購物車' }
        },
        {
            path: 'reviews',
            name: 'member-reviews',
            component: () => import('@/views/member/Reviews.vue'),
            meta: { title: '評論' }
        }
    ]
}
