import { request } from './http'

export interface PhoneLoginPayload {
  phone: string
  code: string
  countryCode?: string
}

export interface AgentLoginPayload {
  email: string
  password: string
}

export interface LoginResult {
  token: string
  userId?: string
  agentId?: string
  userType?: string
  nickname?: string
  name?: string
  role?: string
  shopId?: string
  freeQuota?: number
  subscriptionLevel?: string
  isNewRegistration?: boolean
}

/** POST /api/auth/phone-login — 开发环境验证码可用 123456 */
export function phoneLogin(payload: PhoneLoginPayload) {
  return request<LoginResult>({
    url: '/api/auth/phone-login',
    method: 'POST',
    data: {
      Phone: payload.phone,
      Code: payload.code,
      CountryCode: payload.countryCode || '86',
    },
  })
}

/** POST /api/auth/agent-login — 坐席邮箱+密码 */
export function agentLogin(payload: AgentLoginPayload) {
  return request<LoginResult>({
    url: '/api/auth/agent-login',
    method: 'POST',
    data: {
      Email: payload.email,
      Password: payload.password,
    },
  })
}
