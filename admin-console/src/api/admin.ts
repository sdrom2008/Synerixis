import { request } from './http'

export interface AgentLoginResult {
  token: string
  userId?: string
  agentId?: string
  userType?: string
  role?: string
  name?: string
  nickname?: string
}

export function agentLogin(email: string, password: string) {
  return request<AgentLoginResult>({
    url: '/api/auth/agent-login',
    method: 'POST',
    data: { Email: email, email, Password: password, password },
  })
}

export function getDashboard() {
  return request<Record<string, unknown>>({ url: '/api/admin/dashboard' })
}

export function getMerchants(page = 1, pageSize = 20, q?: string) {
  const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  if (q) params.set('q', q)
  return request<{ page: number; pageSize: number; total: number; items: Record<string, unknown>[] }>({
    url: `/api/admin/merchants?${params}`,
  })
}

export function getShops(page = 1, pageSize = 50) {
  return request<{ page: number; pageSize: number; total: number; items: Record<string, unknown>[] }>({
    url: `/api/admin/shops?page=${page}&pageSize=${pageSize}`,
  })
}

export function getSessions(take = 50) {
  return request<{ take: number; total: number; items: Record<string, unknown>[] }>({
    url: `/api/admin/sessions?take=${take}`,
  })
}

export function getUsage() {
  return request<Record<string, unknown>>({ url: '/api/admin/usage' })
}

export function getUsageDaily(days = 7) {
  return request<{
    days: number
    items: { date: string; count: number; sessions?: number; messages?: number }[]
    hasData?: boolean
  }>({ url: `/api/admin/usage/daily?days=${days}` })
}

export function getSettings() {
  return request<Record<string, unknown>>({ url: '/api/admin/settings' })
}
