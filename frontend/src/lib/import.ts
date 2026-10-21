import type { TFunction } from 'i18next'
import type { Messages } from '@/locales/vi'

export interface ImportError {
  row: number
  column: string
  errorCode: string
  message: string
  value: string | null
}

export interface ImportResult {
  succeeded: boolean
  totalRows: number
  created: number
  updated: number
  errors: ImportError[]
}

type ImportErrorCode = keyof Messages['import']['errors']

/** Translated message for an import error; falls back to the server's English text for unknown codes. */
export function importErrorMessage(error: ImportError, t: TFunction): string {
  const known = t(`import.errors.${error.errorCode as ImportErrorCode}`, { value: error.value ?? '', defaultValue: '' })
  return known || error.message
}
