import { useTranslation } from 'react-i18next'
import { Link } from 'react-router'

export function NotFoundPage() {
  const { t } = useTranslation()
  return (
    <div className="py-16 text-center">
      <div className="text-4xl font-semibold text-zinc-300">404</div>
      <p className="mt-2 text-sm text-zinc-600">{t('errors.pageNotFound')}</p>
      <Link to="/" className="mt-4 inline-block text-sm text-brand-700 hover:underline">
        {t('errors.backHome')}
      </Link>
    </div>
  )
}
