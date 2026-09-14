import axios, { type AxiosInstance, type AxiosRequestConfig } from 'axios'

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
  if (base) config.baseURL = base
  const token = localStorage.getItem('sx_admin_token')
  if (token) {
    config.headers = config.headers ?? {}
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

http.interceptors.response.use(
  (res) => res,
  (err) => {
    if (err?.response?.status === 401) {
      localStorage.removeItem('sx_admin_token')
      localStorage.removeItem('sx_admin_name')
      if (!location.pathname.includes('/login')) {
        location.href = `/login?redirect=${encodeURIComponent(location.pathname + location.search)}`
      }
    }
    return Promise.reject(err)
  },
)

export async function request<T = unknown>(config: AxiosRequestConfig): Promise<T> {
  const res = await http.request<T>(config)
  return res.data
}

export default http
