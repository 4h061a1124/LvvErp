import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { PageHeader } from '@/components/ui/PageHeader'
import { useEmployees } from '@/features/employees/api'
import { EmployeeAvatar } from '@/features/employees/components/EmployeeAvatar'
import { StatusBadge } from '@/features/employees/components/StatusBadge'
import { EMPLOYEE_STATUSES, type EmployeeStatus } from '@/features/employees/types'
import { useDocuments } from '@/features/inventory/documents/api'
import { DOCUMENT_PATHS, type DocumentType } from '@/features/inventory/documents/types'
import { useBelowMinStock } from '@/features/inventory/reports/api'
import { useApprovalInbox, useOrders } from '@/features/purchasing/api'
import { useAuth } from '@/lib/auth-context'
import { formatDate, formatMoney, formatQuantity } from '@/lib/format'
import { Permissions } from '@/lib/permissions'

function useEmployeeCount(status?: EmployeeStatus) {
  return useEmployees({ status, page: 1, pageSize: 1 }).data?.totalCount
}

function Stat({ label, value, to }: { label: string; value: number | undefined; to: string }) {
  return (
    <Link to={to} className="block bg-white px-4 py-3 hover:bg-zinc-50">
      <div className="text-[13px] text-zinc-500">{label}</div>
      <div className="tabular mt-1 text-2xl font-semibold text-zinc-900">{value ?? '–'}</div>
    </Link>
  )
}

function Panel({ title, link, children }: { title: string; link?: { to: string; label: string }; children: ReactNode }) {
  return (
    <section className="rounded-[4px] border border-zinc-200 bg-white">
      <div className="flex items-center justify-between border-b border-zinc-200 px-3 py-2">
        <h2 className="text-sm font-semibold text-zinc-800">{title}</h2>
        {link && (
          <Link to={link.to} className="text-[13px] text-brand-700 hover:underline">
            {link.label}
          </Link>
        )}
      </div>
      {children}
    </section>
  )
}

export function DashboardPage() {
  const { t } = useTranslation()
  const { user, can } = useAuth()

  const total = useEmployeeCount()
  const active = useEmployeeCount('Active')
  const onLeave = useEmployeeCount('OnLeave')
  const resigned = useEmployeeCount('Resigned')
  const counts: Record<EmployeeStatus, number | undefined> = { Active: active, OnLeave: onLeave, Resigned: resigned }

  const recent = useEmployees({ page: 1, pageSize: 6, sort: '-createdAt' })

  return (
    <>
      <PageHeader title={t('dashboard.title')} meta={t('dashboard.hello', { name: user?.fullName })} />

      <div className="mb-5 grid grid-cols-2 gap-px overflow-hidden rounded-[4px] border border-zinc-200 bg-zinc-200 lg:grid-cols-4">
        <Stat label={t('dashboard.total')} value={total} to="/employees" />
        {EMPLOYEE_STATUSES.map((s) => (
          <Stat key={s} label={t(`employees.status.${s}`)} value={counts[s]} to={`/employees?status=${s}`} />
        ))}
      </div>

      <div className="grid items-start gap-5 lg:grid-cols-2">
        <Panel title={t('dashboard.recent')} link={{ to: '/employees?sort=-createdAt', label: t('common.viewAll') }}>
          {recent.data?.items.length ? (
            <ul className="divide-y divide-zinc-100">
              {recent.data.items.map((e) => (
                <li key={e.id} className="flex items-center gap-3 px-3 py-2">
                  <EmployeeAvatar name={e.fullName} photoUrl={e.photoUrl} size={30} />
                  <div className="min-w-0 flex-1">
                    <div className="truncate text-sm font-medium text-zinc-900">{e.fullName}</div>
                    <div className="truncate text-xs text-zinc-500">
                      {e.code} · {e.positionName} · {e.departmentName}
                    </div>
                  </div>
                  <StatusBadge status={e.status} />
                </li>
              ))}
            </ul>
          ) : (
            <p className="px-3 py-6 text-center text-sm text-zinc-500">{recent.isPending ? t('common.loading') : t('employees.empty')}</p>
          )}
        </Panel>

        <div className="space-y-5">
          {(can(Permissions.PurchasingApproveRequests) || can(Permissions.PurchasingApproveOrders)) && <ApprovalPanel />}
          {can(Permissions.PurchasingView) && <OverdueOrdersPanel />}
          {can(Permissions.InventoryView) && <LowStockPanel />}
          {can(Permissions.InventoryApproveDocuments) && <PendingDraftsPanel />}
        </div>
      </div>
    </>
  )
}

const LOW_STOCK_SHOWN = 6

function LowStockPanel() {
  const { t, i18n } = useTranslation()
  const lowStock = useBelowMinStock()
  const items = lowStock.data ?? []

  return (
    <Panel
      title={lowStock.data ? `${t('dashboard.lowStock')} (${items.length})` : t('dashboard.lowStock')}
      link={items.length > 0 ? { to: '/inventory/stock?low=1', label: t('common.viewAll') } : undefined}
    >
      {lowStock.isPending ? (
        <p className="px-3 py-6 text-center text-sm text-zinc-500">{t('common.loading')}</p>
      ) : items.length === 0 ? (
        <p className="px-3 py-6 text-center text-sm text-zinc-500">{t('dashboard.lowStockNone')}</p>
      ) : (
        <ul className="divide-y divide-zinc-100">
          {items.slice(0, LOW_STOCK_SHOWN).map((m) => (
            <li key={m.materialId}>
              <Link to={`/inventory/stock-card?material=${m.materialId}`} className="flex items-center gap-3 px-3 py-2 hover:bg-zinc-50">
                <div className="min-w-0 flex-1">
                  <div className="truncate text-sm text-zinc-900">{m.name}</div>
                  <div className="tabular text-xs text-zinc-500">{m.code}</div>
                </div>
                <div className="tabular text-right text-[13px]">
                  <span className="font-medium text-red-700">{formatQuantity(m.quantity, i18n.language)}</span>
                  <span className="text-zinc-500">
                    {' '}
                    / {formatQuantity(m.minStock, i18n.language)} {m.unitName}
                  </span>
                </div>
              </Link>
            </li>
          ))}
          {items.length > LOW_STOCK_SHOWN && (
            <li className="px-3 py-2 text-[13px] text-zinc-500">{t('dashboard.lowStockMore', { count: items.length - LOW_STOCK_SHOWN })}</li>
          )}
        </ul>
      )}
    </Panel>
  )
}

function PendingDraftsPanel() {
  const { t } = useTranslation()
  const receipts = useDocuments('Receipt', { status: 'Draft', page: 1, pageSize: 1 })
  const issues = useDocuments('Issue', { status: 'Draft', page: 1, pageSize: 1 })
  const transfers = useDocuments('Transfer', { status: 'Draft', page: 1, pageSize: 1 })
  const rows: [DocumentType, number | undefined][] = [
    ['Receipt', receipts.data?.totalCount],
    ['Issue', issues.data?.totalCount],
    ['Transfer', transfers.data?.totalCount],
  ]

  return (
    <Panel title={t('dashboard.pending')}>
      <div className="grid grid-cols-3 divide-x divide-zinc-100">
        {rows.map(([type, count]) => (
          <Link key={type} to={`${DOCUMENT_PATHS[type].route}?status=Draft`} className="block px-3 py-3 hover:bg-zinc-50">
            <div className="text-[13px] text-zinc-500">{t(`inventory.documents.title.${type}`)}</div>
            <div className="tabular mt-1 text-xl font-semibold text-zinc-900">{count ?? '–'}</div>
          </Link>
        ))}
      </div>
    </Panel>
  )
}

function ApprovalPanel() {
  const { t } = useTranslation()
  const inbox = useApprovalInbox()
  const rows: [string, number | undefined][] = [
    [t('purchasing.approvals.requests'), inbox.data?.requests.length],
    [t('purchasing.approvals.orders'), inbox.data?.orders.length],
  ]
  return (
    <Panel title={t('dashboard.approvals')} link={{ to: '/purchasing/approvals', label: t('common.viewAll') }}>
      <div className="grid grid-cols-2 divide-x divide-zinc-100">
        {rows.map(([label, count]) => (
          <Link key={label} to="/purchasing/approvals" className="block px-3 py-3 hover:bg-zinc-50">
            <div className="text-[13px] text-zinc-500">{label}</div>
            <div className={count ? 'tabular mt-1 text-xl font-semibold text-amber-700' : 'tabular mt-1 text-xl font-semibold text-zinc-900'}>
              {count ?? '–'}
            </div>
          </Link>
        ))}
      </div>
    </Panel>
  )
}

const OVERDUE_SHOWN = 5

function OverdueOrdersPanel() {
  const { t, i18n } = useTranslation()
  const overdue = useOrders({ overdueOnly: true, page: 1, pageSize: OVERDUE_SHOWN, sort: 'expected' })
  const items = overdue.data?.items ?? []
  const total = overdue.data?.totalCount ?? 0

  return (
    <Panel
      title={overdue.data ? `${t('dashboard.overdueOrders')} (${total})` : t('dashboard.overdueOrders')}
      link={total > 0 ? { to: '/purchasing/orders?overdue=1', label: t('common.viewAll') } : undefined}
    >
      {overdue.isPending ? (
        <p className="px-3 py-6 text-center text-sm text-zinc-500">{t('common.loading')}</p>
      ) : items.length === 0 ? (
        <p className="px-3 py-6 text-center text-sm text-zinc-500">{t('dashboard.overdueNone')}</p>
      ) : (
        <ul className="divide-y divide-zinc-100">
          {items.map((o) => (
            <li key={o.id}>
              <Link to={`/purchasing/orders/${o.id}`} className="flex items-center gap-3 px-3 py-2 hover:bg-zinc-50">
                <div className="min-w-0 flex-1">
                  <div className="truncate text-sm text-zinc-900">{o.supplierName}</div>
                  <div className="tabular text-xs text-zinc-500">
                    {o.number} · {formatMoney(o.total, i18n.language)}
                  </div>
                </div>
                <div className="tabular text-right text-[13px] font-medium text-red-700">{formatDate(o.expectedDate)}</div>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </Panel>
  )
}
