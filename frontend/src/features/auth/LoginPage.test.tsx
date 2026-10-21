import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { AxiosError, AxiosHeaders, type AxiosResponse } from 'axios'
import { createMemoryRouter, RouterProvider } from 'react-router'
import { describe, expect, it, vi } from 'vitest'
import { AuthContext, type AuthContextValue } from '@/lib/auth-context'
import { LoginPage } from './LoginPage'

function renderLogin(login: AuthContextValue['login']) {
  const auth: AuthContextValue = { status: 'anonymous', user: null, login, logout: vi.fn(), can: () => false }
  const router = createMemoryRouter(
    [
      { path: '/login', element: <LoginPage /> },
      { path: '/', element: <div>home page</div> },
    ],
    { initialEntries: ['/login'] },
  )
  render(
    <AuthContext.Provider value={auth}>
      <RouterProvider router={router} />
    </AuthContext.Provider>,
  )
}

function httpError(status: number) {
  const response = { status, data: {}, statusText: '', headers: {}, config: { headers: new AxiosHeaders() } } as AxiosResponse
  return new AxiosError('failed', 'ERR_BAD_REQUEST', undefined, undefined, response)
}

describe('LoginPage', () => {
  it('keeps the button disabled until both fields are filled', async () => {
    renderLogin(vi.fn())
    const submit = screen.getByRole('button', { name: 'Đăng nhập' })
    expect(submit).toBeDisabled()

    await userEvent.type(screen.getByLabelText('Tên đăng nhập'), 'admin')
    expect(submit).toBeDisabled()
    await userEvent.type(screen.getByLabelText('Mật khẩu'), 'secret')
    expect(submit).toBeEnabled()
  })

  it('signs in with a trimmed username and goes to the home page', async () => {
    const login = vi.fn().mockResolvedValue(undefined)
    renderLogin(login)

    await userEvent.type(screen.getByLabelText('Tên đăng nhập'), '  admin ')
    await userEvent.type(screen.getByLabelText('Mật khẩu'), 'Admin@123')
    await userEvent.click(screen.getByRole('button', { name: 'Đăng nhập' }))

    expect(login).toHaveBeenCalledWith('admin', 'Admin@123')
    expect(await screen.findByText('home page')).toBeInTheDocument()
  })

  it.each([
    [401, 'Sai tên đăng nhập hoặc mật khẩu.'],
    [429, 'Đăng nhập sai quá nhiều lần. Vui lòng thử lại sau ít phút.'],
  ])('shows a clear message for HTTP %i', async (status, message) => {
    renderLogin(vi.fn().mockRejectedValue(httpError(status)))

    await userEvent.type(screen.getByLabelText('Tên đăng nhập'), 'admin')
    await userEvent.type(screen.getByLabelText('Mật khẩu'), 'wrong')
    await userEvent.click(screen.getByRole('button', { name: 'Đăng nhập' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(message)
  })
})
