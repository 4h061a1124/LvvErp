import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import en from '@/locales/en'
import vi from '@/locales/vi'
import { storage } from './storage'

export const LANGUAGES = ['vi', 'en'] as const
export type Language = (typeof LANGUAGES)[number]

const LANGUAGE_KEY = 'lvverp.lang'

function initialLanguage(): Language {
  const saved = storage.get(LANGUAGE_KEY)
  return saved === 'en' ? 'en' : 'vi'
}

i18n.use(initReactI18next).init({
  resources: { vi: { translation: vi }, en: { translation: en } },
  lng: initialLanguage(),
  fallbackLng: 'vi',
  interpolation: { escapeValue: false },
})

document.documentElement.lang = i18n.language
i18n.on('languageChanged', (lng) => {
  storage.set(LANGUAGE_KEY, lng)
  document.documentElement.lang = lng
})

export default i18n
