import { Search } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/Button'
import { Dialog } from '@/components/ui/Dialog'
import { Input } from '@/components/ui/Field'
import { getErrorMessage } from '@/lib/errors'
import { formatDate, formatQuantity } from '@/lib/format'
import { useDebouncedValue } from '@/lib/hooks'
import { useOrderableLines } from '../api'
import type { OrderableLine } from '../types'

interface Props {
  /** Request lines already on the order; shown ticked and disabled. */
  usedLineIds: Set<string>
  onPick: (lines: OrderableLine[]) => void
  onClose: () => void
}

/** Approved request lines still to be ordered. Mount only while open. */
export function PickRequestLinesDialog({ usedLineIds, onPick, onClose }: Props) {
  const { t, i18n } = useTranslation()
  const [search, setSearch] = useState('')
  const term = useDebouncedValue(search.trim())
  const lines = useOrderableLines(term)
  const [picked, setPicked] = useState<Map<string, OrderableLine>>(new Map())
  const rows = lines.data?.items ?? []

  function toggle(line: OrderableLine) {
    setPicked((current) => {
      const next = new Map(current)
      if (next.has(line.requestLineId)) next.delete(line.requestLineId)
      else next.set(line.requestLineId, line)
      return next
    })
  }

  return (
    <Dialog
      open
      onClose={onClose}
      title={t('purchasing.orders.pick.title')}
      width="lg"
      footer={
        <>
          <span className="mr-auto text-[13px] text-zinc-500">{t('purchasing.orders.pick.selected', { count: picked.size })}</span>
          <Button onClick={onClose}>{t('common.cancel')}</Button>
          <Button variant="primary" disabled={picked.size === 0} onClick={() => onPick([...picked.values()])}>
            {t('purchasing.orders.pick.add')}
          </Button>
        </>
      }
    >
      <div className="relative mb-3">
        <Search size={15} className="pointer-events-none absolute top-1/2 left-2.5 -translate-y-1/2 text-zinc-400" />
        <Input
          type="search"
          autoFocus
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder={t('purchasing.orders.pick.search')}
          aria-label={t('purchasing.orders.pick.search')}
          className="pl-8"
        />
      </div>
      {lines.isPending ? (
        <p className="py-8 text-center text-sm text-zinc-500">{t('common.loading')}</p>
      ) : lines.isError ? (
        <p className="py-8 text-center text-sm text-red-700">{getErrorMessage(lines.error, t)}</p>
      ) : rows.length === 0 ? (
        <p className="py-8 text-center text-sm text-zinc-500">{t('purchasing.orders.pick.empty')}</p>
      ) : (
        <div className="max-h-[55vh] overflow-auto rounded-[3px] border border-zinc-200">
          <table className="w-full text-left">
            <thead className="sticky top-0 bg-zinc-50">
              <tr className="border-b border-zinc-200 text-[13px] text-zinc-600">
                <th className="w-8 py-2 pl-2.5" />
                <th className="px-2.5 py-2 font-medium">{t('purchasing.requests.number')}</th>
                <th className="px-2.5 py-2 font-medium">{t('purchasing.requests.material')}</th>
                <th className="px-2.5 py-2 text-right font-medium">{t('purchasing.orders.pick.remaining')}</th>
                <th className="px-2.5 py-2 font-medium">{t('purchasing.requests.neededDate')}</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((l) => {
                const used = usedLineIds.has(l.requestLineId)
                return (
                  <tr key={l.requestLineId} className="border-b border-zinc-100 last:border-b-0">
                    <td className="py-1.5 pl-2.5">
                      <input
                        type="checkbox"
                        disabled={used}
                        checked={used || picked.has(l.requestLineId)}
                        onChange={() => toggle(l)}
                        aria-label={`${l.requestNumber} ${l.materialCode}`}
                        className="size-4 accent-brand-600"
                      />
                    </td>
                    <td className="px-2.5 py-1.5 text-[13px]">
                      <div className="tabular text-zinc-800">{l.requestNumber}</div>
                      <div className="text-xs text-zinc-500">{l.departmentName}</div>
                    </td>
                    <td className="px-2.5 py-1.5 text-[13px]">
                      <span className="tabular mr-1.5 text-zinc-500">{l.materialCode}</span>
                      <span className="text-zinc-900">{l.materialName}</span>
                      {l.note && <div className="text-xs text-zinc-500">{l.note}</div>}
                    </td>
                    <td className="tabular px-2.5 py-1.5 text-right text-[13px] whitespace-nowrap text-zinc-900">
                      {formatQuantity(l.remainingQuantity, i18n.language)} <span className="text-xs text-zinc-500">{l.unitName}</span>
                    </td>
                    <td className="tabular px-2.5 py-1.5 text-[13px] text-zinc-700">{formatDate(l.neededDate)}</td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      )}
    </Dialog>
  )
}
