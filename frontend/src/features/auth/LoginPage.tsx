import { Eye, EyeOff } from 'lucide-react'
import { useState, type FormEvent } from 'react'
import { useTranslation } from 'react-i18next'
import { Navigate, useLocation, useNavigate } from 'react-router'
import { BrandMark } from '@/app/layout/BrandMark'
import { LanguageSwitch } from '@/app/layout/LanguageSwitch'
import { Button } from '@/components/ui/Button'
import { Field, Input } from '@/components/ui/Field'
import { useAuth } from '@/lib/auth-context'
import { getErrorMessage, getStatus } from '@/lib/errors'

const YEAR = new Date().getFullYear()

export function LoginPage() {
  const { t } = useTranslation()
  const { status, login } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const from = (location.state as { from?: string } | null)?.from ?? '/'

  const [userName, setUserName] = useState('')
  const [password, setPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [submitting, setSubmitting] = useState(false)
  const [error, setError] = useState<string | null>(null)

  if (status === 'authenticated') return <Navigate to={from} replace />

  async function handleSubmit(e: FormEvent) {
    e.preventDefault()
    if (!userName.trim() || !password) return

    setSubmitting(true)
    setError(null)
    try {
      await login(userName.trim(), password)
      navigate(from, { replace: true })
    } catch (err) {
      const code = getStatus(err)
      setError(code === 401 ? t('auth.invalid') : code === 429 ? t('auth.tooMany') : getErrorMessage(err, t))
      setSubmitting(false)
    }
  }

  return (
    <div className="flex min-h-dvh flex-col bg-zinc-100">
      <div className="flex justify-end p-3">
        <LanguageSwitch />
      </div>

      <div className="flex flex-1 items-start justify-center px-4 pt-[8vh]">
        <div className="w-full max-w-[360px]">
          <div className="mb-5 flex items-center gap-2.5">
            <BrandMark size={30} />
            <div>
              <div className="text-base leading-tight font-semibold text-zinc-900">LVV ERP</div>
              <div className="text-xs text-zinc-500">{t('auth.subtitle')}</div>
            </div>
          </div>

          <form
            onSubmit={handleSubmit}
            className="rounded-[4px] border border-zinc-200 bg-white px-5 pt-4 pb-5"
            noValidate
          >
            <h1 className="mb-4 text-[15px] font-semibold text-zinc-900">{t('auth.title')}</h1>

            {error && (
              <div role="alert" className="mb-4 border-l-[3px] border-red-600 bg-red-50 px-3 py-2 text-[13px] text-red-800">
                {error}
              </div>
            )}

            <Field label={t('auth.userName')} htmlFor="userName" className="mb-3">
              <Input
                id="userName"
                autoComplete="username"
                autoCapitalize="none"
                autoFocus
                value={userName}
                onChange={(e) => setUserName(e.target.value)}
                className="h-9"
              />
            </Field>

            <Field label={t('auth.password')} htmlFor="password" className="mb-5">
              <div className="relative">
                <Input
                  id="password"
                  type={showPassword ? 'text' : 'password'}
                  autoComplete="current-password"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  className="h-9 pr-9"
                />
                <button
                  type="button"
                  onClick={() => setShowPassword((s) => !s)}
                  className="absolute inset-y-0 right-0 flex w-9 items-center justify-center text-zinc-400 hover:text-zinc-700"
                  aria-label={showPassword ? t('auth.hidePassword') : t('auth.showPassword')}
                >
                  {showPassword ? <EyeOff size={16} /> : <Eye size={16} />}
                </button>
              </div>
            </Field>

            <Button
              type="submit"
              variant="primary"
              className="h-9 w-full"
              disabled={submitting || !userName.trim() || !password}
            >
              {submitting ? t('auth.submitting') : t('auth.submit')}
            </Button>
          </form>
        </div>
      </div>

      <footer className="py-4 text-center text-xs text-zinc-400">© {YEAR} LVV</footer>
    </div>
  )
}
