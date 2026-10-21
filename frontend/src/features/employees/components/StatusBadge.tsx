import clsx from 'clsx'
import { useTranslation } from 'react-i18next'
import type { EmployeeStatus } from '../types'

const styles: Record<EmployeeStatus, string> = {
  Active: 'text-emerald-700 before:bg-emerald-600',
  OnLeave: 'text-amber-700 before:bg-amber-500',
  Resigned: 'text-zinc-500 before:bg-zinc-400',
}

export function StatusBadge({ status }: { status: EmployeeStatus }) {
  const { t } = useTranslation()
  return (
    <span
      className={clsx(
        'inline-flex items-center gap-1.5 text-[13px] whitespace-nowrap before:size-1.5 before:rounded-full',
        styles[status],
      )}
    >
      {t(`employees.status.${status}`)}
    </span>
  )
}
