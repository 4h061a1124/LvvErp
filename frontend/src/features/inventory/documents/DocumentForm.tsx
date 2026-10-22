import { Split, X } from 'lucide-react'
import { useEffect, useRef } from 'react'
import { Controller, useFieldArray, useWatch, type Control, type FieldErrors, type UseFormReturn } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'
import { Button } from '@/components/ui/Button'
import { Field, Input, Select } from '@/components/ui/Field'
import { useDepartments } from '@/features/employees/api'
import { formatDate, formatQuantity, parseQuantity, todayIso } from '@/lib/format'
import { useLocations, useWarehouses } from '../api'
import { useAvailableStock, useLots } from './api'
import { emptyLine, sourceKey, splitLine, type DocumentFormValues } from './documentModel'
import { EmployeePicker, MaterialPicker } from './pickers'
import { REASONS, type DocumentType } from './types'

/** Header fields and editable lines of a draft. Owned by DocumentPage, which handles saving. */
export function DocumentForm({ type, form }: { type: DocumentType; form: UseFormReturn<DocumentFormValues> }) {
  const { t } = useTranslation()
  const {
    register,
    control,
    setValue,
    getValues,
    formState: { errors },
  } = form
  const warehouses = useWarehouses(true)
  const departments = useDepartments()
  const lines = useFieldArray({ control, name: 'lines', keyName: 'fieldKey' })
  const warehouseId = useWatch({ control, name: 'warehouseId' })
  const toWarehouseId = useWatch({ control, name: 'toWarehouseId' })
  // A receipt from a purchase order: warehouse, reason and materials come from the order.
  const purchaseOrder = useWatch({ control, name: 'purchaseOrder' })

  // Locations and lots belong to a warehouse: changing it invalidates what the lines picked.
  const previousWarehouse = useRef(warehouseId)
  const previousToWarehouse = useRef(toWarehouseId)
  useEffect(() => {
    const lineCount = getValues('lines').length
    if (warehouseId !== previousWarehouse.current) {
      for (let i = 0; i < lineCount; i++) {
        setValue(`lines.${i}.locationId`, '')
        setValue(`lines.${i}.source`, '')
      }
      previousWarehouse.current = warehouseId
    }
    if (toWarehouseId !== previousToWarehouse.current) {
      for (let i = 0; i < lineCount; i++) setValue(`lines.${i}.toLocationId`, '')
      previousToWarehouse.current = toWarehouseId
    }
  }, [warehouseId, toWarehouseId, getValues, setValue])

  const message = (key?: string) => (key ? t(key as never) : undefined)
  const warehouseLabel =
    type === 'Receipt' ? t('inventory.documents.receiptWarehouse') : type === 'Issue' ? t('inventory.documents.warehouse') : t('inventory.documents.fromWarehouse')

  return (
    <div className="space-y-4">
      {purchaseOrder && (
        <div className="border-l-[3px] border-brand-600 bg-brand-50 px-3 py-2 text-[13px] text-zinc-800">
          {t('inventory.documents.fromOrder')}{' '}
          <Link to={`/purchasing/orders/${purchaseOrder.id}`} className="tabular font-medium text-brand-700 hover:underline">
            {purchaseOrder.number}
          </Link>
          <span className="text-zinc-500"> · {t('inventory.documents.fromOrderHint')}</span>
        </div>
      )}
      <section className="grid gap-x-4 gap-y-3 rounded-[4px] border border-zinc-200 bg-white p-4 sm:grid-cols-2 lg:grid-cols-4">
        <Field label={t('inventory.documents.date')} htmlFor="documentDate" required error={message(errors.documentDate?.message)}>
          <Input id="documentDate" type="date" max={todayIso()} invalid={!!errors.documentDate} {...register('documentDate')} />
        </Field>
        <Field label={warehouseLabel} htmlFor="warehouseId" required error={message(errors.warehouseId?.message)}>
          {purchaseOrder ? (
            // Not a registered (disabled) select: react-hook-form would drop its value.
            <Input id="warehouseId" value={warehouses.data?.find((w) => w.id === warehouseId)?.name ?? ''} disabled readOnly />
          ) : (
            <Select id="warehouseId" invalid={!!errors.warehouseId} {...register('warehouseId')}>
              <option value="">{t('common.choose')}</option>
              {warehouses.data?.map((w) => (
                <option key={w.id} value={w.id}>
                  {w.name}
                </option>
              ))}
            </Select>
          )}
        </Field>
        {type === 'Transfer' ? (
          <Field label={t('inventory.documents.toWarehouse')} htmlFor="toWarehouseId" required error={message(errors.toWarehouseId?.message)}>
            <Select id="toWarehouseId" invalid={!!errors.toWarehouseId} {...register('toWarehouseId')}>
              <option value="">{t('common.choose')}</option>
              {warehouses.data?.map((w) => (
                <option key={w.id} value={w.id}>
                  {w.name}
                </option>
              ))}
            </Select>
          </Field>
        ) : (
          <Field label={t('inventory.documents.reason')} htmlFor="reason" required error={message(errors.reason?.message)}>
            {purchaseOrder ? (
              <Input id="reason" value={t('inventory.documents.reasons.Purchase')} disabled readOnly />
            ) : (
              <Select id="reason" invalid={!!errors.reason} {...register('reason')}>
                {REASONS[type].map((r) => (
                  <option key={r} value={r}>
                    {t(`inventory.documents.reasons.${r}`)}
                  </option>
                ))}
              </Select>
            )}
          </Field>
        )}
        {type === 'Receipt' && (
          <Field label={t('inventory.documents.partner')} htmlFor="partner" error={message(errors.partner?.message)}>
            <Input id="partner" autoComplete="off" {...register('partner')} />
          </Field>
        )}
        {type === 'Issue' && (
          <>
            <Field label={t('inventory.documents.employee')} htmlFor="employee">
              <Controller
                control={control}
                name="employee"
                render={({ field }) => (
                  <EmployeePicker
                    id="employee"
                    value={field.value}
                    onChange={(employee) => {
                      field.onChange(employee)
                      if (employee) setValue('departmentId', employee.departmentId)
                    }}
                  />
                )}
              />
            </Field>
            <Field label={t('inventory.documents.department')} htmlFor="departmentId">
              <Select id="departmentId" {...register('departmentId')}>
                <option value="">{t('common.choose')}</option>
                {departments.data?.map((d) => (
                  <option key={d.id} value={d.id}>
                    {d.name}
                  </option>
                ))}
              </Select>
            </Field>
          </>
        )}
        <Field
          label={t('inventory.documents.note')}
          htmlFor="note"
          className="sm:col-span-2 lg:col-span-4"
          error={message(errors.note?.message)}
        >
          <Input id="note" autoComplete="off" {...register('note')} />
        </Field>
      </section>

      <section className="rounded-[4px] border border-zinc-200 bg-white">
        <div className="flex items-center justify-between border-b border-zinc-200 px-3 py-2">
          <h2 className="text-sm font-semibold text-zinc-800">{t('inventory.documents.lines')}</h2>
          {!purchaseOrder && (
            <Button size="sm" onClick={() => lines.append(emptyLine())}>
              {t('inventory.documents.addLine')}
            </Button>
          )}
        </div>

        {lines.fields.length > 0 && (
          <div className="hidden gap-2 border-b border-zinc-100 bg-zinc-50 px-3 py-1.5 text-xs font-medium text-zinc-600 md:grid md:grid-cols-[2rem_minmax(0,4fr)_minmax(0,5fr)_minmax(0,2fr)_2rem]">
            <span>#</span>
            <span>{t('inventory.documents.material')}</span>
            <span>
              {type === 'Receipt'
                ? `${t('inventory.documents.location')} / ${t('inventory.documents.lot')}`
                : type === 'Transfer'
                  ? `${t('inventory.documents.source')} → ${t('inventory.documents.toLocation')}`
                  : t('inventory.documents.source')}
            </span>
            <span>{t('inventory.documents.quantity')}</span>
            <span />
          </div>
        )}

        {lines.fields.length === 0 ? (
          <p className="px-3 py-6 text-center text-sm text-zinc-500">{t('inventory.documents.noLines')}</p>
        ) : (
          <ol className="divide-y divide-zinc-100">
            {lines.fields.map((field, index) => (
              <LineRow
                key={field.fieldKey}
                index={index}
                type={type}
                control={control}
                form={form}
                warehouseId={warehouseId}
                toWarehouseId={toWarehouseId}
                errors={errors}
                onRemove={() => lines.remove(index)}
                onSplit={purchaseOrder ? () => lines.insert(index + 1, splitLine(getValues(`lines.${index}`))) : undefined}
              />
            ))}
          </ol>
        )}

        {!purchaseOrder && (
          <div className="border-t border-zinc-100 px-3 py-2.5">
            <MaterialPicker value={null} clearOnSelect onChange={(material) => lines.append({ ...emptyLine(material), quantity: '1' })} />
          </div>
        )}
      </section>
    </div>
  )
}

interface LineRowProps {
  index: number
  type: DocumentType
  control: Control<DocumentFormValues>
  form: UseFormReturn<DocumentFormValues>
  warehouseId: string
  toWarehouseId: string
  errors: FieldErrors<DocumentFormValues>
  onRemove: () => void
  /** Purchase-order receipts: add another line for the same order line (another lot). */
  onSplit?: () => void
}

function LineRow({ index, type, control, form, warehouseId, toWarehouseId, errors, onRemove, onSplit }: LineRowProps) {
  const { t, i18n } = useTranslation()
  const { register, setValue } = form
  const line = useWatch({ control, name: `lines.${index}` })
  const lineErrors = errors.lines?.[index]
  const material = line?.material ?? null
  const message = (key?: string) => (key ? t(key as never) : undefined)

  const available = useAvailableStock(type !== 'Receipt' ? material?.id : undefined, warehouseId || undefined)
  const sources = available.data ?? []
  const chosen = sources.find((s) => sourceKey(s.locationId, s.lotId) === line?.source)

  // A single place the stock can come from: pick it for the user.
  const onlySource = available.data?.length === 1 ? available.data[0] : undefined
  const currentSource = line?.source
  useEffect(() => {
    if (type !== 'Receipt' && onlySource && !currentSource)
      setValue(`lines.${index}.source`, sourceKey(onlySource.locationId, onlySource.lotId), { shouldDirty: true })
  }, [type, onlySource, currentSource, index, setValue])

  const quantity = parseQuantity(line?.quantity ?? '')
  const overAvailable = chosen && quantity !== null && quantity > chosen.quantity

  return (
    <li className="relative grid gap-2 px-3 py-2.5 md:grid-cols-[2rem_minmax(0,4fr)_minmax(0,5fr)_minmax(0,2fr)_2rem] md:items-start">
      <span className="tabular pt-1.5 text-[13px] text-zinc-500">{index + 1}</span>

      <div>
        {onSplit ? (
          <div className="pt-1 text-sm">
            <span className="tabular mr-1.5 text-[13px] text-zinc-500">{material?.code}</span>
            <span className="text-zinc-900">{material?.name}</span>
            <button type="button" onClick={onSplit} className="ml-2 inline-flex items-center gap-1 text-xs text-brand-700 hover:underline">
              <Split size={12} />
              {t('inventory.documents.splitLine')}
            </button>
          </div>
        ) : (
        <Controller
          control={control}
          name={`lines.${index}.material`}
          render={({ field }) => (
            <MaterialPicker
              value={field.value}
              invalid={!!lineErrors?.material}
              onChange={(m) => {
                field.onChange(m)
                setValue(`lines.${index}.source`, '')
                setValue(`lines.${index}.lotNumber`, '')
              }}
            />
          )}
        />
        )}
        {lineErrors?.material && <p className="mt-1 text-xs text-red-600">{message(lineErrors.material.message)}</p>}
      </div>

      <div className="space-y-1.5">
        {type === 'Receipt' ? (
          <ReceiptPlacement index={index} form={form} warehouseId={warehouseId} trackLots={!!material?.trackLots} materialId={material?.id} />
        ) : (
          <>
            <Select
              aria-label={t('inventory.documents.source')}
              invalid={!!lineErrors?.source}
              disabled={!material || !warehouseId}
              {...register(`lines.${index}.source`)}
            >
              <option value="">{t('inventory.documents.chooseSource')}</option>
              {sources.map((s) => (
                <option key={sourceKey(s.locationId, s.lotId)} value={sourceKey(s.locationId, s.lotId)}>
                  {[
                    s.locationCode ?? t('inventory.documents.noLocation'),
                    s.lotNumber && `${t('inventory.documents.lot')} ${s.lotNumber}`,
                    s.expiryDate && `${t('inventory.documents.expiryDate')} ${formatDate(s.expiryDate)}`,
                    t('inventory.documents.available', { quantity: formatQuantity(s.quantity, i18n.language) }),
                  ]
                    .filter(Boolean)
                    .join(' · ')}
                </option>
              ))}
              {line?.source && !chosen && !available.isPending && <option value={line.source}>{t('inventory.documents.selectedMissing')}</option>}
            </Select>
            {lineErrors?.source && <p className="text-xs text-red-600">{message(lineErrors.source.message)}</p>}
            {material && warehouseId && available.data && sources.length === 0 && (
              <p className="text-xs text-amber-700">{t('inventory.documents.noStock')}</p>
            )}
            {type === 'Transfer' && <ToLocationSelect index={index} form={form} toWarehouseId={toWarehouseId} />}
          </>
        )}
      </div>

      <div>
        <div className="flex items-center gap-1.5">
          <Input
            inputMode="decimal"
            autoComplete="off"
            aria-label={t('inventory.documents.quantity')}
            invalid={!!lineErrors?.quantity}
            className="text-right"
            {...register(`lines.${index}.quantity`)}
          />
          <span className="w-10 shrink-0 truncate text-xs text-zinc-500">{material?.unitName}</span>
        </div>
        {lineErrors?.quantity && <p className="mt-1 text-xs text-red-600">{message(lineErrors.quantity.message)}</p>}
        {overAvailable && (
          <p className="mt-1 text-xs text-amber-700">
            {t('inventory.documents.overAvailable', { quantity: formatQuantity(chosen.quantity, i18n.language) })}
          </p>
        )}
      </div>

      <button
        type="button"
        onClick={onRemove}
        className="absolute top-1.5 right-2 rounded-[3px] p-1.5 text-zinc-400 hover:bg-zinc-100 hover:text-zinc-700 md:static"
        aria-label={t('inventory.documents.removeLine', { no: index + 1 })}
      >
        <X size={15} />
      </button>
    </li>
  )
}

function ReceiptPlacement({
  index,
  form,
  warehouseId,
  trackLots,
  materialId,
}: {
  index: number
  form: UseFormReturn<DocumentFormValues>
  warehouseId: string
  trackLots: boolean
  materialId: string | undefined
}) {
  const { t } = useTranslation()
  const { register, setValue, getValues, formState } = form
  const locations = useLocations(warehouseId || undefined, true)
  const lots = useLots(materialId, trackLots)
  const lineErrors = formState.errors.lines?.[index]
  const message = (key?: string) => (key ? t(key as never) : undefined)
  const listId = `lots-${index}`

  // Picking an existing lot number fills in its dates.
  function fillDatesFromLot(lotNumber: string) {
    const lot = lots.data?.find((l) => l.lotNumber === lotNumber.trim())
    if (!lot) return
    if (!getValues(`lines.${index}.manufactureDate`) && lot.manufactureDate) setValue(`lines.${index}.manufactureDate`, lot.manufactureDate)
    if (!getValues(`lines.${index}.expiryDate`) && lot.expiryDate) setValue(`lines.${index}.expiryDate`, lot.expiryDate)
  }

  return (
    <>
      {(locations.data?.length ?? 0) > 0 && (
        <Select aria-label={t('inventory.documents.location')} {...register(`lines.${index}.locationId`)}>
          <option value="">{t('inventory.documents.noLocation')}</option>
          {locations.data?.map((l) => (
            <option key={l.id} value={l.id}>
              {l.code}
            </option>
          ))}
        </Select>
      )}
      {trackLots && (
        <div className="grid grid-cols-[minmax(0,3fr)_minmax(0,2fr)_minmax(0,2fr)] gap-1.5">
          <Input
            list={listId}
            placeholder={t('inventory.documents.lotNumber')}
            aria-label={t('inventory.documents.lotNumber')}
            autoComplete="off"
            invalid={!!lineErrors?.lotNumber}
            {...register(`lines.${index}.lotNumber`, { onChange: (e) => fillDatesFromLot(e.target.value) })}
          />
          <datalist id={listId}>
            {lots.data?.map((l) => (
              <option key={l.id} value={l.lotNumber} />
            ))}
          </datalist>
          <Input type="date" title={t('inventory.documents.manufactureDate')} aria-label={t('inventory.documents.manufactureDate')} {...register(`lines.${index}.manufactureDate`)} />
          <Input
            type="date"
            title={t('inventory.documents.expiryDate')}
            aria-label={t('inventory.documents.expiryDate')}
            invalid={!!lineErrors?.expiryDate}
            {...register(`lines.${index}.expiryDate`)}
          />
        </div>
      )}
      {(lineErrors?.lotNumber || lineErrors?.expiryDate) && (
        <p className="text-xs text-red-600">{message(lineErrors.lotNumber?.message ?? lineErrors.expiryDate?.message)}</p>
      )}
    </>
  )
}

function ToLocationSelect({ index, form, toWarehouseId }: { index: number; form: UseFormReturn<DocumentFormValues>; toWarehouseId: string }) {
  const { t } = useTranslation()
  const locations = useLocations(toWarehouseId || undefined, true)
  const error = form.formState.errors.lines?.[index]?.toLocationId
  if (!toWarehouseId || !(locations.data?.length ?? 0)) return null
  return (
    <>
      <Select aria-label={t('inventory.documents.toLocation')} invalid={!!error} {...form.register(`lines.${index}.toLocationId`)}>
        <option value="">→ {t('inventory.documents.noLocation')}</option>
        {locations.data?.map((l) => (
          <option key={l.id} value={l.id}>
            → {l.code}
          </option>
        ))}
      </Select>
      {error && <p className="text-xs text-red-600">{t(error.message as never)}</p>}
    </>
  )
}
