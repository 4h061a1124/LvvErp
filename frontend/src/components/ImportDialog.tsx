import { useMutation } from '@tanstack/react-query'
import { Download } from 'lucide-react'
import { useRef, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button } from '@/components/ui/Button'
import { Dialog } from '@/components/ui/Dialog'
import { useToast } from '@/components/ui/toast-context'
import { api, downloadFile } from '@/lib/api'
import { getErrorMessage, getStatus } from '@/lib/errors'
import type { ImportResult } from '@/lib/import'
import { ImportErrorTable } from './ImportErrorTable'

interface ImportDialogProps {
  title: string
  step1: string
  templateUrl: string
  templateFileName: string
  /** POST endpoint; receives the file and <c>updateExisting</c>. */
  importUrl: string
  updateExistingLabel: string
  updateExistingHint: string
  /** Translates a column key from the server (e.g. "FullName") to a header. */
  columnLabel: (column: string) => string
  onImported: () => void
  onClose: () => void
}

/** Mount only while open, so every opening starts clean. */
export function ImportDialog(props: ImportDialogProps) {
  const { title, step1, templateUrl, templateFileName, importUrl, columnLabel, onImported, onClose } = props
  const { t } = useTranslation()
  const toast = useToast()
  const fileInput = useRef<HTMLInputElement>(null)

  const [file, setFile] = useState<File | null>(null)
  const [fileError, setFileError] = useState<string | null>(null)
  const [updateExisting, setUpdateExisting] = useState(false)
  const [result, setResult] = useState<ImportResult | null>(null)
  const [downloading, setDownloading] = useState(false)

  const upload = useMutation({
    mutationFn: async () => {
      const form = new FormData()
      form.append('file', file!)
      return (await api.post<ImportResult>(importUrl, form, { params: { updateExisting } })).data
    },
  })

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
      await downloadFile(templateUrl, templateFileName)
    } catch (err) {
      toast.error(getErrorMessage(err, t))
    } finally {
      setDownloading(false)
    }
  }

  async function handleImport() {
    if (!file) return
    try {
      const imported = await upload.mutateAsync()
      setResult(imported)
      if (imported.succeeded) onImported()
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
      title={title}
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
            <Button variant="primary" onClick={handleImport} disabled={!file || upload.isPending}>
              {upload.isPending ? t('import.submitting') : t('import.submit')}
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
            <div className="mb-1.5 font-medium text-zinc-800">1. {step1}</div>
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
              <span className="text-zinc-800">{props.updateExistingLabel}</span>
              <span className="block text-xs text-zinc-500">{props.updateExistingHint}</span>
            </span>
          </label>

          <p className="text-xs text-zinc-500">{t('import.allOrNothing')}</p>

          {result && !result.succeeded && <ImportErrorTable errors={result.errors} columnLabel={columnLabel} />}
        </div>
      )}
    </Dialog>
  )
}
