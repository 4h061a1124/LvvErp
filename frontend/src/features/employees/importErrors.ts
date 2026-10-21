import type { TFunction } from 'i18next'
import type { Messages } from '@/locales/vi'
import type { ImportError } from './types'

type ImportErrorCode = keyof Messages['import']['errors']
type ColumnKey = keyof Messages['employees']['columns']

const columnKeys: Record<string, ColumnKey> = {
  Code: 'code',
  FullName: 'fullName',
  Department: 'department',
  Position: 'position',
  Email: 'email',
  Phone: 'phone',
  Status: 'status',
}

export function importColumnLabel(column: string, t: TFunction): string {
  const key = columnKeys[column]
  return key ? t(`employees.columns.${key}`) : column
}

/** Translated message for an import error; falls back to the server's English text for unknown codes. */
export function importErrorMessage(error: ImportError, t: TFunction): string {
  const known = t(`import.errors.${error.errorCode as ImportErrorCode}`, {
    value: error.value ?? '',
    defaultValue: '',
  })
  return known || error.message
}
