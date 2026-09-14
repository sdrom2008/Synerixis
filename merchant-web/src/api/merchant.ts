import { request } from './http'

export type SlaUrgency = 'ok' | 'soon' | 'overdue'

export interface SessionItem {
  id: string
  sessionId: string
  customerName?: string
  platform?: string
  platformShopOpenId?: string
  shopNickname?: string
  connectionId?: string
  status?: string
  priority?: string
  pendingHumanHandoff?: boolean
  handoffAt?: string | null
  assignedAgent?: { id: string; name: string } | null
  createdAt?: string
  lastActiveAt?: string
  lastBuyerMessageAt?: string | null
  messageCount?: number
  hasPendingDraft?: boolean
  unreadBuyerCount?: number
  hoursSinceLastBuyerMsg?: number
  needsResponseBy?: string
  responseSlaHours?: number
  slaUrgency?: SlaUrgency
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
  pendingHumanHandoff?: boolean
  handoffAt?: string | null
  assignedAgent?: { id: string; name: string; role?: string } | null
  assignedAt?: string | null
  lastBuyerMessageAt?: string | null
  hoursSinceLastBuyerMsg?: number
  needsResponseBy?: string
  responseSlaHours?: number
  slaUrgency?: SlaUrgency
  pendingDraft?: DraftInfo | null
  items: MessageItem[]
}

export interface SessionsListResult {
  items: SessionItem[]
  total: number
  pendingDraftCount: number
  responseSlaHours?: number
}

export interface MerchantAlertItem {
  sessionId?: string
  id?: string
  customerName?: string
  hoursSinceLastBuyerMsg?: number
  slaUrgency?: SlaUrgency
  needsResponseBy?: string
  [key: string]: unknown
}

export interface MerchantAlertsResult {
  items?: MerchantAlertItem[]
  total?: number
  responseSlaHours?: number
  thresholds?: number[] | string
  /** 浏览器 Notification 可用说明；无 APNs/FCM */
  browserNotifySupported?: boolean
  pushEnabled?: boolean
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

export interface SessionQuery {
  status?: string
  platform?: string
  connectionId?: string
  platformShopId?: string
  /** unassigned | mine */
  assignment?: string
}

export function getSessions(params: SessionQuery | string = {}) {
  const q =
    typeof params === 'string'
      ? params
        ? { status: params }
        : {}
      : params
  const sp = new URLSearchParams()
  if (q.status) sp.set('status', q.status)
  if (q.platform) sp.set('platform', q.platform)
  if (q.connectionId) sp.set('connectionId', q.connectionId)
  if (q.platformShopId) sp.set('platformShopId', q.platformShopId)
  if (q.assignment) sp.set('assignment', q.assignment)
  const qs = sp.toString()
  return request<SessionsListResult>({
    url: `/api/merchant/sessions${qs ? `?${qs}` : ''}`,
  })
}

export interface ShopOption {
  connectionId: string
  platform?: string
  shopId?: string
  nickname?: string
}

export function getShopOptions() {
  return request<{ items: ShopOption[] }>({ url: '/api/merchant/shop-options' })
}

export interface SessionOrderItem {
  id?: string
  orderNo?: string
  status?: string
  totalAmount?: number
  paymentAmount?: number
  platform?: string
  orderTime?: string
  paidAt?: string
  shippedAt?: string
  logisticsNo?: string
  logisticsCompany?: string
  /** local | platform */
  source?: string
  summary?: string
}

export function getSessionOrders(sessionId: string) {
  return request<{
    items: SessionOrderItem[]
    total?: number
    empty?: boolean
    source?: string
    warning?: string | null
  }>({
    url: `/api/merchant/sessions/${sessionId}/orders`,
  })
}

export function getSessionMessages(id: string) {
  return request<SessionMessagesResult>({
    url: `/api/merchant/sessions/${id}/messages`,
  })
}

export function transferSession(id: string) {
  return request<{ message?: string; pendingHumanHandoff?: boolean; supersededDrafts?: number }>({
    url: `/api/merchant/sessions/${id}/transfer`,
    method: 'POST',
  })
}

export interface ShopAgentItem {
  id: string
  name: string
  role?: string
  online?: boolean
}

export function getShopAgents() {
  return request<{ items: ShopAgentItem[]; total?: number }>({
    url: '/api/merchant/agents',
  })
}

export function assignSession(sessionId: string, agentId: string) {
  return request<{
    message?: string
    assignedAgent?: { id: string; name: string; role?: string }
    assignedAt?: string
    status?: string
    pendingHumanHandoff?: boolean
  }>({
    url: `/api/merchant/sessions/${sessionId}/assign`,
    method: 'POST',
    data: { agentId, AgentId: agentId },
  })
}

export function claimSession(sessionId: string) {
  return request<{
    message?: string
    assignedAgent?: { id: string; name: string; role?: string }
    assignedAt?: string
    status?: string
    pendingHumanHandoff?: boolean
  }>({
    url: `/api/merchant/sessions/${sessionId}/claim`,
    method: 'POST',
  })
}

/** SLA / timeout wake alerts（应用内 + 浏览器 Notification；无 Push） */
export function getMerchantAlerts(params: { thresholds?: string } = {}) {
  const q = params.thresholds ? `?thresholds=${encodeURIComponent(params.thresholds)}` : ''
  return request<MerchantAlertsResult>({
    url: `/api/merchant/alerts${q}`,
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

export interface UsageDailyPoint {
  date: string
  count: number
  sessions?: number
  messages?: number
  aiCalls?: number
  tokens?: number
}

export interface UsageDailyResult {
  days: number
  items: UsageDailyPoint[]
  hasData?: boolean
  rangeStart?: string
  rangeEnd?: string
}

export function getMerchantUsageDaily(days = 7) {
  return request<UsageDailyResult>({
    url: `/api/merchant/usage/daily?days=${days}`,
  })
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

export function refreshConnection(connectionId: string) {
  return request<{
    message?: string
    tokenExpiresAt?: string
    errorCode?: string
    rebindRequired?: boolean
    lastRefreshError?: string
  }>({
    url: `/api/merchant/connections/${encodeURIComponent(connectionId)}/refresh`,
    method: 'POST',
  })
}

export interface QuickReplyItem {
  id: string
  title: string
  content: string
  category?: string
  categoryValue?: number
  keywords?: string | null
  scope?: string
  shopId?: string | null
  isActive?: boolean
  sortOrder?: number
  createdAt?: string
  updatedAt?: string | null
}

export function listQuickReplies() {
  return request<{ items: QuickReplyItem[]; total: number }>({
    url: '/api/merchant/quick-replies',
  })
}

export function createQuickReply(data: {
  title: string
  content: string
  category?: string
  keywords?: string
  sortOrder?: number
  isActive?: boolean
}) {
  return request<{ id: string; message?: string }>({
    url: '/api/merchant/quick-replies',
    method: 'POST',
    data,
  })
}

export function updateQuickReply(
  id: string,
  data: {
    title: string
    content: string
    category?: string
    keywords?: string
    sortOrder?: number
    isActive?: boolean
  },
) {
  return request<{ id: string; message?: string }>({
    url: `/api/merchant/quick-replies/${encodeURIComponent(id)}`,
    method: 'PUT',
    data,
  })
}

export function deleteQuickReply(id: string) {
  return request<{ message?: string }>({
    url: `/api/merchant/quick-replies/${encodeURIComponent(id)}`,
    method: 'DELETE',
  })
}

export function getAuditLogs(take = 50, action?: string) {
  const params = new URLSearchParams({ take: String(take) })
  if (action) params.set('action', action)
  return request<{ items: Record<string, unknown>[]; total: number; take: number }>({
    url: `/api/merchant/audit-logs?${params}`,
  })
}
