import clsx from 'clsx'
import { LayoutGrid, Users, X } from 'lucide-react'
import type { ComponentType } from 'react'
import { useTranslation } from 'react-i18next'
import { NavLink } from 'react-router'
import type { Messages } from '@/locales/vi'
import { BrandMark } from './BrandMark'

type NavKey = keyof Messages['nav']

interface NavItem {
  to: string
  label: NavKey
  icon: ComponentType<{ size?: number; strokeWidth?: number }>
  end?: boolean
}

const sections: { title?: NavKey; items: NavItem[] }[] = [
  { items: [{ to: '/', label: 'overview', icon: LayoutGrid, end: true }] },
  { title: 'hr', items: [{ to: '/employees', label: 'employees', icon: Users }] },
]

export function Sidebar({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { t } = useTranslation()

  return (
    <>
      {/* Mobile backdrop */}
      <div
        className={clsx('fixed inset-0 z-30 bg-zinc-900/40 lg:hidden', open ? 'block' : 'hidden')}
        onClick={onClose}
        aria-hidden="true"
      />

      <aside
        className={clsx(
          'fixed inset-y-0 left-0 z-40 flex w-60 flex-col border-r border-zinc-200 bg-white transition-transform duration-150',
          'lg:w-56 lg:translate-x-0',
          open ? 'translate-x-0' : '-translate-x-full',
        )}
      >
        <div className="flex h-12 shrink-0 items-center justify-between border-b border-zinc-200 px-4">
          <div className="flex items-center gap-2">
            <BrandMark size={22} />
            <span className="text-[15px] font-semibold tracking-tight text-zinc-900">LVV ERP</span>
          </div>
          <button
            type="button"
            onClick={onClose}
            className="rounded-[3px] p-1 text-zinc-500 hover:bg-zinc-100 lg:hidden"
            aria-label={t('nav.closeMenu')}
          >
            <X size={18} />
          </button>
        </div>

        <nav className="flex-1 overflow-y-auto py-2">
          {sections.map((section, i) => (
            <div key={i} className={clsx(i > 0 && 'mt-3')}>
              {section.title && (
                <div className="px-4 pt-1 pb-1 text-xs font-medium text-zinc-400">{t(`nav.${section.title}`)}</div>
              )}
              {section.items.map((item) => (
                <NavLink
                  key={item.to}
                  to={item.to}
                  end={item.end}
                  onClick={onClose}
                  className={({ isActive }) =>
                    clsx(
                      'relative flex items-center gap-2.5 px-4 py-[7px] text-sm',
                      isActive
                        ? 'bg-brand-50 font-medium text-brand-700 before:absolute before:inset-y-0 before:left-0 before:w-[3px] before:bg-brand-600'
                        : 'text-zinc-700 hover:bg-zinc-50 hover:text-zinc-900',
                    )
                  }
                >
                  <item.icon size={16} strokeWidth={1.75} />
                  {t(`nav.${item.label}`)}
                </NavLink>
              ))}
            </div>
          ))}
        </nav>

        <div className="border-t border-zinc-200 px-4 py-2 text-xs text-zinc-400">v{__APP_VERSION__}</div>
      </aside>
    </>
  )
}
