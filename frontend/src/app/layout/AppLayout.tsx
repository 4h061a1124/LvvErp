import { useState } from 'react'
import { Outlet, useNavigation } from 'react-router'
import { Sidebar } from './Sidebar'
import { Topbar } from './Topbar'

export function AppLayout() {
  const [menuOpen, setMenuOpen] = useState(false)
  // Pages are lazy-loaded; show a thin bar while the next one is being fetched.
  const navigating = useNavigation().state === 'loading'

  return (
    <div className="min-h-dvh">
      {navigating && <div className="animate-progress fixed inset-x-0 top-0 z-50 h-0.5 origin-left bg-brand-600" />}
      <Sidebar open={menuOpen} onClose={() => setMenuOpen(false)} />
      <div className="lg:pl-56">
        <Topbar onOpenMenu={() => setMenuOpen(true)} />
        <main className="mx-auto max-w-[1400px] px-3 py-4 sm:px-4 lg:px-6 lg:py-5">
          <Outlet />
        </main>
      </div>
    </div>
  )
}
