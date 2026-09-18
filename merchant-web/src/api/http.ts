import axios, { type AxiosInstance, type AxiosRequestConfig } from 'axios'
import { ElMessage } from 'element-plus'
import { useAuthStore } from '@/stores/auth'
import router from '@/router'

/** API base: env override, else same-origin /api (Vite proxy in dev). */
export function getApiBaseUrl(): string {
  const fromEnv = (import.meta.env.VITE_API_BASE_URL || '').trim()
  if (fromEnv) return fromEnv.replace(/\/$/, '')
  return ''
}

const http: AxiosInstance = axios.create({
  timeout: 30000,
  headers: { 'Content-Type': 'application/json' },
})

http.interceptors.request.use((config) => {
  const base = getApiBaseUrl()
  if (base) {
    config.baseURL = base
  }
  const auth = useAuthStore()
  if (auth.token) {
    config.headers = config.headers ?? {}
    config.headers.Authorization = `Bearer ${auth.token}`
  }
  return config
})

http.interceptors.response.use(
  (res) => res,
  (err) => {
    const status = err?.response?.status
    const code = err?.response?.data?.code
    if (status === 503 && (code === 'MAINTENANCE' || code === 'REGISTRATION_CLOSED')) {
      ElMessage.warning(err?.response?.data?.message || '系统维护中')
    }
    if (status === 401) {
      const auth = useAuthStore()
      auth.clear()
      if (router.currentRoute.value.name !== 'login') {
        router.push({ name: 'login', query: { redirect: router.currentRoute.value.fullPath } })
      }
    }
    return Promise.reject(err)
  },
)

/** 解开 System.Text.Json ReferenceHandler.Preserve 的 $values / $id 包装 */
function unwrapJsonPreserve<T>(data: T): T {
  if (data == null || typeof data !== 'object') return data
  if (Array.isArray(data)) return data.map((x) => unwrapJsonPreserve(x)) as T
  const obj = data as Record<string, unknown>
  if (Array.isArray(obj.$values)) {
    return unwrapJsonPreserve(obj.$values) as T
  }
  const out: Record<string, unknown> = {}
  for (const [k, v] of Object.entries(obj)) {
    if (k === '$id' || k === '$ref') continue
    out[k] = unwrapJsonPreserve(v)
  }
  return out as T
}

export async function request<T = unknown>(config: AxiosRequestConfig): Promise<T> {
  const res = await http.request<T>(config)
  return unwrapJsonPreserve(res.data)
}

export default http
