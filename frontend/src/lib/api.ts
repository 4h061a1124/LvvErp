import axios from 'axios'
import { storage } from './storage'

const TOKEN_KEY = 'lvverp.token'

export const tokenStore = {
  get: () => storage.get(TOKEN_KEY),
  set: (token: string) => storage.set(TOKEN_KEY, token),
  clear: () => storage.remove(TOKEN_KEY),
}

export const api = axios.create({ baseURL: '/api' })

api.interceptors.request.use((config) => {
  const token = tokenStore.get()
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

let handleUnauthorized: (() => void) | undefined

/** Called when any request (other than login) comes back 401: token expired or revoked. */
export function onUnauthorized(handler: () => void) {
  handleUnauthorized = handler
}

api.interceptors.response.use(
  (response) => response,
  (error) => {
    if (axios.isAxiosError(error) && error.response?.status === 401 && !error.config?.url?.endsWith('/auth/login')) {
      handleUnauthorized?.()
    }
    return Promise.reject(error)
  },
)

/** Triggers a browser download for a binary API response. */
export async function downloadFile(url: string, fallbackName: string) {
  const response = await api.get<Blob>(url, { responseType: 'blob' })
  const disposition = String(response.headers['content-disposition'] ?? '')
  const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition)
  const name = match ? decodeURIComponent(match[1]) : fallbackName

  const href = URL.createObjectURL(response.data)
  const link = document.createElement('a')
  link.href = href
  link.download = name
  link.click()
  URL.revokeObjectURL(href)
}
