import clsx from 'clsx'
import { useTranslation } from 'react-i18next'

export function ActiveStatus({ active }: { active: boolean }) {
  const { t } = useTranslation()
  return (
    <span
      className={clsx(
        'inline-flex items-center gap-1.5 text-[13px] whitespace-nowrap before:size-1.5 before:rounded-full',
        active ? 'text-emerald-700 before:bg-emerald-600' : 'text-zinc-500 before:bg-zinc-400',
      )}
    >
      {active ? t('common.active') : t('common.inactive')}
    </span>
  )
}

/** "Đang sử dụng" checkbox shared by catalog forms. */
export function ActiveCheckbox({ checked, onChange }: { checked: boolean; onChange: (checked: boolean) => void }) {
  const { t } = useTranslation()
  return (
    <label className="flex cursor-pointer items-start gap-2 text-sm">
      <input
        type="checkbox"
        checked={checked}
        onChange={(e) => onChange(e.target.checked)}
        className="mt-0.5 size-4 accent-brand-600"
      />
      <span>
        <span className="text-zinc-800">{t('inventory.activeLabel')}</span>
        <span className="block text-xs text-zinc-500">{t('inventory.activeHint')}</span>
      </span>
    </label>
  )
}
