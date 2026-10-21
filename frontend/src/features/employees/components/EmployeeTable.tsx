import clsx from 'clsx'
import { ArrowDown, ArrowUp } from 'lucide-react'
import { useTranslation } from 'react-i18next'
import type { Messages } from '@/locales/vi'
import type { Employee } from '../types'
import { EmployeeAvatar } from './EmployeeAvatar'
import { StatusBadge } from './StatusBadge'

type ColumnKey = keyof Messages['employees']['columns']

interface EmployeeTableProps {
  employees: Employee[]
  sort: string
  onSort: (sort: string) => void
  canManage: boolean
  onEdit: (employee: Employee) => void
  onDelete: (employee: Employee) => void
}

export function EmployeeTable({ employees, sort, onSort, canManage, onEdit, onDelete }: EmployeeTableProps) {
  const { t } = useTranslation()

  const header = (column: ColumnKey, sortKey?: string, className?: string) => {
    if (!sortKey) {
      return <th className={clsx('px-3 py-2 font-medium', className)}>{t(`employees.columns.${column}`)}</th>
    }
    const active = sort === sortKey || sort === `-${sortKey}`
    const descending = sort === `-${sortKey}`
    return (
      <th className={clsx('px-3 py-2 font-medium', className)} aria-sort={active ? (descending ? 'descending' : 'ascending') : undefined}>
        <button
          type="button"
          onClick={() => onSort(active && !descending ? `-${sortKey}` : sortKey)}
          className={clsx('inline-flex items-center gap-1 hover:text-zinc-900', active && 'text-zinc-900')}
        >
          {t(`employees.columns.${column}`)}
          {active && (descending ? <ArrowDown size={13} /> : <ArrowUp size={13} />)}
        </button>
      </th>
    )
  }

  return (
    <table className="w-full border-collapse text-left">
      <thead>
        <tr className="border-b border-zinc-200 bg-zinc-50 text-[13px] text-zinc-600">
          <th className="w-12 py-2 pl-3">
            <span className="sr-only">{t('employees.columns.photo')}</span>
          </th>
          {header('code', 'code', 'w-28')}
          {header('fullName', 'fullName')}
          {header('department', 'department')}
          {header('position', 'position')}
          {header('email')}
          {header('status', 'status', 'w-32')}
          {canManage && <th className="w-28 px-3 py-2"><span className="sr-only">{t('common.actions')}</span></th>}
        </tr>
      </thead>
      <tbody>
        {employees.map((e) => (
          <tr key={e.id} className="border-b border-zinc-100 last:border-b-0 hover:bg-zinc-50/70">
            <td className="py-1.5 pl-3">
              <EmployeeAvatar name={e.fullName} photoUrl={e.photoUrl} size={32} />
            </td>
            <td className="tabular px-3 py-1.5 text-[13px] text-zinc-600">{e.code}</td>
            <td className="px-3 py-1.5 font-medium text-zinc-900">{e.fullName}</td>
            <td className="px-3 py-1.5 text-zinc-700">{e.departmentName}</td>
            <td className="px-3 py-1.5 text-zinc-700">{e.positionName}</td>
            <td className="px-3 py-1.5 text-[13px]">
              {e.email && (
                <a href={`mailto:${e.email}`} className="block text-zinc-700 hover:text-brand-700 hover:underline">
                  {e.email}
                </a>
              )}
              {e.phone && <div className={e.email ? 'tabular text-xs text-zinc-500' : 'tabular text-zinc-700'}>{e.phone}</div>}
              {!e.email && !e.phone && <span className="text-zinc-400">—</span>}
            </td>
            <td className="px-3 py-1.5">
              <StatusBadge status={e.status} />
            </td>
            {canManage && (
              <td className="px-3 py-1.5 text-right text-[13px] whitespace-nowrap">
                <button type="button" onClick={() => onEdit(e)} className="px-1 text-brand-700 hover:underline">
                  {t('common.edit')}
                </button>
                <span className="mx-1 text-zinc-300">|</span>
                <button type="button" onClick={() => onDelete(e)} className="px-1 text-red-700 hover:underline">
                  {t('common.delete')}
                </button>
              </td>
            )}
          </tr>
        ))}
      </tbody>
    </table>
  )
}
