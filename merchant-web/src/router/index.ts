import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { ElMessage } from 'element-plus'
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
        meta: { title: '概览', roles: ['Seller', 'Supervisor', 'Admin', 'Agent'] },
      },
      {
        path: 'inbox',
        name: 'inbox',
        component: () => import('@/views/Inbox.vue'),
        meta: { title: '收件箱', flush: true, roles: ['Seller', 'Supervisor', 'Admin', 'Agent'] },
      },
      {
        path: 'shops',
        name: 'shops',
        component: () => import('@/views/Shops.vue'),
        meta: { title: '店铺绑定', roles: ['Seller', 'Supervisor', 'Admin'] },
      },
      {
        path: 'ai-settings',
        name: 'ai-settings',
        component: () => import('@/views/AiSettings.vue'),
        meta: { title: 'AI 设置', roles: ['Seller', 'Supervisor', 'Admin'] },
      },
      {
        path: 'billing',
        name: 'billing',
        component: () => import('@/views/Billing.vue'),
        meta: { title: '计费', roles: ['Seller', 'Supervisor', 'Admin'] },
      },
      {
        path: 'quick-replies',
        name: 'quick-replies',
        component: () => import('@/views/QuickReplies.vue'),
        meta: { title: '快捷回复', roles: ['Seller', 'Supervisor', 'Admin'] },
      },
      {
        path: 'team',
        name: 'team',
        component: () => import('@/views/Team.vue'),
        meta: { title: '团队', roles: ['Seller', 'Supervisor', 'Admin'] },
      },
      {
        path: 'audit',
        name: 'audit',
        component: () => import('@/views/AuditLogs.vue'),
        meta: { title: '操作日志', roles: ['Seller', 'Supervisor', 'Admin'] },
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

  const allowed = (to.meta.roles as string[] | undefined) || null
  if (allowed && allowed.length > 0) {
    const ut = auth.userType
    if (!allowed.includes(ut)) {
      ElMessage.warning('无权限访问该页面，已返回收件箱')
      return { name: 'inbox' }
    }
  }

  if (!auth.canAccessRoute(to.name)) {
    ElMessage.warning('无权限访问该页面，已返回收件箱')
    return { name: 'inbox' }
  }

  return true
})

export default router
