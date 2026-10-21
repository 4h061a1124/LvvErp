import axios from 'axios'
import type { TFunction } from 'i18next'
import type { Messages } from '@/locales/vi'

export interface ProblemDetails {
  status?: number
  title?: string
  detail?: string
  errors?: Record<string, string[]>
  /** Stable code sent with 409 responses (backend ErrorCodes). */
  errorCode?: string
  details?: unknown
}

type ErrorCode = keyof Messages['errors']['codes']

export function getStatus(error: unknown): number | undefined {
  return axios.isAxiosError(error) ? error.response?.status : undefined
}

export function getProblem(error: unknown): ProblemDetails | undefined {
  if (!axios.isAxiosError(error)) return undefined
  const data = error.response?.data
  return data && typeof data === 'object' ? (data as ProblemDetails) : undefined
}

export function getErrorCode(error: unknown): string | undefined {
  return getProblem(error)?.errorCode
}

/** Server validation errors keyed by camelCase field name ("FullName" -> "fullName"). */
export function getFieldErrors(error: unknown): Record<string, string> {
  const errors = getProblem(error)?.errors ?? {}
  const result: Record<string, string> = {}
  for (const [key, messages] of Object.entries(errors)) {
    if (!messages.length) continue
    result[key.charAt(0).toLowerCase() + key.slice(1)] = messages[0]
  }
  return result
}

/** A short message for a toast or banner, translated where the status or error code says enough. */
export function getErrorMessage(error: unknown, t: TFunction): string {
  if (axios.isAxiosError(error) && !error.response) return t('errors.network')

  const problem = getProblem(error)
  if (problem?.errorCode) {
    const translated = t(`errors.codes.${problem.errorCode as ErrorCode}`, { defaultValue: '' })
    if (translated) return translated
  }

  switch (getStatus(error)) {
    case 403:
      return t('errors.forbidden')
    case 404:
      return t('errors.notFound')
    case 400:
    case 409: {
      const firstFieldError = Object.values(problem?.errors ?? {})[0]?.[0]
      return problem?.detail ?? firstFieldError ?? t('errors.server')
    }
    default:
      return t('errors.server')
  }
}
