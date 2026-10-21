import type { TFunction } from 'i18next'
import type { Messages } from '@/locales/vi'

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
