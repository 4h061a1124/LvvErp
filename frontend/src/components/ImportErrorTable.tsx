import { useTranslation } from 'react-i18next'
import { importErrorMessage, type ImportError } from '@/lib/import'

/** Row-by-row problems of a rejected import file. */
export function ImportErrorTable({ errors, columnLabel }: { errors: ImportError[]; columnLabel: (column: string) => string }) {
  const { t } = useTranslation()
  return (
    <section>
      <p className="mb-2 border-l-[3px] border-red-600 bg-red-50 px-3 py-2 text-[13px] text-red-800">
        {t('import.failed', { count: errors.length })}
      </p>
      <div className="max-h-64 overflow-auto rounded-[3px] border border-zinc-200">
        <table className="w-full text-left text-[13px]">
          <thead className="sticky top-0 bg-zinc-50 text-zinc-600">
            <tr className="border-b border-zinc-200">
              <th className="w-14 px-2.5 py-1.5 font-medium">{t('import.row')}</th>
              <th className="w-32 px-2.5 py-1.5 font-medium">{t('import.column')}</th>
              <th className="px-2.5 py-1.5 font-medium">{t('import.error')}</th>
            </tr>
          </thead>
          <tbody>
            {errors.map((error, i) => (
              <tr key={i} className="border-b border-zinc-100 last:border-b-0">
                <td className="tabular px-2.5 py-1.5 text-zinc-600">{error.row}</td>
                <td className="px-2.5 py-1.5 text-zinc-700">{columnLabel(error.column)}</td>
                <td className="px-2.5 py-1.5 text-zinc-900">{importErrorMessage(error, t)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  )
}
