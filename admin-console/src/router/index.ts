import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'

const routes: RouteRecordRaw[] = [
  {
    path: '/login',
    name: 'login',
    component: () => import('@/views/Login.vue'),
    meta: { public: true },
  },
  {
    path: '/',
    component: () => import('@/layouts/AdminLayout.vue'),
    redirect: '/dashboard',
    children: [
      { path: 'dashboard', name: 'dashboard', component: () => import('@/views/Dashboard.vue'), meta: { title: '概览' } },
      { path: 'merchants', name: 'merchants', component: () => import('@/views/Merchants.vue'), meta: { title: '商家' } },
      { path: 'shops', name: 'shops', component: () => import('@/views/Shops.vue'), meta: { title: '店铺连接' } },
      { path: 'sessions', name: 'sessions', component: () => import('@/views/Sessions.vue'), meta: { title: '会话监控' } },
      { path: 'usage', name: 'usage', component: () => import('@/views/Usage.vue'), meta: { title: '用量计费' } },
      { path: 'settings', name: 'settings', component: () => import('@/views/Settings.vue'), meta: { title: '系统设置' } },
    ],
  },
]

const router = createRouter({
  history: createWebHistory(),
  routes,
})

router.beforeEach((to) => {
  if (to.meta.public) return true
  const token = localStorage.getItem('sx_admin_token')
  if (!token) return { name: 'login', query: { redirect: to.fullPath } }
  return true
})

export default router
