import { createBrowserRouter } from 'react-router'
import { LoginPage } from '@/features/auth/LoginPage'
import { RequireAuth } from '@/features/auth/RequireAuth'
import { AppLayout } from './layout/AppLayout'
import { NotFoundPage } from './NotFoundPage'

// Pages behind login are loaded on demand to keep the first download small.
export const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  {
    element: <RequireAuth />,
    children: [
      {
        element: <AppLayout />,
        children: [
          {
            index: true,
            lazy: async () => ({ Component: (await import('@/features/dashboard/DashboardPage')).DashboardPage }),
          },
          {
            path: 'employees',
            lazy: async () => ({ Component: (await import('@/features/employees/EmployeesPage')).EmployeesPage }),
          },
          {
            path: 'inventory/stock',
            lazy: async () => ({ Component: (await import('@/features/inventory/reports/StockOnHandPage')).StockOnHandPage }),
          },
          {
            path: 'inventory/stock-card',
            lazy: async () => ({ Component: (await import('@/features/inventory/reports/StockCardPage')).StockCardPage }),
          },
          ...(['Receipt', 'Issue', 'Transfer'] as const).flatMap((type) => {
            const path = { Receipt: 'inventory/receipts', Issue: 'inventory/issues', Transfer: 'inventory/transfers' }[type]
            return [
              {
                path,
                lazy: async () => {
                  const { DocumentListPage } = await import('@/features/inventory/documents/DocumentListPage')
                  return { element: <DocumentListPage key={type} type={type} /> }
                },
              },
              {
                path: `${path}/:id`,
                lazy: async () => {
                  const { DocumentPage } = await import('@/features/inventory/documents/DocumentPage')
                  return { element: <DocumentPage key={type} type={type} /> }
                },
              },
            ]
          }),
          {
            path: 'inventory/materials',
            lazy: async () => ({ Component: (await import('@/features/inventory/materials/MaterialsPage')).MaterialsPage }),
          },
          {
            path: 'inventory/material-groups',
            lazy: async () => {
              const { CatalogPage } = await import('@/features/inventory/catalog/CatalogPage')
              return { element: <CatalogPage key="groups" resource="material-groups" section="groups" /> }
            },
          },
          {
            path: 'inventory/units',
            lazy: async () => {
              const { CatalogPage } = await import('@/features/inventory/catalog/CatalogPage')
              return { element: <CatalogPage key="units" resource="units" section="units" /> }
            },
          },
          {
            path: 'inventory/warehouses',
            lazy: async () => ({ Component: (await import('@/features/inventory/warehouses/WarehousesPage')).WarehousesPage }),
          },
          {
            path: 'inventory/labels',
            lazy: async () => ({ Component: (await import('@/features/inventory/labels/LabelPrintPage')).LabelPrintPage }),
          },
          { path: '*', element: <NotFoundPage /> },
        ],
      },
    ],
  },
])
