import { request } from './http'

export function getSellerProfile() {
  return request<Record<string, unknown>>({ url: '/api/seller/profile' })
}

export function updateSellerConfig(data: Record<string, unknown>) {
  return request({ url: '/api/seller/config', method: 'PUT', data })
}
