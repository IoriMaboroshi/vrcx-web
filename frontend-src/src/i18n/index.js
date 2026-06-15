import { reactive, computed } from 'vue'
import en from './en.js'
import zh from './zh.js'

const messages = { en, zh }

const STORAGE_KEY = 'vrcx_locale'

function detectLocale() {
  try {
    const saved = localStorage.getItem(STORAGE_KEY)
    if (saved && messages[saved]) return saved
  } catch {}
  const nav = navigator.language || navigator.userLanguage || ''
  return nav.startsWith('zh') ? 'zh' : 'en'
}

const state = reactive({
  locale: detectLocale(),
})

export function setLocale(locale) {
  if (messages[locale]) {
    state.locale = locale
    try {
      localStorage.setItem(STORAGE_KEY, locale)
    } catch {}
  }
}

export function getLocale() {
  return state.locale
}

export function useI18nSetup() {
  // No-op, just ensures module is loaded
}

/**
 * Resolve a nested key like 'dashboard.title' from the messages.
 * Supports placeholders: 'moved to {where}' → 'moved to 世界'.
 */
function resolve(messages, key, params = {}) {
  const parts = key.split('.')
  let val = messages
  for (const p of parts) {
    if (val && typeof val === 'object') {
      val = val[p]
    } else {
      return key
    }
  }
  if (typeof val !== 'string') return key
  return val.replace(/\{(\w+)\}/g, (_, name) =>
    params[name] !== undefined ? params[name] : `{${name}}`
  )
}

/**
 * Reactive translation function. Use in <script setup>:
 *   const { t } = useI18n()
 *   // in template: {{ t('dashboard.title') }}
 *   // with params: {{ t('dashboard.movedTo', { where: '世界' }) }}
 */
export function useI18n() {
  const t = (key, params = {}) => {
    return resolve(messages[state.locale] || messages.en, key, params)
  }

  const locale = computed({
    get: () => state.locale,
    set: (val) => setLocale(val),
  })

  return { t, locale }
}

export default {
  install(app) {
    app.config.globalProperties.$t = (key, params = {}) => {
      return resolve(messages[state.locale] || messages.en, key, params)
    }
    app.config.globalProperties.$locale = computed(() => state.locale)
    app.config.globalProperties.$setLocale = setLocale
  },
}
