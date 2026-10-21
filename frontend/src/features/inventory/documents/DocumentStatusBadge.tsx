import clsx from 'clsx'
import { useTranslation } from 'react-i18next'
import type { DocumentStatus } from './types'

const styles: Record<DocumentStatus, string> = {
  Draft: 'text-zinc-600 before:bg-zinc-400',
  Approved: 'text-emerald-700 before:bg-emerald-600',
  Cancelled: 'text-red-700 before:bg-red-600',
}

export function DocumentStatusBadge({ status }: { status: DocumentStatus }) {
  const { t } = useTranslation()
  return (
    <span
      className={clsx(
        'inline-flex items-center gap-1.5 text-[13px] whitespace-nowrap before:size-1.5 before:rounded-full',
        styles[status],
      )}
    >
      {t(`inventory.documents.status.${status}`)}
    </span>
  )
}
