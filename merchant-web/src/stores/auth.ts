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
}

/** 全菜单角色 */
const FULL_MENU_ROLES = new Set(['Seller', 'Supervisor', 'Admin'])

/** 普通坐席可访问 */
const AGENT_ROUTES = new Set(['inbox', 'overview'])

export const useAuthStore = defineStore('auth', () => {
  const token = ref<string | null>(localStorage.getItem(TOKEN_KEY))
  const profile = ref<MerchantProfile | null>(loadProfile())

  const isAuthenticated = computed(() => !!token.value)

  const userType = computed(() => (profile.value?.userType || 'Seller') as string)

  const displayName = computed(
    () => profile.value?.name || profile.value?.nickname || profile.value?.userId || '未命名',
  )

  const identityLabel = computed(() => {
    const t = userType.value
    if (t === 'Seller') return '商家'
    if (t === 'Supervisor') return '坐席 · Supervisor'
    if (t === 'Admin') return '坐席 · Admin'
    if (t === 'Agent') return '坐席 · Agent'
    return t
  })

  const permissions = computed(() => {
    const t = userType.value
    const full = FULL_MENU_ROLES.has(t)
    return {
      canManageTeam: full,
      canManageShops: full,
      canManageAi: full,
      canViewBilling: full,
      canViewOverview: true,
      canViewInbox: true,
      fullMenu: full,
      isAgentOnly: t === 'Agent',
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

  function setSession(newToken: string, p?: MerchantProfile) {
    token.value = newToken
    localStorage.setItem(TOKEN_KEY, newToken)
    if (p) {
      const normalized: MerchantProfile = {
        ...p,
        name: p.name || p.nickname,
        nickname: p.nickname || p.name,
        role: p.role || p.userType,
        userType: p.userType || p.role || 'Seller',
      }
      // 粘贴 Token 时尝试从 JWT payload 解析
      if (!normalized.userType || normalized.userType === 'Seller') {
        const fromJwt = decodeJwtClaims(newToken)
        if (fromJwt) {
          normalized.userType = (fromJwt.userType || fromJwt.role || normalized.userType) as string
          normalized.role = (fromJwt.role || fromJwt.userType || normalized.role) as string
          normalized.shopId = (fromJwt.shopId as string) || normalized.shopId
          normalized.userId = (fromJwt.userId || fromJwt.sub || fromJwt.uid || normalized.userId) as string
        }
      }
      profile.value = normalized
      localStorage.setItem(PROFILE_KEY, JSON.stringify(normalized))
    }
  }

  function canAccessRoute(routeName: string | symbol | null | undefined): boolean {
    if (!routeName || typeof routeName !== 'string') return true
    if (permissions.value.fullMenu) return true
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
