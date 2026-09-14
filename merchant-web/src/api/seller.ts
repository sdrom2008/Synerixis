import { request } from './http'

export function getSellerProfile() {
  return request<Record<string, unknown>>({ url: '/api/seller/profile' })
}

export function updateSellerConfig(data: Record<string, unknown>) {
  return request({ url: '/api/seller/config', method: 'PUT', data })
}

export interface TeamMember {
  id: string
  name: string
  email: string
  role: string
  isActive: boolean
  isOnline: boolean
  maxConcurrentSessions?: number
  currentSessionCount?: number
  lastLoginAt?: string | null
  createdAt?: string
}

export interface TeamListResult {
  items: TeamMember[]
  total: number
  shopId?: string
}

export function getTeam() {
  return request<TeamListResult | TeamMember[]>({ url: '/api/seller/team' })
}

export function addTeamMember(data: {
  email: string
  name: string
  password: string
  role?: string
}) {
  return request({
    url: '/api/seller/team',
    method: 'POST',
    data: {
      Email: data.email,
      Name: data.name,
      Password: data.password,
      RoleName: data.role || 'Agent',
    },
  })
}

export function updateTeamMember(
  agentId: string,
  data: { name?: string; role?: string; isActive?: boolean; maxConcurrentSessions?: number },
) {
  return request({
    url: `/api/seller/team/${agentId}`,
    method: 'PATCH',
    data: {
      Name: data.name,
      RoleName: data.role,
      IsActive: data.isActive,
      MaxConcurrentSessions: data.maxConcurrentSessions,
    },
  })
}

export function resetTeamMemberPassword(agentId: string, newPassword: string) {
  return request({
    url: `/api/seller/team/${agentId}/reset-password`,
    method: 'POST',
    data: { NewPassword: newPassword },
  })
}

export function removeTeamMember(agentId: string) {
  return request({
    url: `/api/seller/team/${agentId}`,
    method: 'DELETE',
  })
}
