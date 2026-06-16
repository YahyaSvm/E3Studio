import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import en from './en.json'
import tr from './tr.json'

i18n.use(initReactI18next).init({
  resources: { en: { translation: en }, tr: { translation: tr } },
  lng: localStorage.getItem('e3studio-lang') || 'en',
  fallbackLng: 'en',
  interpolation: { escapeValue: false },
  returnObjects: true,
})

export function setLanguage(lang: string) {
  i18n.changeLanguage(lang)
  localStorage.setItem('e3studio-lang', lang)
}

export default i18n
