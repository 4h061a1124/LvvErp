import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Link, useLocation, useNavigate, useParams, useSearchParams } from 'react-router'
import { Button } from '@/components/ui/Button'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { Field } from '@/components/ui/Field'
import { PageHeader } from '@/components/ui/PageHeader'
import { useToast } from '@/components/ui/toast-context'
import { useAuth } from '@/lib/auth-context'
import { getErrorCode, getErrorMessage, getProblem } from '@/lib/errors'
import { formatDate, formatDateTime, formatQuantity } from '@/lib/format'
import { Permissions } from '@/lib/permissions'
import { useOrder } from '@/features/purchasing/api'
import { useWarehouses } from '../api'
import type { LabelItem } from '../labels/labels'
import { useDocument, useDocumentAction, useSaveDocument } from './api'
import { documentSchema, documentToValues, newDocumentValues, receiptFromOrder, valuesToInput, type DocumentFormValues } from './documentModel'
import { DocumentForm } from './DocumentForm'
import { DocumentStatusBadge } from './DocumentStatusBadge'
import { DOCUMENT_PATHS, type DocumentType, type OverReceiptLine, type StockDocument, type StockShortage } from './types'

/** /inventory/{receipts|issues|transfers}/{id|new} */
export function DocumentPage({ type }: { type: DocumentType }) {
  const { t, i18n } = useTranslation()
  const { id } = useParams()
  const isNew = id === 'new'
  const document = useDocument(type, isNew ? undefined : id)
  // /inventory/receipts/new?order={id}: receive what is still due on a purchase order.
  const [params] = useSearchParams()
  const orderId = isNew && type === 'Receipt' ? (params.get('order') ?? undefined) : undefined
  const order = useOrder(orderId)
  const warehouses = useWarehouses(true)
  const canEdit = useAuth().can(Permissions.InventoryEditDocuments)

  if (!isNew && document.isError) {
    return <p className="py-10 text-center text-sm text-red-700">{getErrorMessage(document.error, t)}</p>
  }
  // Selects need their options before the form mounts (see CLAUDE.md).
  if (orderId && order.isError) {
    return <p className="py-10 text-center text-sm text-red-700">{getErrorMessage(order.error, t)}</p>
  }
  if ((!isNew && !document.data) || !warehouses.data || (orderId && !order.data)) {
    return <p className="py-10 text-center text-sm text-zinc-500">{t('common.loading')}</p>
  }

  const doc = isNew ? null : document.data!
  if (doc && (doc.status !== 'Draft' || !canEdit)) return <DocumentView key={doc.id} type={type} doc={doc} />
  if (!doc && !canEdit) return <p className="py-10 text-center text-sm text-zinc-500">{t('errors.forbidden')}</p>
  const initial = order.data ? receiptFromOrder(order.data, i18n.language) : undefined
  return <DraftEditor key={doc?.id ?? `new-${orderId ?? ''}`} type={type} doc={doc} initial={initial} />
}

function DraftEditor({ type, doc, initial }: { type: DocumentType; doc: StockDocument | null; initial?: DocumentFormValues }) {
  const { t, i18n } = useTranslation()
  const toast = useToast()
  const navigate = useNavigate()
  const canApprove = useAuth().can(Permissions.InventoryApproveDocuments)
  const save = useSaveDocument(type)
  const actions = useDocumentAction(type)
  const route = DOCUMENT_PATHS[type].route

  const form = useForm<DocumentFormValues>({
    resolver: zodResolver(documentSchema(type)),
    defaultValues: doc ? documentToValues(doc) : (initial ?? newDocumentValues(type)),
  })
  const [confirmApprove, setConfirmApprove] = useState<DocumentFormValues | null>(null)
  const [confirmDelete, setConfirmDelete] = useState(false)
  // A new document is saved before approving; a failed approval then moves to the document's URL
  // (remounting this editor), so the shortage list travels in the navigation state.
  const location = useLocation()
  const [shortages, setShortages] = useState<StockShortage[]>(
    () => (location.state as { shortages?: StockShortage[] } | null)?.shortages ?? [],
  )
  const [overReceipt, setOverReceipt] = useState<OverReceiptLine[]>(
    () => (location.state as { overReceipt?: OverReceiptLine[] } | null)?.overReceipt ?? [],
  )
  const [busy, setBusy] = useState(false)

  async function persist(values: DocumentFormValues): Promise<StockDocument | null> {
    try {
      const saved = await save.mutateAsync({ id: doc?.id, input: valuesToInput(type, values) })
      form.reset(documentToValues(saved))
      return saved
    } catch (err) {
      toast.error(getErrorMessage(err, t))
      return null
    }
  }

  const onSave = form.handleSubmit(async (values) => {
    setBusy(true)
    const saved = await persist(values)
    setBusy(false)
    if (!saved) return
    setShortages([])
    setOverReceipt([])
    toast.success(t('inventory.documents.saved', { number: saved.number }))
    if (!doc) navigate(`${route}/${saved.id}`, { replace: true })
  })

  async function approve(values: DocumentFormValues) {
    setBusy(true)
    const saved = !doc || form.formState.isDirty ? await persist(values) : doc
    if (!saved) {
      setBusy(false)
      return
    }
    try {
      await actions.approve.mutateAsync(saved.id)
      toast.success(t('inventory.documents.approved', { number: saved.number }))
      if (!doc) navigate(`${route}/${saved.id}`, { replace: true })
    } catch (err) {
      const code = getErrorCode(err)
      const missing = code === 'InsufficientStock' ? ((getProblem(err)?.details as StockShortage[]) ?? []) : []
      const over = code === 'OverReceipt' ? ((getProblem(err)?.details as OverReceiptLine[]) ?? []) : []
      if (!missing.length && !over.length) toast.error(getErrorMessage(err, t))
      if (doc) {
        setShortages(missing)
        setOverReceipt(over)
      } else navigate(`${route}/${saved.id}`, { replace: true, state: { shortages: missing, overReceipt: over } })
    }
    setBusy(false)
  }

  async function remove() {
    if (!doc) return
    try {
      await actions.remove.mutateAsync(doc.id)
      toast.success(t('inventory.documents.deleted', { number: doc.number }))
      navigate(route, { replace: true })
    } catch (err) {
      toast.error(getErrorMessage(err, t))
    }
    setConfirmDelete(false)
  }

  const dirty = !doc || form.formState.isDirty

  return (
    <form onSubmit={onSave} noValidate>
      <PageHeader
        title={doc ? doc.number : t(`inventory.documents.newTitle.${type}`)}
        meta={
          <span className="flex items-center gap-3">
            <Link to={route} className="text-brand-700 hover:underline">
              ← {t(`inventory.documents.title.${type}`)}
            </Link>
            {doc && <DocumentStatusBadge status={doc.status} />}
          </span>
        }
        actions={
          <>
            {doc && (
              <Button variant="dangerGhost" onClick={() => setConfirmDelete(true)} disabled={busy}>
                {t('common.delete')}
              </Button>
            )}
            <Button type="submit" disabled={busy}>
              {busy && save.isPending ? t('common.saving') : t('inventory.documents.saveDraft')}
            </Button>
            {canApprove && (
              <Button variant="primary" disabled={busy} onClick={form.handleSubmit((values) => setConfirmApprove(values))}>
                {dirty ? t('inventory.documents.saveAndApprove') : t('inventory.documents.approve')}
              </Button>
            )}
          </>
        }
      />

      {shortages.length > 0 && (
        <div role="alert" className="mb-4 border-l-[3px] border-red-600 bg-red-50 px-3 py-2 text-[13px] text-red-800">
          <div className="font-medium">{t('inventory.documents.shortageTitle')}</div>
          <ul className="mt-1 list-inside list-disc">
            {shortages.map((s, i) => (
              <li key={i}>
                {t('inventory.documents.shortageLine', {
                  line: s.lineNo,
                  code: s.materialCode,
                  lot: s.lotNumber ? ` · ${t('inventory.documents.lot')} ${s.lotNumber}` : '',
                  requested: formatQuantity(s.requested, i18n.language),
                  available: formatQuantity(s.available, i18n.language),
                })}
              </li>
            ))}
          </ul>
        </div>
      )}

      {overReceipt.length > 0 && (
        <div role="alert" className="mb-4 border-l-[3px] border-red-600 bg-red-50 px-3 py-2 text-[13px] text-red-800">
          <div className="font-medium">{t('errors.codes.OverReceipt')}</div>
          <ul className="mt-1 list-inside list-disc">
            {overReceipt.map((o) => (
              <li key={o.lineNo}>
                {t('inventory.documents.overReceiptLine', {
                  line: o.lineNo,
                  code: o.materialCode,
                  ordered: formatQuantity(o.ordered, i18n.language),
                  received: formatQuantity(o.received, i18n.language),
                  receiving: formatQuantity(o.receiving, i18n.language),
                  allowed: formatQuantity(o.allowed, i18n.language),
                })}
              </li>
            ))}
          </ul>
        </div>
      )}

      <DocumentForm type={type} form={form} />

      <ConfirmDialog
        open={!!confirmApprove}
        title={t('inventory.documents.approveTitle')}
        confirmLabel={t('inventory.documents.approve')}
        busy={busy}
        onConfirm={() => {
          const values = confirmApprove!
          setConfirmApprove(null)
          void approve(values)
        }}
        onClose={() => setConfirmApprove(null)}
      >
        <p>{t('inventory.documents.approveMessage', { number: doc?.number ?? t(`inventory.documents.newTitle.${type}`) })}</p>
      </ConfirmDialog>
      <ConfirmDialog
        open={confirmDelete}
        title={t('inventory.documents.deleteTitle')}
        confirmLabel={t('common.delete')}
        busy={actions.remove.isPending}
        onConfirm={remove}
        onClose={() => setConfirmDelete(false)}
      >
        <p>{doc && t('inventory.documents.deleteMessage', { number: doc.number })}</p>
      </ConfirmDialog>
    </form>
  )
}

function DocumentView({ type, doc }: { type: DocumentType; doc: StockDocument }) {
  const { t, i18n } = useTranslation()
  const toast = useToast()
  const navigate = useNavigate()
  const canCancel = useAuth().can(Permissions.InventoryCancelDocuments)
  const actions = useDocumentAction(type)
  const [cancelling, setCancelling] = useState(false)
  const [reason, setReason] = useState('')
  const [reasonError, setReasonError] = useState(false)
  const [shortages, setShortages] = useState<StockShortage[]>([])
  const route = DOCUMENT_PATHS[type].route

  async function cancel() {
    if (!reason.trim()) return setReasonError(true)
    try {
      await actions.cancel.mutateAsync({ id: doc.id, reason: reason.trim() })
      toast.success(t('inventory.documents.cancelled', { number: doc.number }))
      setCancelling(false)
    } catch (err) {
      setCancelling(false)
      if (getErrorCode(err) === 'InsufficientStock') setShortages((getProblem(err)?.details as StockShortage[]) ?? [])
      else toast.error(getErrorMessage(err, t))
    }
  }

  function printLabels() {
    const items: LabelItem[] = doc.lines.map((l) => ({
      key: l.id,
      code: l.materialCode,
      name: l.materialName,
      specification: null,
      unitName: l.unitName,
      lotNumber: l.lotNumber,
      expiryDate: l.expiryDate,
      copies: 1,
    }))
    navigate('/inventory/labels', { state: { items } })
  }

  const info: [string, string | null][] = [
    [t('inventory.documents.date'), formatDate(doc.documentDate)],
    [
      type === 'Transfer' ? t('inventory.documents.fromWarehouse') : t('inventory.documents.warehouse'),
      doc.warehouseName,
    ],
    ...(type === 'Transfer' ? ([[t('inventory.documents.toWarehouse'), doc.toWarehouseName]] as [string, string | null][]) : []),
    ...(doc.reason ? ([[t('inventory.documents.reason'), t(`inventory.documents.reasons.${doc.reason}`)]] as [string, string][]) : []),
    ...(type === 'Receipt' ? ([[t('inventory.documents.partner'), doc.partner]] as [string, string | null][]) : []),
    ...(doc.purchaseOrderId ? ([[t('inventory.documents.purchaseOrder'), doc.purchaseOrderNumber]] as [string, string | null][]) : []),
    ...(type === 'Issue'
      ? ([
          [t('inventory.documents.employee'), doc.employeeName],
          [t('inventory.documents.department'), doc.departmentName],
        ] as [string, string | null][])
      : []),
    [t('inventory.documents.createdBy'), [doc.createdBy, formatDateTime(doc.createdAt, i18n.language)].filter(Boolean).join(' · ')],
    ...(doc.approvedAt
      ? ([[t('inventory.documents.approvedBy'), `${doc.approvedBy ?? ''} · ${formatDateTime(doc.approvedAt, i18n.language)}`]] as [string, string][])
      : []),
    ...(doc.cancelledAt
      ? ([
          [t('inventory.documents.cancelledBy'), `${doc.cancelledBy ?? ''} · ${formatDateTime(doc.cancelledAt, i18n.language)}`],
          [t('inventory.documents.cancelReason'), doc.cancelReason],
        ] as [string, string | null][])
      : []),
    [t('inventory.documents.note'), doc.note],
  ]

  return (
    <>
      <PageHeader
        title={doc.number}
        meta={
          <span className="flex items-center gap-3">
            <Link to={route} className="text-brand-700 hover:underline">
              ← {t(`inventory.documents.title.${type}`)}
            </Link>
            <DocumentStatusBadge status={doc.status} />
            {doc.purchaseOrderId && (
              <Link to={`/purchasing/orders/${doc.purchaseOrderId}`} className="tabular text-brand-700 hover:underline">
                {t('inventory.documents.purchaseOrder')} {doc.purchaseOrderNumber}
              </Link>
            )}
          </span>
        }
        actions={
          <>
            {type === 'Receipt' && doc.status === 'Approved' && <Button onClick={printLabels}>{t('inventory.documents.printLabels')}</Button>}
            {canCancel && doc.status === 'Approved' && (
              <Button variant="dangerGhost" onClick={() => setCancelling(true)}>
                {t('inventory.documents.cancelDocument')}
              </Button>
            )}
          </>
        }
      />

      {shortages.length > 0 && (
        <div role="alert" className="mb-4 border-l-[3px] border-red-600 bg-red-50 px-3 py-2 text-[13px] text-red-800">
          <div className="font-medium">{t('errors.codes.InsufficientStock')}</div>
          <ul className="mt-1 list-inside list-disc">
            {shortages.map((s, i) => (
              <li key={i}>
                {t('inventory.documents.shortageLine', {
                  line: s.lineNo,
                  code: s.materialCode,
                  lot: s.lotNumber ? ` · ${t('inventory.documents.lot')} ${s.lotNumber}` : '',
                  requested: formatQuantity(s.requested, i18n.language),
                  available: formatQuantity(s.available, i18n.language),
                })}
              </li>
            ))}
          </ul>
        </div>
      )}

      <dl className="mb-4 grid gap-x-6 gap-y-2 rounded-[4px] border border-zinc-200 bg-white p-4 text-sm sm:grid-cols-2 lg:grid-cols-3">
        {info
          .filter(([, value]) => value)
          .map(([label, value]) => (
            <div key={label} className="flex gap-2">
              <dt className="w-28 shrink-0 text-zinc-500">{label}</dt>
              <dd className="min-w-0 text-zinc-900">{value}</dd>
            </div>
          ))}
      </dl>

      <section className="rounded-[4px] border border-zinc-200 bg-white">
        <div className="border-b border-zinc-200 px-3 py-2 text-sm font-semibold text-zinc-800">{t('inventory.documents.lines')}</div>
        <div className="overflow-x-auto">
          <table className="w-full text-left">
            <thead>
              <tr className="border-b border-zinc-200 bg-zinc-50 text-[13px] text-zinc-600">
                <th className="w-10 px-3 py-2 font-medium">#</th>
                <th className="px-3 py-2 font-medium">{t('inventory.documents.material')}</th>
                <th className="px-3 py-2 font-medium">{t('inventory.documents.location')}</th>
                <th className="px-3 py-2 font-medium">{t('inventory.documents.lot')}</th>
                <th className="w-32 px-3 py-2 text-right font-medium">{t('inventory.documents.quantity')}</th>
              </tr>
            </thead>
            <tbody>
              {doc.lines.map((l) => (
                <tr key={l.id} className="border-b border-zinc-100 last:border-b-0">
                  <td className="tabular px-3 py-2 text-[13px] text-zinc-500">{l.lineNo}</td>
                  <td className="px-3 py-2">
                    <span className="tabular mr-2 text-[13px] text-zinc-500">{l.materialCode}</span>
                    <span className="text-zinc-900">{l.materialName}</span>
                  </td>
                  <td className="tabular px-3 py-2 text-[13px] text-zinc-700">
                    {type === 'Transfer'
                      ? `${l.locationCode ?? '—'} → ${l.toLocationCode ?? '—'}`
                      : (l.locationCode ?? <span className="text-zinc-400">—</span>)}
                  </td>
                  <td className="px-3 py-2 text-[13px] text-zinc-700">
                    {l.lotNumber ? (
                      <>
                        {l.lotNumber}
                        {l.expiryDate && (
                          <span className="ml-2 text-zinc-500">
                            {t('inventory.documents.expiryDate')} {formatDate(l.expiryDate)}
                          </span>
                        )}
                      </>
                    ) : (
                      <span className="text-zinc-400">—</span>
                    )}
                  </td>
                  <td className="tabular px-3 py-2 text-right text-zinc-900">
                    {formatQuantity(l.quantity, i18n.language)} <span className="text-xs text-zinc-500">{l.unitName}</span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <ConfirmDialog
        open={cancelling}
        title={t('inventory.documents.cancelTitle')}
        confirmLabel={t('inventory.documents.cancelDocument')}
        busy={actions.cancel.isPending}
        onConfirm={cancel}
        onClose={() => setCancelling(false)}
      >
        <p>{t('inventory.documents.cancelMessage', { number: doc.number })}</p>
        <Field
          label={t('inventory.documents.cancelReason')}
          htmlFor="cancelReason"
          required
          error={reasonError ? t('inventory.documents.cancelReasonRequired') : undefined}
        >
          <textarea
            id="cancelReason"
            rows={3}
            maxLength={500}
            value={reason}
            onChange={(e) => {
              setReason(e.target.value)
              setReasonError(false)
            }}
            className="block w-full rounded-[3px] border border-zinc-300 px-2.5 py-1.5 text-sm focus:border-brand-500 focus:ring-2 focus:ring-brand-500/15 focus:outline-none"
          />
        </Field>
      </ConfirmDialog>
    </>
  )
}
