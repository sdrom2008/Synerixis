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

/**
 * TODO: Backend dashboard KPI endpoint for merchant console not yet dedicated.
 * Prefer /api/reports/dashboard when merchant-scoped; until then return nulls.
 */
export async function getDashboardKpis() {
  // Do not invent numbers. Attempt sessions summary only.
  try {
    const data = await getSessions();
    const items = data?.items || [];
    const pending = items.filter((s) => s.status === 'Pending').length;
    return {
      todaySessions: null, // TODO: need date-filtered API
      autoResolveRate: null, // TODO: need metrics API
      pendingHandoff: pending || (items.length ? pending : null),
      shopStatus: null, // filled by connections caller
      sessionsTotal: typeof data?.total === 'number' ? data.total : items.length,
      rawSessions: items
    };
  } catch (e) {
    return {
      todaySessions: null,
      autoResolveRate: null,
      pendingHandoff: null,
      shopStatus: null,
      sessionsTotal: null,
      rawSessions: []
    };
  }
}
