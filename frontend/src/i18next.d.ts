import 'i18next'
import type { Messages } from './locales/vi'

declare module 'i18next' {
  interface CustomTypeOptions {
    resources: { translation: Messages }
  }
}
