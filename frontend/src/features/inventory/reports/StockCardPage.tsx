import { useQuery } from '@tanstack/react-query'
import clsx from 'clsx'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { Field, Input, Select } from '@/components/ui/Field'
import { PageHeader } from '@/components/ui/PageHeader'
import { api } from '@/lib/api'
import { getErrorMessage } from '@/lib/errors'
import { formatDate, formatQuantity, todayIso } from '@/lib/format'
import { useListParams } from '@/lib/useListParams'
import { useWarehouses } from '../api'
import type { MaterialRef } from '../documents/documentModel'
import { MaterialPicker } from '../documents/pickers'
import { DOCUMENT_PATHS } from '../documents/types'
import type { Material } from '../types'
import { useStockCard, type StockCardEntry } from './api'

// Default period: the current month.
const DEFAULTS = { material: '', wh: '', from: `${todayIso().slice(0, 8)}01`, to: todayIso() }

export function StockCardPage() {
  const { t, i18n } = useTranslation()
  const [filters, setFilters] = useListParams(DEFAULTS)
  const warehouses = useWarehouses()
  const card = useStockCard({
    materialId: filters.material || undefined,
    warehouseId: filters.wh || undefined,
    from: filters.from || undefined,
    to: filters.to || undefined,
  })
  // The picker shows the selected material; load it when the page is opened from a link.
  const material = useQuery({
    queryKey: ['inventory', 'materials', 'one', filters.material],
    queryFn: async () => (await api.get<Material>(`/materials/${filters.material}`)).data,
    enabled: !!filters.material,
  })
  const selected: MaterialRef | null = material.data
    ? { id: material.data.id, code: material.data.code, name: material.data.name, unitName: material.data.unitName, trackLots: material.data.trackLots }
    : null

  const qty = (n: number) => (n ? formatQuantity(n, i18n.language) : '')
  const data = card.data

  function describe(e: StockCardEntry): string {
    if (e.isReversal) return t('inventory.card.reversal')
    if (e.documentType === 'Transfer' && e.otherWarehouseName)
      return e.out > 0 ? t('inventory.card.transferTo', { name: e.otherWarehouseName }) : t('inventory.card.transferFrom', { name: e.otherWarehouseName })
    return [e.reason && t(`inventory.documents.reasons.${e.reason}`), e.partner ?? e.recipient].filter(Boolean).join(' – ')
  }

  return (
    <>
      <PageHeader title={t('inventory.card.title')} />

      <div className="mb-4 grid gap-3 rounded-[4px] border border-zinc-200 bg-white p-3 sm:grid-cols-2 lg:grid-cols-[minmax(0,2fr)_minmax(0,1fr)_10rem_10rem]">
        <Field label={t('inventory.documents.material')} htmlFor="card-material">
          <MaterialPicker id="card-material" value={selected} onChange={(m) => setFilters({ material: m.id })} />
        </Field>
        <Field label={t('inventory.documents.warehouse')} htmlFor="card-warehouse">
          <Select id="card-warehouse" value={filters.wh} onChange={(e) => setFilters({ wh: e.target.value })}>
            <option value="">{t('inventory.card.allWarehouses')}</option>
            {warehouses.data?.map((w) => (
              <option key={w.id} value={w.id}>
                {w.name}
              </option>
            ))}
          </Select>
        </Field>
        <Field label={t('inventory.documents.fromDate')} htmlFor="card-from">
          <Input id="card-from" type="date" value={filters.from} onChange={(e) => setFilters({ from: e.target.value })} />
        </Field>
        <Field label={t('inventory.documents.toDate')} htmlFor="card-to">
          <Input id="card-to" type="date" value={filters.to} onChange={(e) => setFilters({ to: e.target.value })} />
        </Field>
      </div>

      {!filters.material ? (
        <p className="rounded-[4px] border border-zinc-200 bg-white px-3 py-10 text-center text-sm text-zinc-500">{t('inventory.card.chooseMaterial')}</p>
      ) : card.isError ? (
        <p className="py-10 text-center text-sm text-red-700">{getErrorMessage(card.error, t)}</p>
      ) : !data ? (
        <p className="py-10 text-center text-sm text-zinc-500">{t('common.loading')}</p>
      ) : (
        <div className={clsx('transition-opacity', card.isPlaceholderData && 'opacity-60')}>
          <div className="mb-4 grid grid-cols-2 gap-px overflow-hidden rounded-[4px] border border-zinc-200 bg-zinc-200 lg:grid-cols-4">
            {(
              [
                ['opening', data.opening],
                ['totalIn', data.totalIn],
                ['totalOut', data.totalOut],
                ['closing', data.closing],
              ] as const
            ).map(([key, value]) => (
              <div key={key} className="bg-white px-4 py-2.5">
                <div className="text-[13px] text-zinc-500">{t(`inventory.card.${key}`)}</div>
                <div className="tabular mt-0.5 text-lg font-semibold text-zinc-900">
                  {formatQuantity(value, i18n.language)} <span className="text-xs font-normal text-zinc-500">{data.unitName}</span>
                </div>
              </div>
            ))}
          </div>

          {data.truncated && <p className="mb-2 text-[13px] text-amber-700">{t('inventory.card.truncated')}</p>}

          <div className="overflow-x-auto rounded-[4px] border border-zinc-200 bg-white">
            {data.entries.length === 0 ? (
              <p className="px-3 py-8 text-center text-sm text-zinc-500">{t('inventory.card.empty')}</p>
            ) : (
              <table className="w-full text-left">
                <thead>
                  <tr className="border-b border-zinc-200 bg-zinc-50 text-[13px] text-zinc-600">
                    <th className="w-24 px-3 py-2 font-medium">{t('inventory.card.date')}</th>
                    <th className="w-36 px-3 py-2 font-medium">{t('inventory.card.document')}</th>
                    <th className="px-3 py-2 font-medium">{t('inventory.card.description')}</th>
                    <th className="hidden px-3 py-2 font-medium md:table-cell">{t('inventory.documents.warehouse')}</th>
                    <th className="hidden px-3 py-2 font-medium md:table-cell">{t('inventory.documents.lot')}</th>
                    <th className="w-24 px-3 py-2 text-right font-medium">{t('inventory.card.in')}</th>
                    <th className="w-24 px-3 py-2 text-right font-medium">{t('inventory.card.out')}</th>
                    <th className="w-28 px-3 py-2 text-right font-medium">{t('inventory.card.balance')}</th>
                  </tr>
                </thead>
                <tbody>
                  {data.entries.map((e) => (
                    <tr key={e.transactionId} className={clsx('border-b border-zinc-100 last:border-b-0', e.isReversal && 'bg-red-50/40')}>
                      <td className="tabular px-3 py-1.5 text-[13px] text-zinc-700">{formatDate(e.date)}</td>
                      <td className="px-3 py-1.5">
                        <Link to={`${DOCUMENT_PATHS[e.documentType].route}/${e.documentId}`} className="tabular text-[13px] text-brand-700 hover:underline">
                          {e.documentNumber}
                        </Link>
                      </td>
                      <td className="px-3 py-1.5 text-[13px] text-zinc-700">{describe(e)}</td>
                      <td className="hidden px-3 py-1.5 text-[13px] text-zinc-600 md:table-cell">
                        {e.warehouseName}
                        {e.locationCode && <span className="tabular ml-1 text-zinc-500">· {e.locationCode}</span>}
                      </td>
                      <td className="hidden px-3 py-1.5 text-[13px] text-zinc-600 md:table-cell">{e.lotNumber}</td>
                      <td className="tabular px-3 py-1.5 text-right text-zinc-900">{qty(e.in)}</td>
                      <td className="tabular px-3 py-1.5 text-right text-zinc-900">{qty(e.out)}</td>
                      <td className="tabular px-3 py-1.5 text-right font-medium text-zinc-900">{formatQuantity(e.balance, i18n.language)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </div>
      )}
    </>
  )
}
