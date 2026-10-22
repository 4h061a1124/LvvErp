import type { ReactNode } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { PageHeader } from '@/components/ui/PageHeader'
import { getErrorMessage } from '@/lib/errors'
import { formatDate, formatDateTime, formatMoney } from '@/lib/format'
import { useApprovalInbox } from '../api'

function Panel({ title, count, children }: { title: string; count: number; children: ReactNode }) {
  return (
    <section className="rounded-[4px] border border-zinc-200 bg-white">
      <h2 className="border-b border-zinc-200 px-3 py-2 text-sm font-semibold text-zinc-800">
        {title} <span className="tabular font-normal text-zinc-500">({count})</span>
      </h2>
      {children}
    </section>
  )
}

export function ApprovalsPage() {
  const { t, i18n } = useTranslation()
  const inbox = useApprovalInbox()

  if (inbox.isError) return <p className="py-10 text-center text-sm text-red-700">{getErrorMessage(inbox.error, t)}</p>
  if (!inbox.data) return <p className="py-10 text-center text-sm text-zinc-500">{t('common.loading')}</p>

  const { requests, orders } = inbox.data
  const none = <p className="px-3 py-6 text-center text-sm text-zinc-500">{t('purchasing.approvals.none')}</p>

  return (
    <>
      <PageHeader title={t('purchasing.approvals.title')} />
      {requests.length === 0 && orders.length === 0 ? (
        <p className="rounded-[4px] border border-zinc-200 bg-white px-3 py-10 text-center text-sm text-zinc-500">
          {t('purchasing.approvals.empty')}
        </p>
      ) : (
        <div className="space-y-5">
          <Panel title={t('purchasing.approvals.requests')} count={requests.length}>
            {requests.length === 0 ? (
              none
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-left">
                  <thead>
                    <tr className="border-b border-zinc-200 bg-zinc-50 text-[13px] text-zinc-600">
                      <th className="w-36 px-3 py-2 font-medium">{t('purchasing.approvals.number')}</th>
                      <th className="px-3 py-2 font-medium">{t('purchasing.requests.department')}</th>
                      <th className="px-3 py-2 font-medium">{t('purchasing.requests.requester')}</th>
                      <th className="px-3 py-2 font-medium">{t('purchasing.requests.purpose')}</th>
                      <th className="w-32 px-3 py-2 font-medium">{t('purchasing.requests.neededDate')}</th>
                      <th className="w-36 px-3 py-2 font-medium">{t('purchasing.approvals.submittedAt')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {requests.map((r) => (
                      <tr key={r.id} className="border-b border-zinc-100 last:border-b-0 hover:bg-zinc-50/70">
                        <td className="px-3 py-2">
                          <Link to={`/purchasing/requests/${r.id}`} className="tabular text-[13px] font-medium text-brand-700 hover:underline">
                            {r.number}
                          </Link>
                        </td>
                        <td className="px-3 py-2 text-[13px] text-zinc-700">{r.departmentName}</td>
                        <td className="px-3 py-2 text-[13px] text-zinc-800">{r.requesterName}</td>
                        <td className="max-w-72 truncate px-3 py-2 text-[13px] text-zinc-600">{r.note}</td>
                        <td className="tabular px-3 py-2 text-[13px] text-zinc-700">{formatDate(r.neededDate)}</td>
                        <td className="tabular px-3 py-2 text-[13px] text-zinc-600">{formatDateTime(r.submittedAt, i18n.language)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </Panel>

          <Panel title={t('purchasing.approvals.orders')} count={orders.length}>
            {orders.length === 0 ? (
              none
            ) : (
              <div className="overflow-x-auto">
                <table className="w-full text-left">
                  <thead>
                    <tr className="border-b border-zinc-200 bg-zinc-50 text-[13px] text-zinc-600">
                      <th className="w-36 px-3 py-2 font-medium">{t('purchasing.approvals.number')}</th>
                      <th className="px-3 py-2 font-medium">{t('purchasing.approvals.supplier')}</th>
                      <th className="w-40 px-3 py-2 text-right font-medium">{t('purchasing.approvals.total')}</th>
                      <th className="w-44 px-3 py-2 font-medium">{t('purchasing.approvals.level')}</th>
                      <th className="w-28 px-3 py-2 font-medium">{t('inventory.documents.createdBy')}</th>
                      <th className="w-36 px-3 py-2 font-medium">{t('purchasing.approvals.submittedAt')}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {orders.map((o) => (
                      <tr key={o.id} className="border-b border-zinc-100 last:border-b-0 hover:bg-zinc-50/70">
                        <td className="px-3 py-2">
                          <Link to={`/purchasing/orders/${o.id}`} className="tabular text-[13px] font-medium text-brand-700 hover:underline">
                            {o.number}
                          </Link>
                        </td>
                        <td className="px-3 py-2 text-[13px] text-zinc-800">{o.supplierName}</td>
                        <td className="tabular px-3 py-2 text-right text-[13px] text-zinc-900">{formatMoney(o.total, i18n.language)}</td>
                        <td className="px-3 py-2 text-[13px] text-zinc-700">{o.approvalLevel && t(`purchasing.orders.level.${o.approvalLevel}`)}</td>
                        <td className="px-3 py-2 text-[13px] text-zinc-600">{o.createdBy}</td>
                        <td className="tabular px-3 py-2 text-[13px] text-zinc-600">{formatDateTime(o.submittedAt, i18n.language)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </Panel>
        </div>
      )}
    </>
  )
}
