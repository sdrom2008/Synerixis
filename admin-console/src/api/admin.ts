import http, { request } from './http'

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

/** Development only: POST /api/dev/seed-demo（可匿名；已登录时会带 Admin token） */
export function seedDemo() {
  return request<{
    ok?: boolean
    sellerId?: string
    message?: string
    created?: string[]
    skipped?: string[]
    updated?: string[]
    accounts?: Record<string, unknown>
  }>({
    url: '/api/dev/seed-demo',
    method: 'POST',
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

export function getMerchant(id: string) {
  return request<Record<string, unknown>>({
    url: `/api/admin/merchants/${encodeURIComponent(id)}`,
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
    items: {
      date: string
      count: number
      sessions?: number
      messages?: number
      aiCalls?: number
      tokens?: number
    }[]
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

export function enterMerchant(id: string) {
  return request<{
    token: string
    expiresAt?: string
    expiryMinutes?: number
    merchantId?: string
    nickname?: string
    merchantWebUrl?: string
    note?: string
  }>({
    url: `/api/admin/merchants/${encodeURIComponent(id)}/enter`,
    method: 'POST',
  })
}

function triggerCsvDownload(blob: Blob, filename: string) {
  const url = URL.createObjectURL(blob)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  a.click()
  URL.revokeObjectURL(url)
}

export async function exportAuditLogsCsv(take = 5000, shopId?: string, action?: string) {
  const params = new URLSearchParams({ take: String(take) })
  if (shopId) params.set('shopId', shopId)
  if (action) params.set('action', action)
  const res = await http.request<Blob>({
    url: `/api/admin/audit-logs/export?${params}`,
    method: 'GET',
    responseType: 'blob',
  })
  triggerCsvDownload(res.data, `admin-audit-${new Date().toISOString().slice(0, 10)}.csv`)
}

export async function exportUsageCsv(days = 30) {
  const res = await http.request<Blob>({
    url: `/api/admin/usage/export?days=${days}`,
    method: 'GET',
    responseType: 'blob',
  })
  triggerCsvDownload(res.data, `admin-usage-${new Date().toISOString().slice(0, 10)}.csv`)
}

export interface LlmProviderPreset {
  id: string
  name: string
  baseUrl: string
  model: string
}

export interface LlmProviderState {
  active?: boolean
  name?: string | null
  baseUrl?: string
  model?: string
  apiKeyConfigured?: boolean
  apiKeyHint?: string | null
  configured?: boolean
  source?: string
  effective?: Record<string, unknown>
  presets?: LlmProviderPreset[]
  note?: string
  message?: string
}

export function getLlmProvider() {
  return request<LlmProviderState>({ url: '/api/admin/llm-provider' })
}

export interface LlmProviderUpdate {
  name?: string
  baseUrl?: string
  model?: string
  apiKey?: string
  active?: boolean
  clearApiKey?: boolean
}

export function updateLlmProvider(data: LlmProviderUpdate) {
  return request<LlmProviderState>({
    url: '/api/admin/llm-provider',
    method: 'PUT',
    data: {
      name: data.name,
      Name: data.name,
      baseUrl: data.baseUrl,
      BaseUrl: data.baseUrl,
      model: data.model,
      Model: data.model,
      apiKey: data.apiKey,
      ApiKey: data.apiKey,
      active: data.active,
      Active: data.active,
      clearApiKey: data.clearApiKey,
      ClearApiKey: data.clearApiKey,
    },
  })
}

export function activateLlmProvider(active = true) {
  return request<LlmProviderState>({
    url: '/api/admin/llm-provider/activate',
    method: 'POST',
    data: { active, Active: active },
  })
}
