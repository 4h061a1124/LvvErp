import { zodResolver } from '@hookform/resolvers/zod'
import { useQuery } from '@tanstack/react-query'
import { X } from 'lucide-react'
import { useMemo, useState } from 'react'
import { Controller, useFieldArray, useForm, useWatch, type UseFormReturn } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams } from 'react-router'
import { Button } from '@/components/ui/Button'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { Field, Input, Select } from '@/components/ui/Field'
import { PageHeader } from '@/components/ui/PageHeader'
import { useToast } from '@/components/ui/toast-context'
import { useWarehouses } from '@/features/inventory/api'
import { DocumentStatusBadge } from '@/features/inventory/documents/DocumentStatusBadge'
import { useAuth } from '@/lib/auth-context'
import { getErrorMessage, getFieldErrors } from '@/lib/errors'
import { formatDate, formatDateTime, formatMoney, formatQuantity, parseQuantity } from '@/lib/format'
import { Permissions } from '@/lib/permissions'
import { fetchLastPrices, useOrder, useOrderAction, useOrderableLines, usePurchasingSettings, useSaveOrder, type OrderAction } from '../api'
import { ReasonDialog } from '../components/ReasonDialog'
import { OrderStatusBadge } from '../components/StatusBadges'
import { SupplierPicker } from '../components/SupplierPicker'
import { VAT_RATES, type PurchaseOrder, type VatRate } from '../types'
import {
  VAT_PERCENT,
  addRequestLines,
  formatPriceInput,
  isOverOrdered,
  lineAmount,
  newOrderValues,
  orderSchema,
  orderToValues,
  orderTotals,
  parsePrice,
  requested,
  valuesToOrderInput,
  type OrderFormValues,
} from './orderModel'
import { PickRequestLinesDialog } from './PickRequestLinesDialog'

const LIST = '/purchasing/orders'

/** /purchasing/orders/{id|new} */
export function OrderPage() {
  const { t } = useTranslation()
  const { id } = useParams()
  const isNew = id === 'new'
  const order = useOrder(isNew ? undefined : id)
  const warehouses = useWarehouses(true)
  const canManage = useAuth().can(Permissions.PurchasingManageOrders)
  const editing = isNew || !!order.data?.actions.canEdit
  // Editing an order needs what its request lines can still give beyond what the order holds.
  const orderable = useOrderableLines('', editing && !isNew)

  if (!isNew && order.isError) return <p className="py-10 text-center text-sm text-red-700">{getErrorMessage(order.error, t)}</p>
  if ((!isNew && !order.data) || !warehouses.data || (editing && !isNew && !orderable.data && !orderable.isError))
    return <p className="py-10 text-center text-sm text-zinc-500">{t('common.loading')}</p>
  if (isNew && !canManage) return <p className="py-10 text-center text-sm text-zinc-500">{t('errors.forbidden')}</p>

  if (!editing) return <OrderView key={order.data!.id} order={order.data!} />
  const remaining = new Map((orderable.data?.items ?? []).map((l) => [l.requestLineId, l.remainingQuantity]))
  return <OrderEditor key={order.data?.id ?? 'new'} order={order.data ?? null} remaining={remaining} />
}

function BackLink() {
  const { t } = useTranslation()
  return (
    <Link to={LIST} className="text-brand-700 hover:underline">
      ← {t('purchasing.orders.title')}
    </Link>
  )
}

function OrderEditor({ order, remaining }: { order: PurchaseOrder | null; remaining: Map<string, number> }) {
  const { t, i18n } = useTranslation()
  const toast = useToast()
  const navigate = useNavigate()
  const save = useSaveOrder()
  const actions = useOrderAction()
  const warehouses = useWarehouses(true)
  const [busy, setBusy] = useState(false)
  const [picking, setPicking] = useState(false)
  const [confirmDelete, setConfirmDelete] = useState(false)

  const form = useForm<OrderFormValues>({
    resolver: zodResolver(orderSchema(i18n.language)),
    defaultValues: order ? orderToValues(order, i18n.language, remaining) : newOrderValues(),
  })
  const { register, control, setValue, getValues, formState: { errors } } = form
  const lines = useFieldArray({ control, name: 'lines', keyName: 'fieldKey' })
  const message = (key?: string) => (key ? t(key as never) : undefined)

  async function persist(values: OrderFormValues): Promise<PurchaseOrder | null> {
    try {
      const saved = await save.mutateAsync({ id: order?.id, input: valuesToOrderInput(values, i18n.language) })
      form.reset(orderToValues(saved, i18n.language, remaining))
      return saved
    } catch (err) {
      const fields = Object.entries(getFieldErrors(err))
      const path = (key: string) =>
        key
          .replace(/\[(\d+)\]/g, '.$1')
          .replace(/^supplierId$/, 'supplier')
          .replace(/\.sourceLineIds\.\d+$/, '.quantity')
      const known = fields.filter(([key]) => /^(supplierId|warehouseId|orderDate|expectedDate|paymentTermDays|note|lines)/.test(key))
      for (const [key, msg] of known) form.setError(path(key) as never, { message: msg })
      if (!known.length) toast.error(getErrorMessage(err, t))
      return null
    }
  }

  const onSave = form.handleSubmit(async (values) => {
    setBusy(true)
    const saved = await persist(values)
    setBusy(false)
    if (!saved) return
    toast.success(t('purchasing.orders.saved', { number: saved.number }))
    if (!order) navigate(`${LIST}/${saved.id}`, { replace: true })
  })

  const onSubmitForApproval = form.handleSubmit(async (values) => {
    setBusy(true)
    const saved = !order || form.formState.isDirty ? await persist(values) : order
    if (saved) {
      try {
        await actions.run.mutateAsync({ id: saved.id, action: 'submit' })
        toast.success(t('purchasing.orders.submitted', { number: saved.number }))
      } catch (err) {
        toast.error(getErrorMessage(err, t))
      }
      if (!order) navigate(`${LIST}/${saved.id}`, { replace: true })
    }
    setBusy(false)
  })

  async function remove() {
    if (!order) return
    try {
      await actions.remove.mutateAsync(order.id)
      toast.success(t('purchasing.orders.deleted', { number: order.number }))
      navigate(LIST, { replace: true })
    } catch (err) {
      toast.error(getErrorMessage(err, t))
    }
    setConfirmDelete(false)
  }

  const used = new Set(useWatch({ control, name: 'lines' }).flatMap((l) => l.sources.map((s) => s.requestLineId)))
  const dirty = !order || form.formState.isDirty

  return (
    <form onSubmit={onSave} noValidate>
      <PageHeader
        title={order ? order.number : t('purchasing.orders.newTitle')}
        meta={
          <span className="flex items-center gap-3">
            <BackLink />
            {order && <OrderStatusBadge status={order.status} />}
          </span>
        }
        actions={
          <>
            {order?.actions.canDelete && (
              <Button variant="dangerGhost" onClick={() => setConfirmDelete(true)} disabled={busy}>
                {t('common.delete')}
              </Button>
            )}
            <Button type="submit" disabled={busy}>
              {busy && save.isPending ? t('common.saving') : t('purchasing.orders.saveDraft')}
            </Button>
            <Button variant="primary" disabled={busy} onClick={onSubmitForApproval}>
              {dirty ? t('purchasing.orders.saveAndSubmit') : t('purchasing.orders.submit')}
            </Button>
          </>
        }
      />

      {order?.status === 'Rejected' && order.rejectReason && (
        <div role="alert" className="mb-4 border-l-[3px] border-red-600 bg-red-50 px-3 py-2 text-[13px] text-red-800">
          {t('purchasing.requests.rejectedNotice', { user: order.rejectedBy ?? '', reason: order.rejectReason })}
        </div>
      )}

      <div className="space-y-4">
        <section className="grid gap-x-4 gap-y-3 rounded-[4px] border border-zinc-200 bg-white p-4 sm:grid-cols-2 lg:grid-cols-4">
          <Field label={t('purchasing.orders.supplier')} htmlFor="supplier" required className="sm:col-span-2" error={message(errors.supplier?.message)}>
            <Controller
              control={control}
              name="supplier"
              render={({ field }) => (
                <SupplierPicker
                  id="supplier"
                  value={field.value}
                  invalid={!!errors.supplier}
                  onChange={(supplier) => {
                    field.onChange(supplier)
                    if (supplier) setValue('paymentTermDays', String(supplier.paymentTermDays), { shouldDirty: true })
                  }}
                />
              )}
            />
          </Field>
          <Field label={t('purchasing.orders.warehouse')} htmlFor="warehouseId" required error={message(errors.warehouseId?.message)}>
            <Select id="warehouseId" invalid={!!errors.warehouseId} {...register('warehouseId')}>
              <option value="">{t('common.choose')}</option>
              {warehouses.data?.map((w) => (
                <option key={w.id} value={w.id}>
                  {w.name}
                </option>
              ))}
            </Select>
          </Field>
          <Field label={t('purchasing.orders.vatRate')} htmlFor="vatRate">
            <Select id="vatRate" {...register('vatRate')}>
              {VAT_RATES.map((r) => (
                <option key={r} value={r}>
                  {t(`purchasing.orders.vat.${r}`)}
                </option>
              ))}
            </Select>
          </Field>
          <Field label={t('purchasing.orders.orderDate')} htmlFor="orderDate" required error={message(errors.orderDate?.message)}>
            <Input id="orderDate" type="date" invalid={!!errors.orderDate} {...register('orderDate')} />
          </Field>
          <Field label={t('purchasing.orders.expectedDate')} htmlFor="expectedDate" error={message(errors.expectedDate?.message)}>
            <Input id="expectedDate" type="date" invalid={!!errors.expectedDate} {...register('expectedDate')} />
          </Field>
          <Field label={t('purchasing.orders.paymentTermDays')} htmlFor="paymentTermDays" error={message(errors.paymentTermDays?.message)}>
            <Input id="paymentTermDays" inputMode="numeric" autoComplete="off" invalid={!!errors.paymentTermDays} {...register('paymentTermDays')} />
          </Field>
          <Field label={t('purchasing.orders.note')} htmlFor="note" error={message(errors.note?.message)}>
            <Input id="note" autoComplete="off" {...register('note')} />
          </Field>
        </section>

        <section className="rounded-[4px] border border-zinc-200 bg-white">
          <div className="flex items-center justify-between border-b border-zinc-200 px-3 py-2">
            <h2 className="text-sm font-semibold text-zinc-800">{t('purchasing.orders.lines')}</h2>
            <Button size="sm" onClick={() => setPicking(true)}>
              {t('purchasing.orders.addFromRequests')}
            </Button>
          </div>
          {lines.fields.length === 0 ? (
            <p className={errors.lines?.message ? 'px-3 py-6 text-center text-sm text-red-600' : 'px-3 py-6 text-center text-sm text-zinc-500'}>
              {message(errors.lines?.message) ?? t('purchasing.orders.noLines')}
            </p>
          ) : (
            <>
              <div className="hidden gap-2 border-b border-zinc-100 bg-zinc-50 px-3 py-1.5 text-xs font-medium text-zinc-600 md:grid md:grid-cols-[2rem_minmax(0,4fr)_minmax(0,2fr)_minmax(0,2fr)_minmax(0,2fr)_2rem]">
                <span>#</span>
                <span>{t('purchasing.requests.material')}</span>
                <span>{t('purchasing.requests.quantity')}</span>
                <span>{t('purchasing.orders.unitPrice')}</span>
                <span className="text-right">{t('purchasing.orders.amount')}</span>
                <span />
              </div>
              <ol className="divide-y divide-zinc-100">
                {lines.fields.map((field, index) => (
                  <OrderLineRow key={field.fieldKey} form={form} index={index} onRemove={() => lines.remove(index)} />
                ))}
              </ol>
            </>
          )}
          <Totals form={form} />
        </section>
      </div>

      {picking && (
        <PickRequestLinesDialog
          usedLineIds={used}
          onClose={() => setPicking(false)}
          onPick={(picked) => {
            const next = addRequestLines(getValues('lines'), picked)
            setValue('lines', next, { shouldDirty: true })
            setPicking(false)
          }}
        />
      )}
      <ConfirmDialog
        open={confirmDelete}
        title={t('purchasing.orders.deleteTitle')}
        confirmLabel={t('common.delete')}
        busy={actions.remove.isPending}
        onConfirm={remove}
        onClose={() => setConfirmDelete(false)}
      >
        <p>{order && t('purchasing.orders.deleteMessage', { number: order.number })}</p>
      </ConfirmDialog>
    </form>
  )
}

function OrderLineRow({ form, index, onRemove }: { form: UseFormReturn<OrderFormValues>; index: number; onRemove: () => void }) {
  const { t, i18n } = useTranslation()
  const { register, control, setValue, formState: { errors } } = form
  const line = useWatch({ control, name: `lines.${index}` })
  const supplier = useWatch({ control, name: 'supplier' })
  const lineErrors = errors.lines?.[index]
  const message = (key?: string) => (key ? t(key as never) : undefined)

  const lastPrice = useQuery({
    queryKey: ['purchasing', 'last-price', line.material.id, supplier?.id ?? ''],
    queryFn: async () => (await fetchLastPrices([line.material.id], supplier?.id))[0] ?? null,
    staleTime: 60_000,
  })
  const amount = lineAmount(parseQuantity(line.quantity), parsePrice(line.unitPrice, i18n.language))
  const over = isOverOrdered(line)

  return (
    <li className="relative grid gap-2 px-3 py-2.5 md:grid-cols-[2rem_minmax(0,4fr)_minmax(0,2fr)_minmax(0,2fr)_minmax(0,2fr)_2rem] md:items-start">
      <span className="tabular pt-1.5 text-[13px] text-zinc-500">{index + 1}</span>
      <div className="pt-1">
        <div className="text-sm">
          <span className="tabular mr-1.5 text-[13px] text-zinc-500">{line.material.code}</span>
          <span className="text-zinc-900">{line.material.name}</span>
        </div>
        <div className="text-xs text-zinc-500">
          {line.sources.map((s) => `${s.requestNumber}/${s.requestLineNo}`).join(', ')}
        </div>
      </div>
      <div>
        <div className="flex items-center gap-1.5">
          <Input
            inputMode="decimal"
            autoComplete="off"
            aria-label={t('purchasing.requests.quantity')}
            invalid={!!lineErrors?.quantity}
            className="text-right"
            {...register(`lines.${index}.quantity`)}
          />
          <span className="w-10 shrink-0 truncate text-xs text-zinc-500">{line.material.unitName}</span>
        </div>
        {lineErrors?.quantity ? (
          <p className="mt-1 text-xs text-red-600">{message(lineErrors.quantity.message)}</p>
        ) : (
          <p className={over ? 'mt-1 text-xs text-amber-700' : 'mt-1 text-xs text-zinc-500'}>
            {t('purchasing.orders.requested', { quantity: formatQuantity(requested(line), i18n.language) })}
          </p>
        )}
      </div>
      <div>
        <Input
          inputMode="decimal"
          autoComplete="off"
          aria-label={t('purchasing.orders.unitPrice')}
          invalid={!!lineErrors?.unitPrice}
          className="text-right"
          {...register(`lines.${index}.unitPrice`, {
            // Regroup on leaving the box: "85000" -> "85.000".
            onBlur: (e) => {
              const price = parsePrice(e.target.value, i18n.language)
              if (price !== null) setValue(`lines.${index}.unitPrice`, formatPriceInput(price, i18n.language))
            },
          })}
        />
        {lineErrors?.unitPrice ? (
          <p className="mt-1 text-xs text-red-600">{message(lineErrors.unitPrice.message)}</p>
        ) : (
          lastPrice.data && (
            <button
              type="button"
              title={t('purchasing.orders.lastPriceTitle', { supplier: lastPrice.data.supplierName, number: lastPrice.data.orderNumber, date: formatDate(lastPrice.data.orderDate) })}
              onClick={() => setValue(`lines.${index}.unitPrice`, formatPriceInput(lastPrice.data!.unitPrice, i18n.language), { shouldDirty: true, shouldValidate: true })}
              className="mt-1 text-left text-xs text-brand-700 hover:underline"
            >
              {t(lastPrice.data.sameSupplier ? 'purchasing.orders.lastPrice' : 'purchasing.orders.lastPriceOther', {
                price: formatMoney(lastPrice.data.unitPrice, i18n.language, 2),
              })}
            </button>
          )
        )}
      </div>
      <div className="tabular pt-1.5 text-right text-sm text-zinc-900">{formatMoney(amount, i18n.language)}</div>
      <button
        type="button"
        onClick={onRemove}
        className="absolute top-1.5 right-2 rounded-[3px] p-1.5 text-zinc-400 hover:bg-zinc-100 hover:text-zinc-700 md:static"
        aria-label={t('purchasing.requests.removeLine', { no: index + 1 })}
      >
        <X size={15} />
      </button>
      <div className="grid gap-2 md:col-span-4 md:col-start-2 md:grid-cols-2">
        {over && (
          <div>
            <Input
              autoComplete="off"
              placeholder={t('purchasing.orders.overOrderReason')}
              aria-label={t('purchasing.orders.overOrderReason')}
              invalid={!!lineErrors?.overOrderReason}
              className="border-amber-400"
              {...register(`lines.${index}.overOrderReason`)}
            />
            {lineErrors?.overOrderReason && <p className="mt-1 text-xs text-red-600">{message(lineErrors.overOrderReason.message)}</p>}
          </div>
        )}
        <Input
          autoComplete="off"
          placeholder={t('purchasing.requests.lineNote')}
          aria-label={t('purchasing.requests.lineNote')}
          {...register(`lines.${index}.note`)}
        />
      </div>
    </li>
  )
}

function Totals({ form }: { form: UseFormReturn<OrderFormValues> }) {
  const { t, i18n } = useTranslation()
  const values = useWatch({ control: form.control })
  const settings = usePurchasingSettings()
  const vatRate = (values.vatRate ?? 'Ten') as VatRate
  const totals = useMemo(
    () =>
      orderTotals(
        (values.lines ?? []).map((l) => lineAmount(parseQuantity(l.quantity ?? ''), parsePrice(l.unitPrice ?? '', i18n.language))),
        vatRate,
      ),
    [values.lines, vatRate, i18n.language],
  )
  const threshold = settings.data?.directorApprovalThreshold

  return (
    <div className="flex flex-wrap items-end justify-between gap-3 border-t border-zinc-200 px-3 py-3">
      <p className="text-xs text-zinc-500">
        {threshold !== undefined &&
          t(totals.total >= threshold ? 'purchasing.orders.needsDirector' : 'purchasing.orders.needsHead', {
            threshold: formatMoney(threshold, i18n.language),
          })}
      </p>
      <MoneySummary subtotal={totals.subtotal} vat={totals.vat} total={totals.total} vatRate={vatRate} />
    </div>
  )
}

function MoneySummary({ subtotal, vat, total, vatRate }: { subtotal: number; vat: number; total: number; vatRate: VatRate }) {
  const { t, i18n } = useTranslation()
  return (
    <dl className="tabular grid min-w-64 grid-cols-[1fr_auto] gap-x-6 gap-y-1 text-sm">
      <dt className="text-zinc-500">{t('purchasing.orders.subtotal')}</dt>
      <dd className="text-right text-zinc-900">{formatMoney(subtotal, i18n.language)}</dd>
      <dt className="text-zinc-500">
        {t('purchasing.orders.vatAmount')} ({vatRate === 'Exempt' ? t('purchasing.orders.vat.Exempt') : `${VAT_PERCENT[vatRate]}%`})
      </dt>
      <dd className="text-right text-zinc-900">{formatMoney(vat, i18n.language)}</dd>
      <dt className="border-t border-zinc-200 pt-1 font-medium text-zinc-800">{t('purchasing.orders.total')}</dt>
      <dd className="border-t border-zinc-200 pt-1 text-right font-semibold text-zinc-900">{formatMoney(total, i18n.language)}</dd>
    </dl>
  )
}

type Pending = { action: 'approve' | 'reject' | 'cancel' | 'close' } | null

function OrderView({ order }: { order: PurchaseOrder }) {
  const { t, i18n } = useTranslation()
  const toast = useToast()
  const navigate = useNavigate()
  const canReceive = useAuth().can(Permissions.InventoryEditDocuments)
  const actions = useOrderAction()
  const [pending, setPending] = useState<Pending>(null)
  const a = order.actions
  const receivable = order.status === 'Approved' || order.status === 'PartiallyReceived'
  const showReceived = receivable || order.status === 'Received' || order.status === 'Closed'

  async function run(action: OrderAction, reason?: string) {
    try {
      await actions.run.mutateAsync({ id: order.id, action, reason })
      const done = { submit: 'submitted', approve: 'approved', reject: 'rejected', cancel: 'cancelled', close: 'closed' } as const
      toast.success(t(`purchasing.orders.${done[action]}`, { number: order.number }))
    } catch (err) {
      toast.error(getErrorMessage(err, t))
    }
    setPending(null)
  }

  const when = (by: string | null, at: string | null) => (at ? [by, formatDateTime(at, i18n.language)].filter(Boolean).join(' · ') : null)
  const info: [string, string | null][] = [
    [t('purchasing.orders.supplier'), `${order.supplier.code} – ${order.supplier.name}`],
    [t('purchasing.orders.warehouse'), order.warehouseName],
    [t('purchasing.orders.orderDate'), formatDate(order.orderDate)],
    [t('purchasing.orders.expectedDate'), order.expectedDate ? formatDate(order.expectedDate) + (order.isOverdue ? ` · ${t('purchasing.orders.overdue')}` : '') : null],
    [t('purchasing.orders.paymentTermDays'), t('purchasing.suppliers.days', { count: order.paymentTermDays })],
    [t('purchasing.orders.approvalLevel'), order.approvalLevel && t(`purchasing.orders.level.${order.approvalLevel}`)],
    [t('inventory.documents.createdBy'), when(order.createdBy, order.createdAt)],
    [t('purchasing.requests.submittedAt'), when(order.submittedBy, order.submittedAt)],
    [t('purchasing.requests.approvedBy'), when(order.approvedBy, order.approvedAt)],
    [t('purchasing.requests.closedBy'), when(order.closedBy, order.closedAt)],
    [t('purchasing.requests.cancelledBy'), when(order.cancelledBy, order.cancelledAt)],
    [
      t('purchasing.requests.reason'),
      { Rejected: order.rejectReason, Closed: order.closeReason, Cancelled: order.cancelReason }[order.status as string] ?? null,
    ],
    [t('purchasing.orders.note'), order.note],
  ]

  return (
    <>
      <PageHeader
        title={order.number}
        meta={
          <span className="flex items-center gap-3">
            <BackLink />
            <OrderStatusBadge status={order.status} />
          </span>
        }
        actions={
          <>
            {a.canCancel && (
              <Button variant="dangerGhost" onClick={() => setPending({ action: 'cancel' })}>
                {t('purchasing.orders.cancel')}
              </Button>
            )}
            {a.canClose && <Button onClick={() => setPending({ action: 'close' })}>{t('purchasing.orders.close')}</Button>}
            <Button onClick={() => navigate(`${LIST}/${order.id}/print`)}>{t('purchasing.orders.print')}</Button>
            {receivable && canReceive && (
              <Button variant="primary" onClick={() => navigate(`/inventory/receipts/new?order=${order.id}`)}>
                {t('purchasing.orders.receive')}
              </Button>
            )}
            {a.canSubmit && (
              <Button variant="primary" onClick={() => run('submit')} disabled={actions.run.isPending}>
                {t('purchasing.orders.submit')}
              </Button>
            )}
            {a.canApprove && (
              <>
                <Button variant="dangerGhost" onClick={() => setPending({ action: 'reject' })}>
                  {t('purchasing.requests.reject')}
                </Button>
                <Button variant="primary" onClick={() => setPending({ action: 'approve' })}>
                  {t('purchasing.requests.approve')}
                </Button>
              </>
            )}
          </>
        }
      />

      <dl className="mb-4 grid gap-x-6 gap-y-2 rounded-[4px] border border-zinc-200 bg-white p-4 text-sm sm:grid-cols-2 lg:grid-cols-3">
        {info
          .filter(([, value]) => value)
          .map(([label, value]) => (
            <div key={label} className="flex gap-2">
              <dt className="w-32 shrink-0 text-zinc-500">{label}</dt>
              <dd className="min-w-0 text-zinc-900">{value}</dd>
            </div>
          ))}
      </dl>

      <section className="mb-4 rounded-[4px] border border-zinc-200 bg-white">
        <div className="border-b border-zinc-200 px-3 py-2 text-sm font-semibold text-zinc-800">{t('purchasing.orders.lines')}</div>
        <div className="overflow-x-auto">
          <table className="w-full text-left">
            <thead>
              <tr className="border-b border-zinc-200 bg-zinc-50 text-[13px] text-zinc-600">
                <th className="w-10 px-3 py-2 font-medium">#</th>
                <th className="px-3 py-2 font-medium">{t('purchasing.requests.material')}</th>
                <th className="w-32 px-3 py-2 text-right font-medium">{t('purchasing.requests.quantity')}</th>
                {showReceived && <th className="w-32 px-3 py-2 text-right font-medium">{t('purchasing.orders.received')}</th>}
                <th className="w-32 px-3 py-2 text-right font-medium">{t('purchasing.orders.unitPrice')}</th>
                <th className="w-36 px-3 py-2 text-right font-medium">{t('purchasing.orders.amount')}</th>
              </tr>
            </thead>
            <tbody>
              {order.lines.map((l) => (
                <tr key={l.id} className="border-b border-zinc-100 align-top last:border-b-0">
                  <td className="tabular px-3 py-2 text-[13px] text-zinc-500">{l.lineNo}</td>
                  <td className="px-3 py-2">
                    <span className="tabular mr-2 text-[13px] text-zinc-500">{l.materialCode}</span>
                    <span className="text-zinc-900">{l.materialName}</span>
                    <div className="text-xs text-zinc-500">
                      {l.sources.map((s) => (
                        <Link key={s.requestLineId} to={`/purchasing/requests/${s.requestId}`} className="mr-2 hover:underline">
                          {s.requestNumber}/{s.requestLineNo}
                        </Link>
                      ))}
                      {l.note}
                    </div>
                    {l.overOrderReason && (
                      <div className="text-xs text-amber-700">{t('purchasing.orders.overOrderNotice', { reason: l.overOrderReason })}</div>
                    )}
                  </td>
                  <td className="tabular px-3 py-2 text-right text-zinc-900">
                    {formatQuantity(l.quantity, i18n.language)} <span className="text-xs text-zinc-500">{l.unitName}</span>
                  </td>
                  {showReceived && (
                    <td className="tabular px-3 py-2 text-right text-zinc-700">
                      {formatQuantity(l.receivedQuantity, i18n.language)}
                      {l.receiptTolerancePercent > 0 && (
                        <div className="text-xs text-zinc-500">{t('purchasing.orders.tolerance', { percent: formatQuantity(l.receiptTolerancePercent, i18n.language) })}</div>
                      )}
                    </td>
                  )}
                  <td className="tabular px-3 py-2 text-right text-zinc-700">{formatMoney(l.unitPrice, i18n.language, 2)}</td>
                  <td className="tabular px-3 py-2 text-right text-zinc-900">{formatMoney(l.amount, i18n.language)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <div className="flex justify-end border-t border-zinc-200 px-3 py-3">
          <MoneySummary subtotal={order.subtotal} vat={order.vatAmount} total={order.total} vatRate={order.vatRate} />
        </div>
      </section>

      {order.receipts.length > 0 && (
        <section className="rounded-[4px] border border-zinc-200 bg-white">
          <div className="border-b border-zinc-200 px-3 py-2 text-sm font-semibold text-zinc-800">{t('purchasing.orders.receipts')}</div>
          <ul className="divide-y divide-zinc-100">
            {order.receipts.map((r) => (
              <li key={r.id} className="flex items-center justify-between gap-3 px-3 py-2">
                <Link to={`/inventory/receipts/${r.id}`} className="tabular text-[13px] font-medium text-brand-700 hover:underline">
                  {r.number}
                </Link>
                <span className="tabular text-[13px] text-zinc-600">{formatDate(r.documentDate)}</span>
                <DocumentStatusBadge status={r.status} />
              </li>
            ))}
          </ul>
        </section>
      )}

      <ConfirmDialog
        open={pending?.action === 'approve'}
        title={t('purchasing.orders.approveTitle')}
        confirmLabel={t('purchasing.requests.approve')}
        busy={actions.run.isPending}
        onConfirm={() => run('approve')}
        onClose={() => setPending(null)}
      >
        <p>{t('purchasing.orders.approveMessage', { number: order.number, total: formatMoney(order.total, i18n.language) })}</p>
        {order.lines.some((l) => l.overOrderReason) && <p className="text-[13px] text-amber-700">{t('purchasing.orders.approveOverOrder')}</p>}
      </ConfirmDialog>
      {pending && pending.action !== 'approve' && (
        <ReasonDialog
          title={t(`purchasing.orders.${pending.action}Title`)}
          confirmLabel={t(pending.action === 'reject' ? 'purchasing.requests.reject' : `purchasing.orders.${pending.action}`)}
          danger={pending.action !== 'close'}
          busy={actions.run.isPending}
          onConfirm={(reason) => run(pending.action, reason)}
          onClose={() => setPending(null)}
        >
          <p>{t(`purchasing.orders.${pending.action}Message`)}</p>
        </ReasonDialog>
      )}
    </>
  )
}
