import { defineStore } from 'pinia'
import { ref, computed } from 'vue'

const TOKEN_KEY = 'sx_merchant_token'
const PROFILE_KEY = 'sx_merchant_profile'

export interface MerchantProfile {
  userId?: string
  nickname?: string
  userType?: string
  subscriptionLevel?: string
}

export const useAuthStore = defineStore('auth', () => {
  const token = ref<string | null>(localStorage.getItem(TOKEN_KEY))
  const profile = ref<MerchantProfile | null>(loadProfile())

  const isAuthenticated = computed(() => !!token.value)

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
      profile.value = p
      localStorage.setItem(PROFILE_KEY, JSON.stringify(p))
    }
  }

  function clear() {
    token.value = null
    profile.value = null
    localStorage.removeItem(TOKEN_KEY)
    localStorage.removeItem(PROFILE_KEY)
  }

  return { token, profile, isAuthenticated, setSession, clear }
})
