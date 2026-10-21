import clsx from 'clsx'
import { useTranslation } from 'react-i18next'
import { LANGUAGES } from '@/lib/i18n'

export function LanguageSwitch({ className }: { className?: string }) {
  const { i18n } = useTranslation()

  return (
    <div className={clsx('flex overflow-hidden rounded-[3px] border border-zinc-300 text-xs', className)}>
      {LANGUAGES.map((lng) => (
        <button
          key={lng}
          type="button"
          onClick={() => i18n.changeLanguage(lng)}
          aria-pressed={i18n.language === lng}
          className={clsx(
            'px-2 py-1 font-medium uppercase',
            i18n.language === lng ? 'bg-zinc-800 text-white' : 'bg-white text-zinc-600 hover:bg-zinc-50',
          )}
        >
          {lng}
        </button>
      ))}
    </div>
  )
}
