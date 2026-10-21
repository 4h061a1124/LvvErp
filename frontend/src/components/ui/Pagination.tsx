import { ChevronLeft, ChevronRight } from 'lucide-react'
import { useTranslation } from 'react-i18next'

const PAGE_SIZES = [20, 50, 100]

interface PaginationProps {
  page: number
  pageSize: number
  total: number
  onPageChange: (page: number) => void
  onPageSizeChange: (size: number) => void
}

export function Pagination({ page, pageSize, total, onPageChange, onPageSizeChange }: PaginationProps) {
  const { t } = useTranslation()
  const totalPages = Math.max(1, Math.ceil(total / pageSize))
  const from = total === 0 ? 0 : (page - 1) * pageSize + 1
  const to = Math.min(page * pageSize, total)

  return (
    <div className="flex items-center justify-between gap-3 border-t border-zinc-200 px-3 py-2 text-[13px] text-zinc-600">
      <label className="flex items-center gap-2">
        <span className="hidden sm:inline">{t('pager.perPage')}</span>
        <select
          value={pageSize}
          onChange={(e) => onPageSizeChange(Number(e.target.value))}
          className="h-7 rounded-[3px] border border-zinc-300 bg-white px-1.5 text-[13px]"
        >
          {PAGE_SIZES.map((size) => (
            <option key={size} value={size}>
              {size}
            </option>
          ))}
        </select>
      </label>

      <div className="flex items-center gap-1">
        <span className="tabular mr-2">{t('pager.range', { from, to, total })}</span>
        <button
          type="button"
          onClick={() => onPageChange(page - 1)}
          disabled={page <= 1}
          className="rounded-[3px] border border-zinc-300 bg-white p-1 hover:bg-zinc-50 disabled:opacity-40"
          aria-label={t('pager.prev')}
        >
          <ChevronLeft size={16} />
        </button>
        <button
          type="button"
          onClick={() => onPageChange(page + 1)}
          disabled={page >= totalPages}
          className="rounded-[3px] border border-zinc-300 bg-white p-1 hover:bg-zinc-50 disabled:opacity-40"
          aria-label={t('pager.next')}
        >
          <ChevronRight size={16} />
        </button>
      </div>
    </div>
  )
}
