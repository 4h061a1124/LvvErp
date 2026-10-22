import clsx from 'clsx'
import { useState, type ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { Input } from '@/components/ui/Field'
import { PageHeader } from '@/components/ui/PageHeader'
import { Pagination } from '@/components/ui/Pagination'
import type { MaterialRef } from '@/features/inventory/documents/documentModel'
import { MaterialPicker } from '@/features/inventory/documents/pickers'
import { getErrorMessage } from '@/lib/errors'
import { formatDate, formatMoney, formatQuantity } from '@/lib/format'
import { useListParams } from '@/lib/useListParams'
import { useOverdueLines, usePriceHistory, usePurchasingSummary } from '../api'
import { OrderStatusBadge, RequestStatusBadge } from '../components/StatusBadges'
import { ORDER_STATUSES, REQUEST_STATUSES } from '../types'

const DEFAULTS = { tab: 'summary', from: '', to: '', page: 1, size: 50 }
const TABS = ['summary', 'overdue', 'prices'] as const

export function PurchasingReportsPage() {
  const { t } = useTranslation()
  const [filters, setFilters] = useListParams(DEFAULTS)
  const tab = (TABS as readonly string[]).includes(filters.tab) ? (filters.tab as (typeof TABS)[number]) : 'summary'

  return (
    <>
      <PageHeader title={t('purchasing.reports.title')} />
      <div role="tablist" className="mb-4 flex gap-1 border-b border-zinc-200">
        {TABS.map((key) => (
          <button
            key={key}
            type="button"
            role="tab"
            aria-selected={tab === key}
            onClick={() => setFilters({ tab: key })}
            className={clsx(
              '-mb-px border-b-2 px-3 py-2 text-sm',
              tab === key ? 'border-brand-600 font-medium text-brand-700' : 'border-transparent text-zinc-600 hover:text-zinc-900',
            )}
          >
            {t(`purchasing.reports.tabs.${key}`)}
          </button>
        ))}
      </div>
      {tab === 'summary' && <Summary from={filters.from} to={filters.to} onChange={setFilters} />}
      {tab === 'overdue' && (
        <Overdue page={filters.page} size={filters.size} onPage={(page) => setFilters({ page })} onSize={(size) => setFilters({ size })} />
      )}
      {tab === 'prices' && <Prices />}
    </>
  )
}

function Panel({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="rounded-[4px] border border-zinc-200 bg-white">
      <h2 className="border-b border-zinc-200 px-3 py-2 text-sm font-semibold text-zinc-800">{title}</h2>
      {children}
    </section>
  )
}

function Summary({ from, to, onChange }: { from: string; to: string; onChange: (patch: { from?: string; to?: string }) => void }) {
  const { t, i18n } = useTranslation()
  const summary = usePurchasingSummary({ from: from || undefined, to: to || undefined })
  const data = summary.data
  const requestCount = (s: string) => data?.requests.find((r) => r.status === s)?.count ?? 0
  const order = (s: string) => data?.orders.find((o) => o.status === s)

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center gap-3">
        <label className="flex items-center gap-1.5 text-[13px] text-zinc-600">
          {t('inventory.documents.fromDate')}
          <Input type="date" value={from} onChange={(e) => onChange({ from: e.target.value })} className="w-36" />
        </label>
        <label className="flex items-center gap-1.5 text-[13px] text-zinc-600">
          {t('inventory.documents.toDate')}
          <Input type="date" value={to} onChange={(e) => onChange({ to: e.target.value })} className="w-36" />
        </label>
        <span className="text-xs text-zinc-500">{t('purchasing.reports.periodHint')}</span>
      </div>

      {summary.isError ? (
        <p className="py-10 text-center text-sm text-red-700">{getErrorMessage(summary.error, t)}</p>
      ) : !data ? (
        <p className="py-10 text-center text-sm text-zinc-500">{t('common.loading')}</p>
      ) : (
        <>
          <div className="grid grid-cols-2 gap-px overflow-hidden rounded-[4px] border border-zinc-200 bg-zinc-200">
            <Link to="/purchasing/orders?overdue=1" className="block bg-white px-4 py-3 hover:bg-zinc-50">
              <div className="text-[13px] text-zinc-500">{t('purchasing.reports.overdueOrders')}</div>
              <div className={clsx('tabular mt-1 text-2xl font-semibold', data.overdueOrders > 0 ? 'text-red-700' : 'text-zinc-900')}>
                {data.overdueOrders}
              </div>
            </Link>
            <div className="bg-white px-4 py-3">
              <div className="text-[13px] text-zinc-500">{t('purchasing.reports.openValue')}</div>
              <div className="tabular mt-1 text-2xl font-semibold text-zinc-900">{formatMoney(data.openOrderValue, i18n.language)}</div>
            </div>
          </div>

          <div className="grid items-start gap-4 lg:grid-cols-2">
            <Panel title={t('purchasing.reports.requestsByStatus')}>
              <table className="w-full text-left">
                <tbody>
                  {REQUEST_STATUSES.map((s) => (
                    <tr key={s} className="border-b border-zinc-100 last:border-b-0">
                      <td className="px-3 py-2">
                        <Link to={`/purchasing/requests?status=${s}`} className="hover:underline">
                          <RequestStatusBadge status={s} />
                        </Link>
                      </td>
                      <td className="tabular px-3 py-2 text-right text-sm text-zinc-900">{requestCount(s)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </Panel>
            <Panel title={t('purchasing.reports.ordersByStatus')}>
              <table className="w-full text-left">
                <thead>
                  <tr className="border-b border-zinc-200 bg-zinc-50 text-[13px] text-zinc-600">
                    <th className="px-3 py-2 font-medium">{t('inventory.status')}</th>
                    <th className="w-20 px-3 py-2 text-right font-medium">{t('purchasing.reports.count')}</th>
                    <th className="w-40 px-3 py-2 text-right font-medium">{t('purchasing.orders.total')}</th>
                  </tr>
                </thead>
                <tbody>
                  {ORDER_STATUSES.map((s) => (
                    <tr key={s} className="border-b border-zinc-100 last:border-b-0">
                      <td className="px-3 py-2">
                        <Link to={`/purchasing/orders?status=${s}`} className="hover:underline">
                          <OrderStatusBadge status={s} />
                        </Link>
                      </td>
                      <td className="tabular px-3 py-2 text-right text-sm text-zinc-900">{order(s)?.count ?? 0}</td>
                      <td className="tabular px-3 py-2 text-right text-sm text-zinc-700">{formatMoney(order(s)?.total ?? 0, i18n.language)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </Panel>
          </div>
        </>
      )}
    </div>
  )
}

function Overdue({ page, size, onPage, onSize }: { page: number; size: number; onPage: (page: number) => void; onSize: (size: number) => void }) {
  const { t, i18n } = useTranslation()
  const overdue = useOverdueLines(page, size)
  const rows = overdue.data?.items ?? []
  const qty = (v: number) => formatQuantity(v, i18n.language)

  return (
    <Panel title={t('purchasing.reports.overdueLines')}>
      {overdue.isError ? (
        <p className="px-3 py-10 text-center text-sm text-red-700">{getErrorMessage(overdue.error, t)}</p>
      ) : !overdue.data ? (
        <p className="px-3 py-10 text-center text-sm text-zinc-500">{t('common.loading')}</p>
      ) : rows.length === 0 ? (
        <p className="px-3 py-10 text-center text-sm text-zinc-500">{t('purchasing.reports.noOverdue')}</p>
      ) : (
        <>
          <div className="overflow-x-auto">
            <table className="w-full text-left">
              <thead>
                <tr className="border-b border-zinc-200 bg-zinc-50 text-[13px] text-zinc-600">
                  <th className="w-36 px-3 py-2 font-medium">{t('purchasing.orders.number')}</th>
                  <th className="px-3 py-2 font-medium">{t('purchasing.orders.supplier')}</th>
                  <th className="px-3 py-2 font-medium">{t('purchasing.requests.material')}</th>
                  <th className="w-28 px-3 py-2 font-medium">{t('purchasing.orders.expectedDate')}</th>
                  <th className="w-24 px-3 py-2 text-right font-medium">{t('purchasing.reports.daysLate')}</th>
                  <th className="w-28 px-3 py-2 text-right font-medium">{t('purchasing.requests.ordered')}</th>
                  <th className="w-28 px-3 py-2 text-right font-medium">{t('purchasing.orders.received')}</th>
                  <th className="w-28 px-3 py-2 text-right font-medium">{t('purchasing.reports.remaining')}</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((r) => (
                  <tr key={`${r.orderId}-${r.lineNo}`} className="border-b border-zinc-100 last:border-b-0">
                    <td className="px-3 py-2">
                      <Link to={`/purchasing/orders/${r.orderId}`} className="tabular text-[13px] font-medium text-brand-700 hover:underline">
                        {r.orderNumber}
                      </Link>
                    </td>
                    <td className="px-3 py-2 text-[13px] text-zinc-800">{r.supplierName}</td>
                    <td className="px-3 py-2 text-[13px]">
                      <span className="tabular mr-1.5 text-zinc-500">{r.materialCode}</span>
                      <span className="text-zinc-900">{r.materialName}</span>
                    </td>
                    <td className="tabular px-3 py-2 text-[13px] text-zinc-700">{formatDate(r.expectedDate)}</td>
                    <td className="tabular px-3 py-2 text-right text-[13px] font-medium text-red-700">{r.daysLate}</td>
                    <td className="tabular px-3 py-2 text-right text-[13px] text-zinc-700">{qty(r.ordered)}</td>
                    <td className="tabular px-3 py-2 text-right text-[13px] text-zinc-700">{qty(r.received)}</td>
                    <td className="tabular px-3 py-2 text-right text-[13px] text-zinc-900">
                      {qty(r.remaining)} <span className="text-xs text-zinc-500">{r.unitName}</span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <Pagination page={page} pageSize={size} total={overdue.data.totalCount} onPageChange={onPage} onPageSizeChange={onSize} />
        </>
      )}
    </Panel>
  )
}

function Prices() {
  const { t, i18n } = useTranslation()
  const [material, setMaterial] = useState<MaterialRef | null>(null)
  const [page, setPage] = useState(1)
  const [size, setSize] = useState(20)
  const history = usePriceHistory(material?.id, page, size)
  const rows = history.data?.items ?? []

  return (
    <div className="space-y-3">
      <div className="max-w-md">
        <MaterialPicker
          value={material}
          onChange={(m) => {
            setMaterial(m)
            setPage(1)
          }}
        />
      </div>
      <Panel title={material ? `${t('purchasing.reports.priceHistory')} · ${material.code} – ${material.name}` : t('purchasing.reports.priceHistory')}>
        {!material ? (
          <p className="px-3 py-10 text-center text-sm text-zinc-500">{t('purchasing.reports.chooseMaterial')}</p>
        ) : history.isError ? (
          <p className="px-3 py-10 text-center text-sm text-red-700">{getErrorMessage(history.error, t)}</p>
        ) : !history.data ? (
          <p className="px-3 py-10 text-center text-sm text-zinc-500">{t('common.loading')}</p>
        ) : rows.length === 0 ? (
          <p className="px-3 py-10 text-center text-sm text-zinc-500">{t('purchasing.reports.noPrices')}</p>
        ) : (
          <>
            <div className="overflow-x-auto">
              <table className="w-full text-left">
                <thead>
                  <tr className="border-b border-zinc-200 bg-zinc-50 text-[13px] text-zinc-600">
                    <th className="w-28 px-3 py-2 font-medium">{t('purchasing.orders.orderDate')}</th>
                    <th className="w-36 px-3 py-2 font-medium">{t('purchasing.orders.number')}</th>
                    <th className="px-3 py-2 font-medium">{t('purchasing.orders.supplier')}</th>
                    <th className="w-28 px-3 py-2 text-right font-medium">{t('purchasing.requests.quantity')}</th>
                    <th className="w-36 px-3 py-2 text-right font-medium">{t('purchasing.orders.unitPrice')}</th>
                    <th className="w-24 px-3 py-2 font-medium">{t('purchasing.orders.vatRate')}</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((r) => (
                    <tr key={`${r.orderId}`} className="border-b border-zinc-100 last:border-b-0">
                      <td className="tabular px-3 py-2 text-[13px] text-zinc-700">{formatDate(r.orderDate)}</td>
                      <td className="px-3 py-2">
                        <Link to={`/purchasing/orders/${r.orderId}`} className="tabular text-[13px] font-medium text-brand-700 hover:underline">
                          {r.orderNumber}
                        </Link>
                      </td>
                      <td className="px-3 py-2 text-[13px] text-zinc-800">{r.supplierName}</td>
                      <td className="tabular px-3 py-2 text-right text-[13px] text-zinc-700">{formatQuantity(r.quantity, i18n.language)}</td>
                      <td className="tabular px-3 py-2 text-right text-[13px] font-medium text-zinc-900">{formatMoney(r.unitPrice, i18n.language, 2)}</td>
                      <td className="px-3 py-2 text-[13px] text-zinc-600">{t(`purchasing.orders.vat.${r.vatRate}`)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Pagination
              page={page}
              pageSize={size}
              total={history.data.totalCount}
              onPageChange={setPage}
              onPageSizeChange={(s) => {
                setSize(s)
                setPage(1)
              }}
            />
          </>
        )}
      </Panel>
    </div>
  )
}
