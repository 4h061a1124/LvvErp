import { zodResolver } from '@hookform/resolvers/zod'
import { X } from 'lucide-react'
import { useState } from 'react'
import { Controller, useFieldArray, useForm, useWatch, type UseFormReturn } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Link, useNavigate, useParams } from 'react-router'
import { Button } from '@/components/ui/Button'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { Field, Input } from '@/components/ui/Field'
import { PageHeader } from '@/components/ui/PageHeader'
import { useToast } from '@/components/ui/toast-context'
import { MaterialPicker } from '@/features/inventory/documents/pickers'
import { useAuth } from '@/lib/auth-context'
import { getErrorMessage, getFieldErrors } from '@/lib/errors'
import { formatDate, formatDateTime, formatQuantity } from '@/lib/format'
import { useRequest, useRequestAction, useSaveRequest, type RequestAction } from '../api'
import { ReasonDialog } from '../components/ReasonDialog'
import { OrderStatusBadge, RequestStatusBadge } from '../components/StatusBadges'
import type { PurchaseRequest } from '../types'
import {
  addMaterial,
  emptyRequestLine,
  newRequestValues,
  requestSchema,
  requestToValues,
  valuesToRequestInput,
  type RequestFormValues,
} from './requestModel'

const LIST = '/purchasing/requests'

/** /purchasing/requests/{id|new} */
export function RequestPage() {
  const { t } = useTranslation()
  const { id } = useParams()
  const isNew = id === 'new'
  const { user } = useAuth()
  const request = useRequest(isNew ? undefined : id)

  if (isNew) {
    if (!user?.departmentId)
      return (
        <>
          <PageHeader title={t('purchasing.requests.newTitle')} meta={<BackLink />} />
          <div className="border-l-[3px] border-amber-500 bg-amber-50 px-3 py-2 text-[13px] text-amber-900">
            {t('purchasing.requests.noDepartment')}
          </div>
        </>
      )
    return <RequestEditor request={null} />
  }
  if (request.isError) return <p className="py-10 text-center text-sm text-red-700">{getErrorMessage(request.error, t)}</p>
  if (!request.data) return <p className="py-10 text-center text-sm text-zinc-500">{t('common.loading')}</p>

  return request.data.actions.canEdit ? (
    <RequestEditor key={request.data.id} request={request.data} />
  ) : (
    <RequestView key={request.data.id} request={request.data} />
  )
}

function BackLink() {
  const { t } = useTranslation()
  return (
    <Link to={LIST} className="text-brand-700 hover:underline">
      ← {t('purchasing.requests.title')}
    </Link>
  )
}

function RequestEditor({ request }: { request: PurchaseRequest | null }) {
  const { t, i18n } = useTranslation()
  const toast = useToast()
  const navigate = useNavigate()
  const { user } = useAuth()
  const save = useSaveRequest()
  const actions = useRequestAction()
  const [busy, setBusy] = useState(false)
  const [confirmDelete, setConfirmDelete] = useState(false)

  const form = useForm<RequestFormValues>({
    resolver: zodResolver(requestSchema),
    defaultValues: request ? requestToValues(request, i18n.language) : newRequestValues(),
  })

  async function persist(values: RequestFormValues): Promise<PurchaseRequest | null> {
    try {
      const saved = await save.mutateAsync({ id: request?.id, input: valuesToRequestInput(values) })
      form.reset(requestToValues(saved, i18n.language))
      return saved
    } catch (err) {
      const fields = Object.entries(getFieldErrors(err))
      const known = fields.filter(([key]) => /^(requestDate|neededDate|note|lines)/.test(key))
      // Server paths look like "lines[1].materialId"; the form's are "lines.1.material".
      for (const [key, message] of known)
        form.setError(key.replace(/\[(\d+)\]/g, '.$1').replace(/\.materialId$/, '.material') as never, { message })
      if (!known.length) toast.error(getErrorMessage(err, t))
      return null
    }
  }

  const onSave = form.handleSubmit(async (values) => {
    setBusy(true)
    const saved = await persist(values)
    setBusy(false)
    if (!saved) return
    toast.success(t('purchasing.requests.saved', { number: saved.number }))
    if (!request) navigate(`${LIST}/${saved.id}`, { replace: true })
  })

  const onSubmitForApproval = form.handleSubmit(async (values) => {
    setBusy(true)
    const saved = !request || form.formState.isDirty ? await persist(values) : request
    if (saved) {
      try {
        await actions.run.mutateAsync({ id: saved.id, action: 'submit' })
        toast.success(t('purchasing.requests.submitted', { number: saved.number }))
      } catch (err) {
        toast.error(getErrorMessage(err, t))
      }
      if (!request) navigate(`${LIST}/${saved.id}`, { replace: true })
    }
    setBusy(false)
  })

  async function remove() {
    if (!request) return
    try {
      await actions.remove.mutateAsync(request.id)
      toast.success(t('purchasing.requests.deleted', { number: request.number }))
      navigate(LIST, { replace: true })
    } catch (err) {
      toast.error(getErrorMessage(err, t))
    }
    setConfirmDelete(false)
  }

  const dirty = !request || form.formState.isDirty
  const { register, formState: { errors } } = form
  const message = (key?: string) => (key ? t(key as never) : undefined)

  return (
    <form onSubmit={onSave} noValidate>
      <PageHeader
        title={request ? request.number : t('purchasing.requests.newTitle')}
        meta={
          <span className="flex items-center gap-3">
            <BackLink />
            {request && <RequestStatusBadge status={request.status} />}
          </span>
        }
        actions={
          <>
            {request?.actions.canDelete && (
              <Button variant="dangerGhost" onClick={() => setConfirmDelete(true)} disabled={busy}>
                {t('common.delete')}
              </Button>
            )}
            <Button type="submit" disabled={busy}>
              {busy && save.isPending ? t('common.saving') : t('purchasing.requests.saveDraft')}
            </Button>
            <Button variant="primary" disabled={busy} onClick={onSubmitForApproval}>
              {dirty ? t('purchasing.requests.saveAndSubmit') : t('purchasing.requests.submit')}
            </Button>
          </>
        }
      />

      {request?.status === 'Rejected' && request.rejectReason && (
        <div role="alert" className="mb-4 border-l-[3px] border-red-600 bg-red-50 px-3 py-2 text-[13px] text-red-800">
          {t('purchasing.requests.rejectedNotice', { user: request.rejectedBy ?? '', reason: request.rejectReason })}
        </div>
      )}

      <div className="space-y-4">
        <section className="grid gap-x-4 gap-y-3 rounded-[4px] border border-zinc-200 bg-white p-4 sm:grid-cols-2 lg:grid-cols-4">
          <Field label={t('purchasing.requests.department')} htmlFor="department">
            <Input id="department" value={request?.departmentName ?? user?.departmentName ?? ''} disabled readOnly />
          </Field>
          <Field label={t('purchasing.requests.requestDate')} htmlFor="requestDate" required error={message(errors.requestDate?.message)}>
            <Input id="requestDate" type="date" invalid={!!errors.requestDate} {...register('requestDate')} />
          </Field>
          <Field label={t('purchasing.requests.neededDate')} htmlFor="neededDate" error={message(errors.neededDate?.message)}>
            <Input id="neededDate" type="date" invalid={!!errors.neededDate} {...register('neededDate')} />
          </Field>
          <Field
            label={t('purchasing.requests.purpose')}
            htmlFor="note"
            className="sm:col-span-2 lg:col-span-4"
            error={message(errors.note?.message)}
          >
            <Input id="note" autoComplete="off" invalid={!!errors.note} {...register('note')} />
          </Field>
        </section>

        <RequestLines form={form} />
      </div>

      <ConfirmDialog
        open={confirmDelete}
        title={t('purchasing.requests.deleteTitle')}
        confirmLabel={t('common.delete')}
        busy={actions.remove.isPending}
        onConfirm={remove}
        onClose={() => setConfirmDelete(false)}
      >
        <p>{request && t('purchasing.requests.deleteMessage', { number: request.number })}</p>
      </ConfirmDialog>
    </form>
  )
}

function RequestLines({ form }: { form: UseFormReturn<RequestFormValues> }) {
  const { t } = useTranslation()
  const { control, register, getValues, setValue, setFocus, formState: { errors } } = form
  const lines = useFieldArray({ control, name: 'lines', keyName: 'fieldKey' })
  const values = useWatch({ control, name: 'lines' })
  const message = (key?: string) => (key ? t(key as never) : undefined)

  function scan(material: Parameters<typeof addMaterial>[1]) {
    const current = getValues('lines')
    const { lines: next, index } = addMaterial(current, material)
    if (next.length > current.length) lines.append(next[index])
    else setValue(`lines.${index}.quantity`, next[index].quantity, { shouldDirty: true })
    setTimeout(() => setFocus(`lines.${index}.quantity`), 0)
  }

  return (
    <section className="rounded-[4px] border border-zinc-200 bg-white">
      <div className="flex items-center justify-between border-b border-zinc-200 px-3 py-2">
        <h2 className="text-sm font-semibold text-zinc-800">{t('purchasing.requests.lines')}</h2>
        <Button size="sm" onClick={() => lines.append(emptyRequestLine())}>
          {t('purchasing.requests.addLine')}
        </Button>
      </div>

      {lines.fields.length > 0 && (
        <div className="hidden gap-2 border-b border-zinc-100 bg-zinc-50 px-3 py-1.5 text-xs font-medium text-zinc-600 md:grid md:grid-cols-[2rem_minmax(0,5fr)_minmax(0,2fr)_minmax(0,4fr)_2rem]">
          <span>#</span>
          <span>{t('purchasing.requests.material')}</span>
          <span>{t('purchasing.requests.quantity')}</span>
          <span>{t('purchasing.requests.lineNote')}</span>
          <span />
        </div>
      )}

      {lines.fields.length === 0 ? (
        <p className={errors.lines?.message ? 'px-3 py-6 text-center text-sm text-red-600' : 'px-3 py-6 text-center text-sm text-zinc-500'}>
          {message(errors.lines?.message) ?? t('purchasing.requests.noLines')}
        </p>
      ) : (
        <ol className="divide-y divide-zinc-100">
          {lines.fields.map((field, index) => {
            const lineErrors = errors.lines?.[index]
            return (
              <li
                key={field.fieldKey}
                className="relative grid gap-2 px-3 py-2.5 md:grid-cols-[2rem_minmax(0,5fr)_minmax(0,2fr)_minmax(0,4fr)_2rem] md:items-start"
              >
                <span className="tabular pt-1.5 text-[13px] text-zinc-500">{index + 1}</span>
                <div>
                  <Controller
                    control={control}
                    name={`lines.${index}.material`}
                    render={({ field: f }) => <MaterialPicker value={f.value} invalid={!!lineErrors?.material} onChange={f.onChange} />}
                  />
                  {lineErrors?.material && <p className="mt-1 text-xs text-red-600">{message(lineErrors.material.message)}</p>}
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
                    <span className="w-10 shrink-0 truncate text-xs text-zinc-500">{values?.[index]?.material?.unitName}</span>
                  </div>
                  {lineErrors?.quantity && <p className="mt-1 text-xs text-red-600">{message(lineErrors.quantity.message)}</p>}
                </div>
                <Input
                  autoComplete="off"
                  aria-label={t('purchasing.requests.lineNote')}
                  placeholder={t('purchasing.requests.lineNote')}
                  {...register(`lines.${index}.note`)}
                />
                <button
                  type="button"
                  onClick={() => lines.remove(index)}
                  className="absolute top-1.5 right-2 rounded-[3px] p-1.5 text-zinc-400 hover:bg-zinc-100 hover:text-zinc-700 md:static"
                  aria-label={t('purchasing.requests.removeLine', { no: index + 1 })}
                >
                  <X size={15} />
                </button>
              </li>
            )
          })}
        </ol>
      )}

      <div className="border-t border-zinc-100 px-3 py-2.5">
        <MaterialPicker value={null} clearOnSelect onChange={scan} />
      </div>
    </section>
  )
}

type Pending = { action: Exclude<RequestAction, 'submit'> } | null

function RequestView({ request }: { request: PurchaseRequest }) {
  const { t, i18n } = useTranslation()
  const toast = useToast()
  const actions = useRequestAction()
  const [pending, setPending] = useState<Pending>(null)
  const a = request.actions

  async function run(action: RequestAction, reason?: string) {
    try {
      await actions.run.mutateAsync({ id: request.id, action, reason })
      const done = { submit: 'submitted', approve: 'approved', reject: 'rejected', cancel: 'cancelled', close: 'closed' } as const
      toast.success(t(`purchasing.requests.${done[action]}`, { number: request.number }))
    } catch (err) {
      toast.error(getErrorMessage(err, t))
    }
    setPending(null)
  }

  const when = (by: string | null, at: string | null) => (at ? [by, formatDateTime(at, i18n.language)].filter(Boolean).join(' · ') : null)
  const info: [string, string | null][] = [
    [t('purchasing.requests.department'), request.departmentName],
    [t('purchasing.requests.requester'), request.requesterName],
    [t('purchasing.requests.requestDate'), formatDate(request.requestDate)],
    [t('purchasing.requests.neededDate'), formatDate(request.neededDate)],
    [t('purchasing.requests.submittedAt'), request.submittedAt ? formatDateTime(request.submittedAt, i18n.language) : null],
    [t('purchasing.requests.approvedBy'), when(request.approvedBy, request.approvedAt)],
    // An earlier rejection of a request that was fixed and approved is no longer relevant.
    [t('purchasing.requests.rejectedBy'), request.status === 'Rejected' ? when(request.rejectedBy, request.rejectedAt) : null],
    [
      t('purchasing.requests.reason'),
      { Rejected: request.rejectReason, Closed: request.closeReason, Cancelled: request.cancelReason }[request.status as string] ?? null,
    ],
    [t('purchasing.requests.closedBy'), when(request.closedBy, request.closedAt)],
    [t('purchasing.requests.cancelledBy'), when(request.cancelledBy, request.cancelledAt)],
    [t('purchasing.requests.purpose'), request.note],
  ]
  const showOrdered = request.orders.length > 0

  return (
    <>
      <PageHeader
        title={request.number}
        meta={
          <span className="flex items-center gap-3">
            <BackLink />
            <RequestStatusBadge status={request.status} />
          </span>
        }
        actions={
          <>
            {a.canCancel && (
              <Button variant="dangerGhost" onClick={() => setPending({ action: 'cancel' })}>
                {t('purchasing.requests.cancel')}
              </Button>
            )}
            {a.canClose && <Button onClick={() => setPending({ action: 'close' })}>{t('purchasing.requests.close')}</Button>}
            {a.canSubmit && (
              <Button variant="primary" onClick={() => run('submit')} disabled={actions.run.isPending}>
                {t('purchasing.requests.submit')}
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
              <dt className="w-28 shrink-0 text-zinc-500">{label}</dt>
              <dd className="min-w-0 text-zinc-900">{value}</dd>
            </div>
          ))}
      </dl>

      <section className="mb-4 rounded-[4px] border border-zinc-200 bg-white">
        <div className="border-b border-zinc-200 px-3 py-2 text-sm font-semibold text-zinc-800">{t('purchasing.requests.lines')}</div>
        <div className="overflow-x-auto">
          <table className="w-full text-left">
            <thead>
              <tr className="border-b border-zinc-200 bg-zinc-50 text-[13px] text-zinc-600">
                <th className="w-10 px-3 py-2 font-medium">#</th>
                <th className="px-3 py-2 font-medium">{t('purchasing.requests.material')}</th>
                <th className="w-32 px-3 py-2 text-right font-medium">{t('purchasing.requests.quantity')}</th>
                {showOrdered && <th className="w-32 px-3 py-2 text-right font-medium">{t('purchasing.requests.ordered')}</th>}
                <th className="px-3 py-2 font-medium">{t('purchasing.requests.lineNote')}</th>
              </tr>
            </thead>
            <tbody>
              {request.lines.map((l) => (
                <tr key={l.id} className="border-b border-zinc-100 last:border-b-0">
                  <td className="tabular px-3 py-2 text-[13px] text-zinc-500">{l.lineNo}</td>
                  <td className="px-3 py-2">
                    <span className="tabular mr-2 text-[13px] text-zinc-500">{l.materialCode}</span>
                    <span className="text-zinc-900">{l.materialName}</span>
                  </td>
                  <td className="tabular px-3 py-2 text-right text-zinc-900">
                    {formatQuantity(l.quantity, i18n.language)} <span className="text-xs text-zinc-500">{l.unitName}</span>
                  </td>
                  {showOrdered && (
                    <td className="tabular px-3 py-2 text-right text-zinc-700">{formatQuantity(l.orderedQuantity, i18n.language)}</td>
                  )}
                  <td className="px-3 py-2 text-[13px] text-zinc-600">{l.note}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      {showOrdered && (
        <section className="rounded-[4px] border border-zinc-200 bg-white">
          <div className="border-b border-zinc-200 px-3 py-2 text-sm font-semibold text-zinc-800">{t('purchasing.requests.orders')}</div>
          <ul className="divide-y divide-zinc-100">
            {request.orders.map((o) => (
              <li key={o.id} className="flex items-center justify-between px-3 py-2">
                <Link to={`/purchasing/orders/${o.id}`} className="tabular text-[13px] font-medium text-brand-700 hover:underline">
                  {o.number}
                </Link>
                <OrderStatusBadge status={o.status} />
              </li>
            ))}
          </ul>
        </section>
      )}

      <ConfirmDialog
        open={pending?.action === 'approve'}
        title={t('purchasing.requests.approveTitle')}
        confirmLabel={t('purchasing.requests.approve')}
        busy={actions.run.isPending}
        onConfirm={() => run('approve')}
        onClose={() => setPending(null)}
      >
        <p>{t('purchasing.requests.approveMessage', { number: request.number })}</p>
      </ConfirmDialog>
      {pending && pending.action !== 'approve' && (
        <ReasonDialog
          title={t(`purchasing.requests.${pending.action}Title`)}
          confirmLabel={t(`purchasing.requests.${pending.action}`)}
          danger={pending.action !== 'close'}
          busy={actions.run.isPending}
          onConfirm={(reason) => run(pending.action, reason)}
          onClose={() => setPending(null)}
        >
          <p>{t(`purchasing.requests.${pending.action}Message`)}</p>
        </ReasonDialog>
      )}
    </>
  )
}
