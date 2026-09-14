import { request } from './http'

export interface PhoneLoginPayload {
  phone: string
  code: string
  countryCode?: string
}

export interface LoginResult {
  token: string
  userId?: string
  userType?: string
  nickname?: string
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
