/**
 * Merchant console API helpers.
 * Wraps existing backend REST; callers should treat missing KPI fields as "—".
 */
import { request } from '@/utils/request.js';

export function getSellerProfile() {
  return request({ url: '/api/seller/profile' });
}

export function updateSellerConfig(data) {
  return request({ url: '/api/seller/config', method: 'PUT', data });
}

export function getPlatforms() {
  return request({ url: '/api/merchant/platforms' });
}

export function getConnections() {
  return request({ url: '/api/merchant/connections' });
}

export function getBindUrl(platform) {
  return request({ url: `/api/merchant/bind/${encodeURIComponent(platform)}` });
}

export function unbindPlatform(platform) {
  return request({ url: `/api/merchant/unbind/${encodeURIComponent(platform)}`, method: 'POST' });
}

export function getSessions(params = {}) {
  const q = params.status ? `?status=${encodeURIComponent(params.status)}` : '';
  return request({ url: `/api/merchant/sessions${q}` });
}

export function getSessionMessages(id) {
  return request({ url: `/api/merchant/sessions/${id}/messages` });
}

export function transferSession(id) {
  return request({ url: `/api/merchant/sessions/${id}/transfer`, method: 'POST' });
}

/** Real DB aggregates from GET /api/merchant/dashboard */
export function getMerchantDashboard() {
  return request({ url: '/api/merchant/dashboard' });
}

/** Monthly usage from GET /api/merchant/usage */
export function getMerchantUsage() {
  return request({ url: '/api/merchant/usage' });
}

/**
 * Dashboard KPIs — honest nulls when API field is null/absent.
 */
export async function getDashboardKpis() {
  try {
    const data = await getMerchantDashboard();
    return {
      todaySessions: data?.sessionsToday ?? data?.SessionsToday ?? null,
      autoResolveRate: data?.autoResolveRate ?? data?.AutoResolveRate ?? null,
      pendingHandoff: data?.pendingHandoff ?? data?.PendingHandoff ?? null,
      connectedShops: data?.connectedShops ?? data?.ConnectedShops ?? null,
      messagesThisMonth: data?.messagesThisMonth ?? data?.MessagesThisMonth ?? null,
      sessionsTotal: null,
      rawSessions: []
    };
  } catch (e) {
    return {
      todaySessions: null,
      autoResolveRate: null,
      pendingHandoff: null,
      connectedShops: null,
      messagesThisMonth: null,
      sessionsTotal: null,
      rawSessions: []
    };
  }
}
