import { Download } from 'lucide-react'
import { useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/Button'
import { Dialog } from '@/components/ui/Dialog'
import { useToast } from '@/components/ui/toast-context'
import { getErrorMessage, getStatus } from '@/lib/errors'
import { downloadImportTemplate, useImportEmployees } from '../api'
import { importColumnLabel, importErrorMessage } from '../importErrors'
import type { ImportResult } from '../types'

/** Mount only while open, so every opening starts clean. */
export function ImportDialog({ onClose }: { onClose: () => void }) {
  const { t } = useTranslation()
  const toast = useToast()
  const importEmployees = useImportEmployees()
  const fileInput = useRef<HTMLInputElement>(null)

  const [file, setFile] = useState<File | null>(null)
  const [fileError, setFileError] = useState<string | null>(null)
  const [updateExisting, setUpdateExisting] = useState(false)
  const [result, setResult] = useState<ImportResult | null>(null)
  const [downloading, setDownloading] = useState(false)

  function chooseFile(chosen: File | undefined) {
    if (!chosen) return
    const ok = chosen.name.toLowerCase().endsWith('.xlsx')
    setFileError(ok ? null : t('import.wrongType'))
    setFile(ok ? chosen : null)
    setResult(null)
  }

  async function handleDownload() {
    setDownloading(true)
    try {
      await downloadImportTemplate()
    } catch (err) {
      toast.error(getErrorMessage(err, t))
    } finally {
      setDownloading(false)
    }
  }

  async function handleImport() {
    if (!file) return
    try {
      setResult(await importEmployees.mutateAsync({ file, updateExisting }))
    } catch (err) {
      setResult(null)
      // 400 = file-level problem (not a workbook, wrong header, no rows); row errors come back as a 200 result.
      setFileError(getStatus(err) === 400 ? t('import.unreadable') : getErrorMessage(err, t))
    }
  }

  const done = result?.succeeded === true

  return (
    <Dialog
      open
      onClose={onClose}
      title={t('import.title')}
      width="lg"
      footer={
        done ? (
          <Button variant="primary" onClick={onClose}>
            {t('common.close')}
          </Button>
        ) : (
          <>
            <Button onClick={onClose} disabled={importEmployees.isPending}>
              {t('common.cancel')}
            </Button>
            <Button variant="primary" onClick={handleImport} disabled={!file || importEmployees.isPending}>
              {importEmployees.isPending ? t('import.submitting') : t('import.submit')}
            </Button>
          </>
        )
      }
    >
      {done ? (
        <p className="border-l-[3px] border-emerald-600 bg-emerald-50 px-3 py-2.5 text-sm text-emerald-900">
          {t('import.success', { total: result.totalRows, created: result.created, updated: result.updated })}
        </p>
      ) : (
        <div className="space-y-4 text-sm">
          <section>
            <div className="mb-1.5 font-medium text-zinc-800">1. {t('import.step1')}</div>
            <Button size="sm" onClick={handleDownload} disabled={downloading}>
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
                chooseFile(e.target.files?.[0])
                e.target.value = ''
              }}
            />
          </section>

          <label className="flex cursor-pointer items-start gap-2">
            <input
              type="checkbox"
              checked={updateExisting}
              onChange={(e) => setUpdateExisting(e.target.checked)}
              className="mt-0.5 size-4 accent-brand-600"
            />
            <span>
              <span className="text-zinc-800">{t('import.updateExisting')}</span>
              <span className="block text-xs text-zinc-500">{t('import.updateExistingHint')}</span>
            </span>
          </label>

          <p className="text-xs text-zinc-500">{t('import.allOrNothing')}</p>

          {result && !result.succeeded && (
            <section>
              <p className="mb-2 border-l-[3px] border-red-600 bg-red-50 px-3 py-2 text-[13px] text-red-800">
                {t('import.failed', { count: result.errors.length })}
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
                    {result.errors.map((error, i) => (
                      <tr key={i} className="border-b border-zinc-100 last:border-b-0">
                        <td className="tabular px-2.5 py-1.5 text-zinc-600">{error.row}</td>
                        <td className="px-2.5 py-1.5 text-zinc-700">{importColumnLabel(error.column, t)}</td>
                        <td className="px-2.5 py-1.5 text-zinc-900">{importErrorMessage(error, t)}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </section>
          )}
        </div>
      )}
    </Dialog>
  )
}
