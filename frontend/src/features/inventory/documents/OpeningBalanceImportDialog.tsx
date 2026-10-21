import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Download } from 'lucide-react'
import { useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { ImportErrorTable } from '@/components/ImportErrorTable'
import { Button } from '@/components/ui/Button'
import { Dialog } from '@/components/ui/Dialog'
import { Field, Input } from '@/components/ui/Field'
import { useToast } from '@/components/ui/toast-context'
import { api, downloadFile } from '@/lib/api'
import { getErrorMessage, getStatus } from '@/lib/errors'
import { todayIso } from '@/lib/format'
import type { ImportError } from '@/lib/import'
import type { Messages } from '@/locales/vi'
import { documentKeys } from './api'
import { DOCUMENT_PATHS } from './types'

interface OpeningBalanceResult {
  succeeded: boolean
  totalRows: number
  errors: ImportError[]
  documents: { id: string; number: string; warehouseName: string; lineCount: number }[]
}

type ColumnKey = keyof Messages['inventory']['documents']
const columnKeys: Record<string, ColumnKey> = {
  Material: 'material',
  Warehouse: 'warehouse',
  Location: 'location',
  LotNumber: 'lotNumber',
  ManufactureDate: 'manufactureDate',
  ExpiryDate: 'expiryDate',
  Quantity: 'quantity',
}

/** Mount only while open. Creates draft "opening balance" receipts; nothing touches stock until approved. */
export function OpeningBalanceImportDialog({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const toast = useToast()
  const queryClient = useQueryClient()
  const fileInput = useRef<HTMLInputElement>(null)
  const [file, setFile] = useState<File | null>(null)
  const [fileError, setFileError] = useState<string | null>(null)
  const [documentDate, setDocumentDate] = useState(todayIso())
  const [note, setNote] = useState('')
  const [result, setResult] = useState<OpeningBalanceResult | null>(null)

  const upload = useMutation({
    mutationFn: async () => {
      const form = new FormData()
      form.append('file', file!)
      return (
        await api.post<OpeningBalanceResult>('/stock/opening-balance/import', form, {
          params: { documentDate, note: note.trim() || undefined },
        })
      ).data
    },
  })

  async function handleImport() {
    try {
      const imported = await upload.mutateAsync()
      setResult(imported)
      if (imported.succeeded) await queryClient.invalidateQueries({ queryKey: documentKeys.all })
    } catch (err) {
      setResult(null)
      setFileError(getStatus(err) === 400 ? t('import.unreadable') : getErrorMessage(err, t))
    }
  }

  const done = result?.succeeded === true

  return (
    <Dialog
      open
      onClose={onClose}
      title={t('inventory.openingImport.title')}
      width="lg"
      footer={
        done ? (
          <Button variant="primary" onClick={onClose}>
            {t('common.close')}
          </Button>
        ) : (
          <>
            <Button onClick={onClose} disabled={upload.isPending}>
              {t('common.cancel')}
            </Button>
            <Button variant="primary" onClick={handleImport} disabled={!file || !documentDate || upload.isPending}>
              {upload.isPending ? t('import.submitting') : t('import.submit')}
            </Button>
          </>
        )
      }
    >
      {done ? (
        <div className="space-y-2 text-sm">
          <p className="border-l-[3px] border-emerald-600 bg-emerald-50 px-3 py-2.5 text-emerald-900">
            {t('inventory.openingImport.created', { count: result.documents.length })}
          </p>
          <ul className="divide-y divide-zinc-100 rounded-[3px] border border-zinc-200">
            {result.documents.map((d) => (
              <li key={d.id} className="flex items-center justify-between px-3 py-2">
                <Link to={`${DOCUMENT_PATHS.Receipt.route}/${d.id}`} onClick={onClose} className="tabular font-medium text-brand-700 hover:underline">
                  {d.number}
                </Link>
                <span className="text-[13px] text-zinc-600">
                  {d.warehouseName} · {t('inventory.documents.lineCount')}: {d.lineCount}
                </span>
              </li>
            ))}
          </ul>
        </div>
      ) : (
        <div className="space-y-4 text-sm">
          <section>
            <div className="mb-1.5 font-medium text-zinc-800">1. {t('inventory.openingImport.step1')}</div>
            <Button
              size="sm"
              onClick={() => downloadFile('/stock/opening-balance/template', 'mau-ton-dau-ky.xlsx').catch((err) => toast.error(getErrorMessage(err, t)))}
            >
              <Download size={14} />
              {t('import.downloadTemplate')}
            </Button>
          </section>
          <section>
            <div className="mb-1.5 font-medium text-zinc-800">2. {t('import.step2')}</div>
            <div className="flex items-center gap-2">
              <Button size="sm" onClick={() => fileInput.current?.click()}>
                {t('import.chooseFile')}
              </Button>
              <span className="min-w-0 truncate text-[13px] text-zinc-600">{file?.name ?? t('import.noFile')}</span>
            </div>
            {fileError && <p className="mt-1.5 text-xs text-red-600">{fileError}</p>}
            <input
              ref={fileInput}
              type="file"
              accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
              className="hidden"
              onChange={(e) => {
                const chosen = e.target.files?.[0]
                e.target.value = ''
                if (!chosen) return
                const ok = chosen.name.toLowerCase().endsWith('.xlsx')
                setFileError(ok ? null : t('import.wrongType'))
                setFile(ok ? chosen : null)
                setResult(null)
              }}
            />
          </section>
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label={t('inventory.openingImport.documentDate')} htmlFor="ob-date" required>
              <Input id="ob-date" type="date" max={todayIso()} value={documentDate} onChange={(e) => setDocumentDate(e.target.value)} />
            </Field>
            <Field label={t('inventory.openingImport.note')} htmlFor="ob-note">
              <Input id="ob-note" value={note} maxLength={500} onChange={(e) => setNote(e.target.value)} />
            </Field>
          </div>
          <p className="text-xs text-zinc-500">{t('inventory.openingImport.hint')}</p>
          <p className="text-xs text-zinc-500">{t('import.allOrNothing')}</p>
          {result && !result.succeeded && (
            <ImportErrorTable
              errors={result.errors}
              columnLabel={(column) => (columnKeys[column] ? t(`inventory.documents.${columnKeys[column]}` as never) : column)}
            />
          )}
        </div>
      )}
    </Dialog>
  )
}
