import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { useAuthStore } from '@/stores/auth'

const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'login',
    component: () => import('@/views/Login.vue'),
    meta: { public: true },
  },
  {
    path: '/',
    component: () => import('@/layouts/MerchantLayout.vue'),
    redirect: '/inbox',
    children: [
      {
        path: 'overview',
        name: 'overview',
        component: () => import('@/views/Overview.vue'),
        meta: { title: '概览' },
      },
      {
        path: 'inbox',
        name: 'inbox',
        component: () => import('@/views/Inbox.vue'),
        meta: { title: '收件箱', flush: true },
      },
      {
        path: 'shops',
        name: 'shops',
        component: () => import('@/views/Shops.vue'),
        meta: { title: '店铺绑定' },
      },
      {
        path: 'ai-settings',
        name: 'ai-settings',
        component: () => import('@/views/AiSettings.vue'),
        meta: { title: 'AI 设置' },
      },
      {
        path: 'billing',
        name: 'billing',
        component: () => import('@/views/Billing.vue'),
        meta: { title: '计费' },
      },
      {
        path: 'team',
        name: 'team',
        component: () => import('@/views/Team.vue'),
        meta: { title: '团队' },
      },
    ],
  },
]

const router = createRouter({
  history: createWebHistory(),
  routes,
})

router.beforeEach((to) => {
  if (to.meta.public) return true
  const auth = useAuthStore()
  if (!auth.token) {
    return { name: 'login', query: { redirect: to.fullPath } }
  }
  return true
})

export default router
