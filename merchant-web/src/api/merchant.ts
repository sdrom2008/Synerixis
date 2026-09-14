import { request } from './http'

export interface SessionItem {
  id: string
  sessionId: string
  customerName?: string
  platform?: string
  status?: string
  priority?: string
  assignedAgent?: { id: string; name: string } | null
  createdAt?: string
  lastActiveAt?: string
  lastBuyerMessageAt?: string | null
  messageCount?: number
  hasPendingDraft?: boolean
  unreadBuyerCount?: number
  hoursSinceLastBuyerMsg?: number
  needsResponseBy?: string
}

export interface MessageItem {
  id: string
  content: string
  senderType: string
  messageType?: string
  createdAt: string
}

export interface DraftInfo {
  id: string
  content: string
  status?: string
  createdAt?: string
  updatedAt?: string
}

export interface SessionMessagesResult {
  sessionStatus?: string
  lastBuyerMessageAt?: string | null
  hoursSinceLastBuyerMsg?: number
  needsResponseBy?: string
  pendingDraft?: DraftInfo | null
  items: MessageItem[]
}

export interface DashboardKpis {
  sessionsToday?: number | null
  pendingHandoff?: number | null
  pendingDrafts?: number | null
  connectedShops?: number | null
  autoResolveRate?: number | null
  messagesThisMonth?: number | null
  generatedAt?: string
}

export function getSessions(status?: string) {
  const q = status ? `?status=${encodeURIComponent(status)}` : ''
  return request<{ items: SessionItem[]; total: number; pendingDraftCount: number }>({
    url: `/api/merchant/sessions${q}`,
  })
}

export function getSessionMessages(id: string) {
  return request<SessionMessagesResult>({
    url: `/api/merchant/sessions/${id}/messages`,
  })
}

export function getPendingDrafts() {
  return request<{ items: unknown[]; total: number }>({
    url: '/api/merchant/drafts',
  })
}

export function getSessionDraft(id: string) {
  return request<{ draft: DraftInfo | null; hoursSinceLastBuyerMsg?: number; needsResponseBy?: string }>({
    url: `/api/merchant/sessions/${id}/draft`,
  })
}

export function updateDraft(id: string, content: string) {
  return request<{ message: string; draftId: string; content: string }>({
    url: `/api/merchant/sessions/${id}/draft`,
    method: 'PUT',
    data: { content },
  })
}

export function approveDraft(id: string) {
  return request({
    url: `/api/merchant/sessions/${id}/draft/approve`,
    method: 'POST',
  })
}

export function editAndSendDraft(id: string, content: string) {
  return request({
    url: `/api/merchant/sessions/${id}/draft/edit-send`,
    method: 'POST',
    data: { content },
  })
}

export function discardDraft(id: string) {
  return request({
    url: `/api/merchant/sessions/${id}/draft/discard`,
    method: 'POST',
  })
}

export function getMerchantDashboard() {
  return request<DashboardKpis>({ url: '/api/merchant/dashboard' })
}

export function getMerchantUsage() {
  return request<Record<string, unknown>>({ url: '/api/merchant/usage' })
}

export function getPlatforms() {
  return request({ url: '/api/merchant/platforms' })
}

export function getConnections() {
  return request({ url: '/api/merchant/connections' })
}

export function getBindUrl(platform: string) {
  return request<{ url?: string; authorizeUrl?: string }>({
    url: `/api/merchant/bind/${encodeURIComponent(platform)}`,
  })
}

export function unbindPlatform(platform: string) {
  return request({
    url: `/api/merchant/unbind/${encodeURIComponent(platform)}`,
    method: 'POST',
  })
}
