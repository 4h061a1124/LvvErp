import { ChevronDown, KeyRound, LogOut, Menu as MenuIcon } from 'lucide-react'
import { useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Menu, MenuItem } from '@/components/ui/Menu'
import { ChangePasswordDialog } from '@/features/auth/ChangePasswordDialog'
import { useAuth } from '@/lib/auth-context'
import type { Messages } from '@/locales/vi'
import { LanguageSwitch } from './LanguageSwitch'

export function Topbar({ onOpenMenu }: { onOpenMenu: () => void }) {
  const { t } = useTranslation()
  const { user, logout } = useAuth()
  const [changingPassword, setChangingPassword] = useState(false)

  const roleLabel = user?.roles.map((r) => t(`roles.${r as keyof Messages['roles']}`, r)).join(', ')

  return (
    <header className="sticky top-0 z-20 flex h-12 items-center justify-between border-b border-zinc-200 bg-white px-3 lg:px-6">
      <button
        type="button"
        onClick={onOpenMenu}
        className="-ml-1 rounded-[3px] p-1.5 text-zinc-600 hover:bg-zinc-100 lg:hidden"
        aria-label={t('nav.openMenu')}
      >
        <MenuIcon size={20} />
      </button>
      <div className="hidden lg:block" />

      <div className="flex items-center gap-3">
        <LanguageSwitch />
        <Menu
          trigger={({ toggle, open }) => (
            <button
              type="button"
              onClick={toggle}
              aria-expanded={open}
              className="flex items-center gap-2 rounded-[3px] py-1 pr-1 pl-2 hover:bg-zinc-100"
            >
              <span className="text-right leading-tight">
                <span className="block max-w-40 truncate text-[13px] font-medium text-zinc-900">{user?.fullName}</span>
                <span className="hidden text-xs text-zinc-500 sm:block">{roleLabel}</span>
              </span>
              <ChevronDown size={14} className="text-zinc-400" />
            </button>
          )}
        >
          {(close) => (
            <>
              <div className="border-b border-zinc-100 px-3 pt-1 pb-2 text-xs text-zinc-500">{user?.userName}</div>
              <MenuItem
                onClick={() => {
                  close()
                  setChangingPassword(true)
                }}
              >
                <KeyRound size={14} className="text-zinc-400" />
                {t('auth.changePassword')}
              </MenuItem>
              <MenuItem onClick={logout}>
                <LogOut size={14} className="text-zinc-400" />
                {t('auth.logout')}
              </MenuItem>
            </>
          )}
        </Menu>
      </div>

      {changingPassword && <ChangePasswordDialog onClose={() => setChangingPassword(false)} />}
    </header>
  )
}
