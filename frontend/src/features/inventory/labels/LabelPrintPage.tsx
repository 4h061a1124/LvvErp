import clsx from 'clsx'
import { X } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link, useLocation } from 'react-router'
import { Button } from '@/components/ui/Button'
import { Input } from '@/components/ui/Field'
import { PageHeader } from '@/components/ui/PageHeader'
import { formatDate } from '@/lib/format'
import { LAYOUTS, MAX_COPIES, paginateLabels, qrSvg, type LabelItem, type LabelLayout } from './labels'

/** Items come from the page that opened this one (router state); a refresh starts empty. */
export function LabelPrintPage() {
  const { t } = useTranslation()
  const location = useLocation()
  const [items, setItems] = useState<LabelItem[]>(() => (location.state as { items?: LabelItem[] } | null)?.items ?? [])
  const [layout, setLayout] = useState<LabelLayout>('a4')
  const [qr, setQr] = useState<Record<string, string>>({})

  const codes = useMemo(() => [...new Set(items.map((i) => i.code))], [items])
  useEffect(() => {
    let cancelled = false
    Promise.all(codes.map(async (code) => [code, await qrSvg(code)] as const)).then((pairs) => {
      if (!cancelled) setQr(Object.fromEntries(pairs))
    })
    return () => {
      cancelled = true
    }
  }, [codes])

  const pages = paginateLabels(items, layout)
  const total = pages.reduce((sum, page) => sum + page.length, 0)
  const format = LAYOUTS[layout]

  const setCopies = (key: string, copies: number) =>
    setItems((current) => current.map((i) => (i.key === key ? { ...i, copies } : i)))

  return (
    <>
      {/* Paper size for the browser's print dialog. */}
      <style>{`@page { ${format.page} }`}</style>

      <div className="print:hidden">
        <PageHeader
          title={t('inventory.labels.title')}
          meta={items.length > 0 && t('inventory.labels.total', { count: total })}
          actions={
            <>
              <Link to="/inventory/materials" className="inline-flex h-8 items-center px-2 text-sm text-brand-700 hover:underline">
                {t('common.back')}
              </Link>
              <Button variant="primary" onClick={() => window.print()} disabled={total === 0 || codes.some((c) => !qr[c])}>
                {t('inventory.labels.print')}
              </Button>
            </>
          }
        />

        {items.length === 0 ? (
          <p className="rounded-[4px] border border-zinc-200 bg-white px-4 py-8 text-center text-sm text-zinc-600">
            {t('inventory.labels.empty')}
          </p>
        ) : (
          <div className="mb-5 grid gap-4 lg:grid-cols-[minmax(0,1fr)_280px]">
            <div className="rounded-[4px] border border-zinc-200 bg-white">
              <table className="w-full text-left">
                <thead>
                  <tr className="border-b border-zinc-200 bg-zinc-50 text-[13px] text-zinc-600">
                    <th className="px-3 py-2 font-medium">{t('inventory.materials.columns.code')}</th>
                    <th className="px-3 py-2 font-medium">{t('inventory.materials.columns.name')}</th>
                    <th className="w-24 px-3 py-2 font-medium">{t('inventory.labels.copies')}</th>
                    <th className="w-10" />
                  </tr>
                </thead>
                <tbody>
                  {items.map((item) => (
                    <tr key={item.key} className="border-b border-zinc-100 last:border-b-0">
                      <td className="tabular px-3 py-1.5 text-[13px] text-zinc-600">{item.code}</td>
                      <td className="px-3 py-1.5 text-sm text-zinc-900">
                        {item.name}
                        {item.lotNumber && (
                          <span className="ml-2 text-xs text-zinc-500">
                            {t('inventory.labels.lot')} {item.lotNumber}
                          </span>
                        )}
                      </td>
                      <td className="px-3 py-1.5">
                        <Input
                          type="number"
                          min={0}
                          max={MAX_COPIES}
                          value={item.copies}
                          onChange={(e) => setCopies(item.key, Number(e.target.value))}
                          aria-label={`${t('inventory.labels.copies')} ${item.code}`}
                          className="h-7 w-20"
                        />
                      </td>
                      <td className="pr-2 text-right">
                        <button
                          type="button"
                          onClick={() => setItems((current) => current.filter((i) => i.key !== item.key))}
                          className="rounded-[3px] p-1 text-zinc-400 hover:bg-zinc-100 hover:text-zinc-700"
                          aria-label={t('inventory.labels.remove')}
                        >
                          <X size={14} />
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <fieldset className="self-start rounded-[4px] border border-zinc-200 bg-white px-3 py-2.5">
              <legend className="px-1 text-[13px] font-medium text-zinc-700">{t('inventory.labels.layout')}</legend>
              {(['a4', 'roll'] as const).map((value) => (
                <label key={value} className="flex cursor-pointer items-center gap-2 py-1 text-sm text-zinc-800">
                  <input
                    type="radio"
                    name="layout"
                    checked={layout === value}
                    onChange={() => setLayout(value)}
                    className="size-4 accent-brand-600"
                  />
                  {t(`inventory.labels.${value}`)}
                </label>
              ))}
              <p className="mt-2 text-xs text-zinc-500">{t('inventory.labels.hint')}</p>
            </fieldset>
          </div>
        )}
      </div>

      {/* Preview on screen; exactly what is printed. */}
      {pages.length > 0 && (
        <div className="overflow-x-auto pb-4 print:overflow-visible print:pb-0">
          {pages.map((page, p) => (
            <div
              key={p}
              style={{ width: format.pageWidth, height: format.pageHeight }}
              className={clsx(
                'mb-4 bg-white shadow-[0_0_0_1px_rgb(228_228_231)] print:mb-0 print:shadow-none',
                p < pages.length - 1 && 'print:break-after-page',
                layout === 'a4' && 'grid grid-cols-3 grid-rows-8',
              )}
            >
              {page.map((item, i) => (
                <Label key={i} item={item} svg={qr[item.code]} layout={layout} />
              ))}
            </div>
          ))}
        </div>
      )}
    </>
  )
}

function Label({ item, svg, layout }: { item: LabelItem; svg: string | undefined; layout: LabelLayout }) {
  const { t } = useTranslation()
  const small = layout === 'roll'
  return (
    <div
      className={clsx(
        'flex items-center overflow-hidden text-black',
        small ? 'h-[30mm] w-[50mm] gap-[2mm] p-[2mm]' : 'h-[36mm] w-[70mm] gap-[2.5mm] px-[3mm] py-[2.5mm]',
      )}
    >
      <div
        className={clsx('shrink-0 [&>svg]:block [&>svg]:h-full [&>svg]:w-full', small ? 'size-[22mm]' : 'size-[26mm]')}
        // SVG produced locally by the qrcode library from the material code.
        dangerouslySetInnerHTML={svg ? { __html: svg } : undefined}
      />
      <div className={clsx('min-w-0 flex-1 leading-tight', small ? 'text-[6.5pt]' : 'text-[7.5pt]')}>
        <div className={clsx('font-semibold break-all', small ? 'text-[8pt]' : 'text-[10pt]')}>{item.code}</div>
        <div className="mt-[0.8mm] line-clamp-2">{item.name}</div>
        {item.specification && <div className="truncate text-zinc-700">{item.specification}</div>}
        <div className="mt-[0.8mm] text-zinc-700">{item.unitName}</div>
        {item.lotNumber && (
          <div className="mt-[0.5mm] font-medium">
            {t('inventory.labels.lot')}: {item.lotNumber}
            {item.expiryDate && ` · ${t('inventory.labels.expiry')}: ${formatDate(item.expiryDate)}`}
          </div>
        )}
      </div>
    </div>
  )
}
