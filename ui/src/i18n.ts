import { ref, watch } from 'vue'
import { on, send } from './bridge'
import { FR } from './i18n-fr'

export type Lang = 'en' | 'fr'
export type LangChoice = Lang | 'auto'

const systemLang = (): Lang => (navigator.language?.toLowerCase().startsWith('fr') ? 'fr' : 'en')

/** The language on screen. Follows the setting from the host (or Windows when it's "auto"). */
export const lang = ref<Lang>(systemLang())
/** What the user picked in Settings. */
export const langChoice = ref<LangChoice>('auto')

/**
 * Translates English text. Keys are the English text itself, so untranslated text still reads
 * fine. Placeholders look like {name}: t('{n} matches', { n: 3 }).
 */
export function t(english: string, vars?: Record<string, string | number>): string {
  let s = lang.value === 'fr' ? (FR[english] ?? english) : english
  if (vars) for (const [k, v] of Object.entries(vars)) s = s.split(`{${k}}`).join(String(v))
  return s
}

/** Singular or plural English key, then translated: tp(3, '{n} match', '{n} matches'). */
export function tp(n: number, one: string, many: string, vars?: Record<string, string | number>): string {
  return t(n === 1 ? one : many, { n, ...vars })
}

// Rank, mode and track names arrive in English from the host; these swap the words.
const WORDS: [string, string][] = [
  ['Zero Build', 'Zéro construction'], ['Unranked', 'Non classé'], ['Platinum', 'Platine'],
  ['Creative', 'Créatif'], ['Diamond', 'Diamant'], ['Ranked', 'Classé'], ['Silver', 'Argent'],
  ['Squads', 'Section'], ['Tournament', 'Tournoi'], ['Build', 'Construction'], ['Elite', 'Élite'],
  ['Trios', 'Trio'], ['Duos', 'Duo'], ['Gold', 'Or'], ['Rank ', 'Rang '],
]

/** A rank, mode or track name in the current language. */
export function tn(english: string | null | undefined): string {
  if (!english) return ''
  if (lang.value !== 'fr') return english
  let s = english
  for (const [en, fr] of WORDS) s = s.split(en).join(fr)
  return s
}

/** A shortcut as shown on screen: "Ctrl+Shift+F", or "Ctrl+Maj+F" in French. */
export const keysLabel = (keys: string) => (lang.value === 'fr' ? keys.replace('Shift', 'Maj') : keys)

/** The locale for dates and times. */
export const locale = () => (lang.value === 'fr' ? 'fr-FR' : 'en-GB')

export function setLanguage(choice: LangChoice) {
  langChoice.value = choice
  lang.value = choice === 'auto' ? systemLang() : choice
  send({ type: 'setLanguage', lang: choice })
}

export function initI18n() {
  on('settings', (s) => {
    langChoice.value = s.language
    lang.value = s.effectiveLanguage
  })
  const apply = () => (document.documentElement.lang = lang.value)
  apply()
  watch(lang, apply)
}
