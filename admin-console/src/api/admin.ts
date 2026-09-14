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

export interface AdminSettingsUpdate {
  maintenanceMode?: boolean
  defaultOutboundMode?: string
  allowNewRegistration?: boolean
}

export function updateSettings(data: AdminSettingsUpdate) {
  return request<{
    message?: string
    maintenanceMode?: boolean
    defaultOutboundMode?: string
    allowNewRegistration?: boolean
  }>({
    url: '/api/admin/settings',
    method: 'PUT',
    data: {
      maintenanceMode: data.maintenanceMode,
      MaintenanceMode: data.maintenanceMode,
      defaultOutboundMode: data.defaultOutboundMode,
      DefaultOutboundMode: data.defaultOutboundMode,
      allowNewRegistration: data.allowNewRegistration,
      AllowNewRegistration: data.allowNewRegistration,
    },
  })
}

export function getAuditLogs(take = 50, shopId?: string, action?: string) {
  const params = new URLSearchParams({ take: String(take) })
  if (shopId) params.set('shopId', shopId)
  if (action) params.set('action', action)
  return request<{ items: Record<string, unknown>[]; total: number; take: number }>({
    url: `/api/admin/audit-logs?${params}`,
  })
}

export function setMerchantActive(id: string, isActive: boolean) {
  return request<{ id: string; isActive: boolean; message?: string }>({
    url: `/api/admin/merchants/${encodeURIComponent(id)}/active`,
    method: 'PATCH',
    data: { isActive, IsActive: isActive },
  })
}

export function setMerchantSubscription(id: string, level: string) {
  return request<{ id: string; subscriptionLevel: string; message?: string }>({
    url: `/api/admin/merchants/${encodeURIComponent(id)}/subscription`,
    method: 'PATCH',
    data: { level, Level: level },
  })
}
