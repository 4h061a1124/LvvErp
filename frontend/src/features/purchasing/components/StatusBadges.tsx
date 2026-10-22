import clsx from 'clsx'
import { useTranslation } from 'react-i18next'
import type { OrderStatus, RequestStatus } from '../types'

const base = 'inline-flex items-center gap-1.5 text-[13px] whitespace-nowrap before:size-1.5 before:rounded-full'

const tones = {
  draft: 'text-zinc-600 before:bg-zinc-400',
  waiting: 'text-amber-700 before:bg-amber-500',
  approved: 'text-brand-700 before:bg-brand-600',
  partial: 'text-sky-700 before:bg-sky-500',
  done: 'text-emerald-700 before:bg-emerald-600',
  closed: 'text-zinc-500 before:bg-zinc-300',
  bad: 'text-red-700 before:bg-red-600',
}

const requestTones: Record<RequestStatus, string> = {
  Draft: tones.draft,
  Submitted: tones.waiting,
  Approved: tones.approved,
  Rejected: tones.bad,
  PartiallyOrdered: tones.partial,
  Ordered: tones.done,
  Closed: tones.closed,
  Cancelled: tones.bad,
}

const orderTones: Record<OrderStatus, string> = {
  Draft: tones.draft,
  Submitted: tones.waiting,
  Approved: tones.approved,
  Rejected: tones.bad,
  PartiallyReceived: tones.partial,
  Received: tones.done,
  Closed: tones.closed,
  Cancelled: tones.bad,
}

export function RequestStatusBadge({ status }: { status: RequestStatus }) {
  const { t } = useTranslation()
  return <span className={clsx(base, requestTones[status])}>{t(`purchasing.requests.status.${status}`)}</span>
}

export function OrderStatusBadge({ status }: { status: OrderStatus }) {
  const { t } = useTranslation()
  return <span className={clsx(base, orderTones[status])}>{t(`purchasing.orders.status.${status}`)}</span>
}
