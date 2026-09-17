import { defineStore } from 'pinia'
import { ref, computed } from 'vue'

const TOKEN_KEY = 'sx_merchant_token'
const PROFILE_KEY = 'sx_merchant_profile'

export type UserType = 'Seller' | 'Agent' | 'Supervisor' | 'Admin'

export interface MerchantProfile {
  userId?: string
  name?: string
  nickname?: string
  userType?: UserType | string
  role?: string
  shopId?: string
  subscriptionLevel?: string
  /** Platform Admin enter-merchant support/impersonation */
  support?: boolean
}

/** 全菜单角色（support token 除外） */
const FULL_MENU_ROLES = new Set(['Seller', 'Supervisor', 'Admin'])

/** 普通坐席可访问；support 会话同此范围（全店 inbox，无团队/计费写） */
const AGENT_ROUTES = new Set(['inbox', 'overview', 'onboarding'])

export const useAuthStore = defineStore('auth', () => {
  const token = ref<string | null>(localStorage.getItem(TOKEN_KEY))
  const profile = ref<MerchantProfile | null>(loadProfile())

  const isAuthenticated = computed(() => !!token.value)

  const userType = computed(() => (profile.value?.userType || 'Seller') as string)

  const isSupport = computed(() => !!profile.value?.support)

  const displayName = computed(
    () => profile.value?.name || profile.value?.nickname || profile.value?.userId || '未命名',
  )

  const identityLabel = computed(() => {
    if (isSupport.value) return '平台支持'
    const t = userType.value
    if (t === 'Seller') return '商家'
    if (t === 'Supervisor') return '坐席 · Supervisor'
    if (t === 'Admin') return '坐席 · Admin'
    if (t === 'Agent') return '坐席 · Agent'
    return t
  })

  const permissions = computed(() => {
    const t = userType.value
    const support = isSupport.value
    const full = FULL_MENU_ROLES.has(t) && !support
    return {
      canManageTeam: full,
      canManageShops: full,
      canManageAi: full,
      // C: Seller + Supervisor 可看计费/充值；Agent / support 不可
      canViewBilling: (t === 'Seller' || t === 'Supervisor') && !support,
      canViewOverview: true,
      canViewInbox: true,
      fullMenu: full,
      isAgentOnly: t === 'Agent' && !support,
      isSupport: support,
    }
  })

  function loadProfile(): MerchantProfile | null {
    try {
      const raw = localStorage.getItem(PROFILE_KEY)
      return raw ? (JSON.parse(raw) as MerchantProfile) : null
    } catch {
      return null
    }
  }

  function claimTruthy(v: unknown): boolean {
    return v === true || v === 'true' || v === '1'
  }

  function setSession(newToken: string, p?: MerchantProfile) {
    token.value = newToken
    localStorage.setItem(TOKEN_KEY, newToken)
    const fromJwt = decodeJwtClaims(newToken)
    const normalized: MerchantProfile = {
      ...(p || {}),
      name: p?.name || p?.nickname,
      nickname: p?.nickname || p?.name,
      role: p?.role || p?.userType,
      userType: p?.userType || p?.role || 'Seller',
      support: !!p?.support,
    }
    if (fromJwt) {
      normalized.userType = (fromJwt.userType || fromJwt.role || normalized.userType) as string
      normalized.role = (fromJwt.role || fromJwt.userType || normalized.role) as string
      normalized.shopId = (fromJwt.shopId as string) || normalized.shopId
      normalized.userId = (fromJwt.userId || fromJwt.sub || fromJwt.uid || normalized.userId) as string
      normalized.support =
        claimTruthy(fromJwt.support) || claimTruthy(fromJwt.impersonation) || !!normalized.support
      if (!normalized.name && !normalized.nickname && normalized.support) {
        normalized.name = '平台支持'
        normalized.nickname = '平台支持'
      }
    }
    profile.value = normalized
    localStorage.setItem(PROFILE_KEY, JSON.stringify(normalized))
  }

  function canAccessRoute(routeName: string | symbol | null | undefined): boolean {
    if (!routeName || typeof routeName !== 'string') return true
    if (isSupport.value) return AGENT_ROUTES.has(routeName)
    if (permissions.value.fullMenu) return true
    if (routeName === 'billing') return permissions.value.canViewBilling
    return AGENT_ROUTES.has(routeName)
  }

  function clear() {
    token.value = null
    profile.value = null
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(PROFILE_KEY)
  }

  return {
    token,
    profile,
    isAuthenticated,
    userType,
    isSupport,
    displayName,
    identityLabel,
    permissions,
    setSession,
    canAccessRoute,
    clear,
  }
})

function decodeJwtClaims(token: string): Record<string, unknown> | null {
  try {
    const part = token.split('.')[1]
    if (!part) return null
    const json = atob(part.replace(/-/g, '+').replace(/_/g, '/'))
    return JSON.parse(json) as Record<string, unknown>
  } catch {
    return null
  }
}
