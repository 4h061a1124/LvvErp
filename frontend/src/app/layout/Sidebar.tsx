import clsx from 'clsx'
import {
  ArrowDownToLine,
  ArrowLeftRight,
  ArrowUpFromLine,
  BarChart3,
  Boxes,
  ClipboardCheck,
  ClipboardList,
  FileText,
  FolderTree,
  KeyRound,
  LayoutGrid,
  Package,
  Ruler,
  Settings,
  ShoppingCart,
  Truck,
  Users,
  Warehouse,
  X,
} from 'lucide-react'
import type { ComponentType } from 'react'
import { useTranslation } from 'react-i18next'
import { NavLink } from 'react-router'
import { useApprovalInbox } from '@/features/purchasing/api'
import { useAuth } from '@/lib/auth-context'
import { Permissions, type Permission } from '@/lib/permissions'
import type { Messages } from '@/locales/vi'
import { BrandMark } from './BrandMark'

type NavKey = keyof Messages['nav']

interface NavItem {
  to: string
  label: NavKey
  icon: ComponentType<{ size?: number; strokeWidth?: number }>
  end?: boolean
  /** Hidden without this permission. */
  permission?: Permission
  /** Shows the number of documents waiting for the user's approval. */
  approvalCount?: boolean
}

const sections: { title?: NavKey; items: NavItem[] }[] = [
  { items: [{ to: '/', label: 'overview', icon: LayoutGrid, end: true }] },
  { title: 'hr', items: [{ to: '/employees', label: 'employees', icon: Users }] },
  {
    title: 'inventory',
    items: [
      { to: '/inventory/stock', label: 'stock', icon: Boxes },
      { to: '/inventory/receipts', label: 'receipts', icon: ArrowDownToLine },
      { to: '/inventory/issues', label: 'issues', icon: ArrowUpFromLine },
      { to: '/inventory/transfers', label: 'transfers', icon: ArrowLeftRight },
      { to: '/inventory/stock-card', label: 'stockCard', icon: ClipboardList },
    ],
  },
  {
    title: 'inventoryCatalog',
    items: [
      { to: '/inventory/materials', label: 'materials', icon: Package },
      { to: '/inventory/material-groups', label: 'materialGroups', icon: FolderTree },
      { to: '/inventory/units', label: 'units', icon: Ruler },
      { to: '/inventory/warehouses', label: 'warehouses', icon: Warehouse },
    ],
  },
  {
    title: 'purchasing',
    items: [
      { to: '/purchasing/approvals', label: 'approvals', icon: ClipboardCheck, approvalCount: true, permission: Permissions.PurchasingApproveRequests },
      { to: '/purchasing/requests', label: 'purchaseRequests', icon: FileText, permission: Permissions.PurchasingCreateRequests },
      { to: '/purchasing/orders', label: 'purchaseOrders', icon: ShoppingCart, permission: Permissions.PurchasingView },
      { to: '/purchasing/suppliers', label: 'suppliers', icon: Truck, permission: Permissions.PurchasingView },
      { to: '/purchasing/reports', label: 'purchasingReports', icon: BarChart3, permission: Permissions.PurchasingView },
      { to: '/purchasing/settings', label: 'purchasingSettings', icon: Settings, permission: Permissions.PurchasingManageSettings },
    ],
  },
  {
    title: 'admin',
    items: [{ to: '/admin/users', label: 'users', icon: KeyRound, permission: Permissions.UsersManage }],
  },
]

export function Sidebar({ open, onClose }: { open: boolean; onClose: () => void }) {
  const { t } = useTranslation()
  const { can } = useAuth()
  const isApprover = can(Permissions.PurchasingApproveRequests) || can(Permissions.PurchasingApproveOrders)
  const inbox = useApprovalInbox(isApprover)
  const waiting = (inbox.data?.requests.length ?? 0) + (inbox.data?.orders.length ?? 0)

  const visible = sections
    .map((section) => ({
      ...section,
      items: section.items.filter((item) => (item.approvalCount ? isApprover : !item.permission || can(item.permission))),
    }))
    .filter((section) => section.items.length > 0)

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
          'fixed inset-y-0 left-0 z-40 flex w-60 flex-col border-r border-zinc-200 bg-white transition-transform duration-150 print:hidden',
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
          {visible.map((section, i) => (
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
                  {item.approvalCount && waiting > 0 && (
                    <span className="tabular ml-auto rounded-[3px] bg-amber-100 px-1.5 text-xs font-medium text-amber-800">{waiting}</span>
                  )}
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
