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
          { path: '*', element: <NotFoundPage /> },
        ],
      },
    ],
  },
])
