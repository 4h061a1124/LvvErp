import { zodResolver } from '@hookform/resolvers/zod'
import clsx from 'clsx'
import { useState } from 'react'
import { Controller, useForm } from 'react-hook-form'
import { useTranslation } from 'react-i18next'
import { useSearchParams } from 'react-router'
import { Button } from '@/components/ui/Button'
import { ConfirmDialog } from '@/components/ui/ConfirmDialog'
import { Dialog } from '@/components/ui/Dialog'
import { Field, Input } from '@/components/ui/Field'
import { PageHeader } from '@/components/ui/PageHeader'
import { useToast } from '@/components/ui/toast-context'
import { useAuth } from '@/lib/auth-context'
import { getErrorCode, getErrorMessage } from '@/lib/errors'
import { Permissions } from '@/lib/permissions'
import { useDeleteLocation, useDeleteWarehouse, useLocations, useSaveLocation, useSaveWarehouse, useWarehouses } from '../api'
import { ActiveCheckbox, ActiveStatus } from '../components/ActiveStatus'
import { locationSchema, warehouseSchema, type LocationFormValues, type WarehouseFormValues } from '../schema'
import type { Location, Warehouse } from '../types'

export function WarehousesPage() {
  const { t } = useTranslation()
  const toast = useToast()
  const canManage = useAuth().can(Permissions.InventoryManageCatalog)
  const warehouses = useWarehouses()
  const deleteWarehouse = useDeleteWarehouse()
  const deleteLocation = useDeleteLocation()

  // Selected warehouse lives in the URL (?w=) so a refresh keeps it.
  const [params, setParams] = useSearchParams()
  const selectedId = params.get('w') ?? warehouses.data?.[0]?.id
  const selected = warehouses.data?.find((w) => w.id === selectedId)
  const locations = useLocations(selected?.id)

  const [warehouseForm, setWarehouseForm] = useState<{ item: Warehouse | null } | null>(null)
  const [locationForm, setLocationForm] = useState<{ item: Location | null } | null>(null)
  const [deletingWarehouse, setDeletingWarehouse] = useState<Warehouse | null>(null)
  const [deletingLocation, setDeletingLocation] = useState<Location | null>(null)

  const select = (id: string) => setParams({ w: id }, { replace: true })

  async function confirmDeleteWarehouse() {
    if (!deletingWarehouse) return
    try {
      await deleteWarehouse.mutateAsync(deletingWarehouse.id)
      toast.success(t('inventory.deleted', { name: deletingWarehouse.name }))
      if (deletingWarehouse.id === selectedId) setParams({}, { replace: true })
    } catch (err) {
      toast.error(getErrorMessage(err, t))
    }
    setDeletingWarehouse(null)
  }

  async function confirmDeleteLocation() {
    if (!deletingLocation) return
    try {
      await deleteLocation.mutateAsync(deletingLocation.id)
      toast.success(t('inventory.deleted', { name: deletingLocation.code }))
    } catch (err) {
      toast.error(getErrorMessage(err, t))
    }
    setDeletingLocation(null)
  }

  const actionLinks = (onEdit: () => void, onDelete: () => void) => (
    <span className="text-[13px] whitespace-nowrap">
      <button type="button" onClick={onEdit} className="px-1 text-brand-700 hover:underline">
        {t('common.edit')}
      </button>
      <span className="mx-1 text-zinc-300">|</span>
      <button type="button" onClick={onDelete} className="px-1 text-red-700 hover:underline">
        {t('common.delete')}
      </button>
    </span>
  )

  return (
    <>
      <PageHeader
        title={t('inventory.warehouses.title')}
        actions={
          canManage && (
            <Button variant="primary" onClick={() => setWarehouseForm({ item: null })}>
              {t('inventory.warehouses.add')}
            </Button>
          )
        }
      />

      <div className="grid items-start gap-4 lg:grid-cols-[minmax(0,5fr)_minmax(0,4fr)]">
        <section className="rounded-[4px] border border-zinc-200 bg-white">
          {warehouses.isPending ? (
            <p className="px-3 py-8 text-center text-sm text-zinc-500">{t('common.loading')}</p>
          ) : !warehouses.data?.length ? (
            <p className="px-3 py-8 text-center text-sm text-zinc-500">{t('inventory.warehouses.empty')}</p>
          ) : (
            <ul className="divide-y divide-zinc-100">
              {warehouses.data.map((w) => (
                <li
                  key={w.id}
                  className={clsx(
                    'relative flex items-center gap-3 px-3 py-2.5',
                    w.id === selectedId ? 'bg-brand-50 before:absolute before:inset-y-0 before:left-0 before:w-[3px] before:bg-brand-600' : 'hover:bg-zinc-50',
                  )}
                >
                  <button type="button" onClick={() => select(w.id)} className="min-w-0 flex-1 text-left">
                    <div className="flex items-baseline gap-2">
                      <span className={clsx('truncate font-medium', w.isActive ? 'text-zinc-900' : 'text-zinc-500')}>{w.name}</span>
                      <span className="tabular shrink-0 text-xs text-zinc-500">{w.code}</span>
                    </div>
                    <div className="mt-0.5 flex items-center gap-3 text-[13px] text-zinc-500">
                      <span>{t('inventory.warehouses.locationCount', { count: w.locationCount })}</span>
                      {!w.isActive && <ActiveStatus active={false} />}
                      {w.note && <span className="truncate">{w.note}</span>}
                    </div>
                  </button>
                  {canManage &&
                    actionLinks(
                      () => setWarehouseForm({ item: w }),
                      () => setDeletingWarehouse(w),
                    )}
                </li>
              ))}
            </ul>
          )}
        </section>

        <section className="rounded-[4px] border border-zinc-200 bg-white">
          <div className="flex items-center justify-between gap-2 border-b border-zinc-200 px-3 py-2">
            <h2 className="truncate text-sm font-semibold text-zinc-800">
              {t('inventory.warehouses.locations')}
              {selected && <span className="font-normal text-zinc-500"> · {selected.name}</span>}
            </h2>
            {canManage && selected && (
              <Button size="sm" onClick={() => setLocationForm({ item: null })}>
                {t('inventory.warehouses.addLocation')}
              </Button>
            )}
          </div>
          {!selected ? (
            <p className="px-3 py-8 text-center text-sm text-zinc-500">{t('inventory.warehouses.selectWarehouse')}</p>
          ) : locations.isPending ? (
            <p className="px-3 py-8 text-center text-sm text-zinc-500">{t('common.loading')}</p>
          ) : !locations.data?.length ? (
            <p className="px-3 py-8 text-center text-sm text-zinc-500">{t('inventory.warehouses.noLocations')}</p>
          ) : (
            <table className="w-full text-left">
              <thead>
                <tr className="border-b border-zinc-200 bg-zinc-50 text-[13px] text-zinc-600">
                  <th className="w-28 px-3 py-2 font-medium">{t('inventory.warehouses.locationCode')}</th>
                  <th className="px-3 py-2 font-medium">{t('inventory.warehouses.description')}</th>
                  <th className="hidden w-28 px-3 py-2 font-medium sm:table-cell">{t('inventory.status')}</th>
                  {canManage && <th className="w-24 px-3 py-2" />}
                </tr>
              </thead>
              <tbody>
                {locations.data.map((l) => (
                  <tr key={l.id} className="border-b border-zinc-100 last:border-b-0">
                    <td className={clsx('tabular px-3 py-2 text-[13px] font-medium', l.isActive ? 'text-zinc-900' : 'text-zinc-500')}>
                      {l.code}
                    </td>
                    <td className="px-3 py-2 text-[13px] text-zinc-600">{l.description}</td>
                    <td className="hidden px-3 py-2 sm:table-cell">
                      <ActiveStatus active={l.isActive} />
                    </td>
                    {canManage && (
                      <td className="px-3 py-2 text-right">
                        {actionLinks(
                          () => setLocationForm({ item: l }),
                          () => setDeletingLocation(l),
                        )}
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          )}
        </section>
      </div>

      {warehouseForm && (
        <WarehouseDialog
          item={warehouseForm.item}
          onSaved={(w) => select(w.id)}
          onClose={() => setWarehouseForm(null)}
        />
      )}
      {locationForm && selected && (
        <LocationDialog warehouseId={selected.id} item={locationForm.item} onClose={() => setLocationForm(null)} />
      )}
      <ConfirmDialog
        open={!!deletingWarehouse}
        title={t('inventory.warehouses.deleteTitle')}
        confirmLabel={t('common.delete')}
        busy={deleteWarehouse.isPending}
        onConfirm={confirmDeleteWarehouse}
        onClose={() => setDeletingWarehouse(null)}
      >
        <p>{deletingWarehouse && t('inventory.warehouses.deleteMessage', { name: deletingWarehouse.name })}</p>
      </ConfirmDialog>
      <ConfirmDialog
        open={!!deletingLocation}
        title={t('inventory.warehouses.deleteLocationTitle')}
        confirmLabel={t('common.delete')}
        busy={deleteLocation.isPending}
        onConfirm={confirmDeleteLocation}
        onClose={() => setDeletingLocation(null)}
      >
        <p>{deletingLocation && t('inventory.warehouses.deleteLocationMessage', { code: deletingLocation.code })}</p>
      </ConfirmDialog>
    </>
  )
}

function WarehouseDialog({ item, onSaved, onClose }: { item: Warehouse | null; onSaved: (w: Warehouse) => void; onClose: () => void }) {
  const { t } = useTranslation()
  const toast = useToast()
  const save = useSaveWarehouse()
  const {
    register,
    control,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<WarehouseFormValues>({
    resolver: zodResolver(warehouseSchema),
    defaultValues: item
      ? { code: item.code, name: item.name, note: item.note ?? '', isActive: item.isActive }
      : { code: '', name: '', note: '', isActive: true },
  })

  const onSubmit = handleSubmit(async (values) => {
    try {
      const saved = await save.mutateAsync({
        id: item?.id,
        input: { ...values, code: values.code.toUpperCase(), note: values.note || null },
      })
      toast.success(t('inventory.saved', { name: saved.name }))
      onSaved(saved)
      onClose()
    } catch (err) {
      const code = getErrorCode(err)
      if (code === 'DuplicateCode') setError('code', { message: 'errors.codes.DuplicateCode' })
      else if (code === 'DuplicateName') setError('name', { message: 'errors.codes.DuplicateName' })
      else toast.error(getErrorMessage(err, t))
    }
  })

  const message = (key?: string) => (key ? t(key as never) : undefined)

  return (
    <Dialog
      open
      onClose={onClose}
      title={t(item ? 'inventory.warehouses.editTitle' : 'inventory.warehouses.createTitle')}
      width="sm"
      footer={
        <>
          <Button onClick={onClose} disabled={isSubmitting}>
            {t('common.cancel')}
          </Button>
          <Button variant="primary" type="submit" form="warehouse-form" disabled={isSubmitting}>
            {isSubmitting ? t('common.saving') : t('common.save')}
          </Button>
        </>
      }
    >
      <form id="warehouse-form" onSubmit={onSubmit} className="space-y-3" noValidate>
        <Field label={t('inventory.code')} htmlFor="code" required error={message(errors.code?.message)}>
          <Input id="code" autoComplete="off" className="uppercase" invalid={!!errors.code} {...register('code')} />
        </Field>
        <Field label={t('inventory.name')} htmlFor="name" required error={message(errors.name?.message)}>
          <Input id="name" autoComplete="off" invalid={!!errors.name} {...register('name')} />
        </Field>
        <Field label={t('inventory.warehouses.note')} htmlFor="note" error={message(errors.note?.message)}>
          <Input id="note" autoComplete="off" invalid={!!errors.note} {...register('note')} />
        </Field>
        <Controller
          control={control}
          name="isActive"
          render={({ field }) => <ActiveCheckbox checked={field.value} onChange={field.onChange} />}
        />
      </form>
    </Dialog>
  )
}

function LocationDialog({ warehouseId, item, onClose }: { warehouseId: string; item: Location | null; onClose: () => void }) {
  const { t } = useTranslation()
  const toast = useToast()
  const save = useSaveLocation(warehouseId)
  const {
    register,
    control,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<LocationFormValues>({
    resolver: zodResolver(locationSchema),
    defaultValues: item
      ? { code: item.code, description: item.description ?? '', isActive: item.isActive }
      : { code: '', description: '', isActive: true },
  })

  const onSubmit = handleSubmit(async (values) => {
    try {
      const saved = await save.mutateAsync({
        id: item?.id,
        input: { ...values, code: values.code.toUpperCase(), description: values.description || null },
      })
      toast.success(t('inventory.saved', { name: saved.code }))
      onClose()
    } catch (err) {
      if (getErrorCode(err) === 'DuplicateCode') setError('code', { message: 'errors.codes.DuplicateCode' })
      else toast.error(getErrorMessage(err, t))
    }
  })

  const message = (key?: string) => (key ? t(key as never) : undefined)

  return (
    <Dialog
      open
      onClose={onClose}
      title={t(item ? 'inventory.warehouses.editLocationTitle' : 'inventory.warehouses.createLocationTitle')}
      width="sm"
      footer={
        <>
          <Button onClick={onClose} disabled={isSubmitting}>
            {t('common.cancel')}
          </Button>
          <Button variant="primary" type="submit" form="location-form" disabled={isSubmitting}>
            {isSubmitting ? t('common.saving') : t('common.save')}
          </Button>
        </>
      }
    >
      <form id="location-form" onSubmit={onSubmit} className="space-y-3" noValidate>
        <Field
          label={t('inventory.warehouses.locationCode')}
          htmlFor="code"
          required
          error={message(errors.code?.message)}
          hint={t('inventory.warehouses.locationCodeHint')}
        >
          <Input id="code" autoComplete="off" className="uppercase" invalid={!!errors.code} {...register('code')} />
        </Field>
        <Field label={t('inventory.warehouses.description')} htmlFor="description" error={message(errors.description?.message)}>
          <Input id="description" autoComplete="off" invalid={!!errors.description} {...register('description')} />
        </Field>
        <Controller
          control={control}
          name="isActive"
          render={({ field }) => <ActiveCheckbox checked={field.value} onChange={field.onChange} />}
        />
      </form>
    </Dialog>
  )
}
