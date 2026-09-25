import { reactive, watch } from 'vue'
import { on, send } from '../bridge'

// Everything the Appearance page can change. Stored by the host in theme.json (see ThemeStore.cs,
// which reads background/surface/text/accent for the window frame and overlay).
export interface Theme {
  version: 1
  background: string
  surface: string
  text: string
  accent: string
  danger: string
  displayFont: FontId
  bodyFont: FontId
  /** UI zoom, 0.85–1.3. */
  scale: number
  /** Corner radius in px, 0–24. */
  radius: number
  /** Data URLs of uploaded images. */
  logo: string | null
  icons: Partial<Record<IconSlot, string>>
  wallpaper: string | null
  /** How much the background colour covers the wallpaper, 30–95 (%). */
  wallpaperDim: number
  /** See-through panels over the wallpaper. */
  glass: boolean
}

export type IconSlot = 'live' | 'leaderboard' | 'history' | 'me' | 'appearance' | 'settings'

export const fonts = {
  barlow: { label: 'Barlow Condensed (default)', css: "'Barlow Condensed', sans-serif" },
  bahnschrift: { label: 'Bahnschrift', css: "'Bahnschrift', sans-serif" },
  bahnschriftCondensed: { label: 'Bahnschrift Condensed', css: "'Bahnschrift Condensed', 'Bahnschrift', sans-serif" },
  segoe: { label: 'Segoe UI', css: "'Segoe UI Variable Text', 'Segoe UI', sans-serif" },
  impact: { label: 'Impact', css: 'Impact, sans-serif' },
  arialBlack: { label: 'Arial Black', css: "'Arial Black', sans-serif" },
  georgia: { label: 'Georgia (serif)', css: 'Georgia, serif' },
  consolas: { label: 'Consolas (monospace)', css: 'Consolas, monospace' },
} as const
export type FontId = keyof typeof fonts

type Colors = Pick<Theme, 'background' | 'surface' | 'text' | 'accent' | 'danger'>

export const presets: { id: string; name: string; colors: Colors }[] = [
  { id: 'default', name: 'Default', colors: { background: '#0b0d12', surface: '#141821', text: '#eef1f6', accent: '#4cb8ff', danger: '#ff5a5f' } },
  { id: 'pink', name: 'Pink', colors: { background: '#15090f', surface: '#221019', text: '#ffeaf3', accent: '#ff5fa2', danger: '#ff6b6b' } },
  { id: 'purple', name: 'Nebula', colors: { background: '#0d0a17', surface: '#171226', text: '#f0ebff', accent: '#a970ff', danger: '#ff6680' } },
  { id: 'toxic', name: 'Toxic', colors: { background: '#080f0a', surface: '#0f1a12', text: '#e9fbe9', accent: '#7dff5a', danger: '#ff5a5a' } },
  { id: 'sunset', name: 'Sunset', colors: { background: '#120b08', surface: '#1f130d', text: '#fff1e8', accent: '#ff8a3d', danger: '#ff4d6a' } },
  { id: 'bus', name: 'Battle Bus', colors: { background: '#0a1633', surface: '#11234d', text: '#eef3ff', accent: '#ffd21f', danger: '#ff5a5f' } },
  { id: 'mono', name: 'Graphite', colors: { background: '#0e0e0e', surface: '#181818', text: '#f2f2f2', accent: '#e6e6e6', danger: '#ff5a5f' } },
  { id: 'light', name: 'Frost (light)', colors: { background: '#eef1f6', surface: '#ffffff', text: '#151a24', accent: '#1f6fe5', danger: '#d92d3a' } },
]

export const defaultTheme = (): Theme => ({
  version: 1,
  ...presets[0]!.colors,
  displayFont: 'barlow',
  bodyFont: 'segoe',
  scale: 1,
  radius: 12,
  logo: null,
  icons: {},
  wallpaper: null,
  wallpaperDim: 80,
  glass: true,
})

const CACHE_KEY = 'ft-theme'

/** The live theme; editing it re-applies instantly and saves shortly after. */
export const theme = reactive<Theme>(readCache() ?? defaultTheme())

/** Checks an imported or stored theme and fills gaps, so a bad file can't break the app. */
export function normalize(input: unknown): Theme | null {
  if (!input || typeof input !== 'object') return null
  const t = input as Partial<Theme>
  const base = defaultTheme()
  const hex = (v: unknown, fallback: string) => (typeof v === 'string' && /^#[0-9a-f]{6}$/i.test(v) ? v : fallback)
  const num = (v: unknown, min: number, max: number, fallback: number) =>
    typeof v === 'number' && Number.isFinite(v) ? Math.min(max, Math.max(min, v)) : fallback
  const img = (v: unknown) => (typeof v === 'string' && v.startsWith('data:image/') ? v : null)
  const icons: Theme['icons'] = {}
  for (const [k, v] of Object.entries(t.icons ?? {})) if (img(v)) icons[k as IconSlot] = v as string
  return {
    version: 1,
    background: hex(t.background, base.background),
    surface: hex(t.surface, base.surface),
    text: hex(t.text, base.text),
    accent: hex(t.accent, base.accent),
    danger: hex(t.danger, base.danger),
    displayFont: t.displayFont && t.displayFont in fonts ? t.displayFont : base.displayFont,
    bodyFont: t.bodyFont && t.bodyFont in fonts ? t.bodyFont : base.bodyFont,
    scale: num(t.scale, 0.85, 1.3, base.scale),
    radius: num(t.radius, 0, 24, base.radius),
    logo: img(t.logo),
    icons,
    wallpaper: img(t.wallpaper),
    wallpaperDim: num(t.wallpaperDim, 30, 95, base.wallpaperDim),
    glass: typeof t.glass === 'boolean' ? t.glass : base.glass,
  }
}

export function replaceTheme(next: Theme) {
  Object.assign(theme, next)
}

// Relative luminance (WCAG) of a #rrggbb colour.
function luminance(hex: string) {
  const [r, g, b] = [1, 3, 5].map((i) => {
    const c = parseInt(hex.slice(i, i + 2), 16) / 255
    return c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4
  })
  return 0.2126 * r! + 0.7152 * g! + 0.0722 * b!
}

const withAlpha = (hex: string, alpha: number) =>
  `rgba(${parseInt(hex.slice(1, 3), 16)}, ${parseInt(hex.slice(3, 5), 16)}, ${parseInt(hex.slice(5, 7), 16)}, ${alpha})`

function apply(t: Theme) {
  const root = document.documentElement.style
  const light = luminance(t.background) > 0.5
  const glass = t.glass && !!t.wallpaper
  root.setProperty('--bg', t.background)
  root.setProperty('--surface', glass ? withAlpha(t.surface, 0.82) : t.surface)
  root.setProperty('--text', t.text)
  root.setProperty('--accent', t.accent)
  // Text on accent-coloured buttons: black or white, whichever reads better.
  root.setProperty('--accent-ink', luminance(t.accent) > 0.35 ? '#0b0d12' : '#ffffff')
  root.setProperty('--danger', t.danger)
  root.setProperty('--display', fonts[t.displayFont].css)
  root.setProperty('--body', fonts[t.bodyFont].css)
  root.setProperty('--radius', `${t.radius}px`)
  root.setProperty('--wallpaper', t.wallpaper ? `url("${t.wallpaper}")` : 'none')
  root.setProperty('--wallpaper-dim', `${t.wallpaperDim}%`)
  root.setProperty('color-scheme', light ? 'light' : 'dark')
  // Rarity/rank colours are tuned for dark backgrounds; use darker versions on light ones.
  for (const [name, dark, lightValue] of [
    ['--rarity-common', '#b4bac6', '#6b7280'],
    ['--rarity-uncommon', '#5fd86b', '#1d9a3c'],
    ['--rarity-rare', '#4cb8ff', '#1b78cf'],
    ['--rarity-epic', '#c07cff', '#8a3fd6'],
    ['--rarity-legendary', '#ffb83d', '#c07400'],
    ['--live', '#5fd86b', '#1d9a3c'],
    ['--tier-Silver', '#c3cad6', '#7a8494'],
    ['--tier-Gold', '#f5c542', '#b8860b'],
    ['--tier-Platinum', '#52d6c8', '#12978b'],
    ['--tier-Diamond', '#5b9dff', '#2f6fe0'],
    ['--tier-Elite', '#e4e9f2', '#7c8799'],
    ['--tier-Unranked', '#6b7282', '#9aa1b0'],
  ] as const)
    root.setProperty(name, light ? lightValue : dark)
  document.documentElement.style.zoom = String(t.scale)
  document.documentElement.classList.toggle('glass', glass)
}

function readCache(): Theme | null {
  try {
    const raw = localStorage.getItem(CACHE_KEY)
    return raw ? normalize(JSON.parse(raw)) : null
  } catch {
    return null
  }
}

function writeCache(t: Theme) {
  try {
    localStorage.setItem(CACHE_KEY, JSON.stringify(t))
  } catch {
    // Storage full or unavailable (large wallpaper): the host copy is the one that matters.
  }
}

let loaded = false
let saveTimer: number | undefined

/** Call once at startup: applies the cached theme now, then the host's saved one, and saves edits. */
export function initTheme() {
  apply(theme)
  on('theme', (saved) => {
    loaded = true
    replaceTheme(normalize(saved) ?? defaultTheme())
  })
  watch(
    theme,
    (t) => {
      apply(t)
      writeCache(t)
      // Don't echo the host's own copy back before it has been received.
      if (!loaded) return
      window.clearTimeout(saveTimer)
      saveTimer = window.setTimeout(() => send({ type: 'setTheme', theme: JSON.parse(JSON.stringify(t)) }), 400)
    },
    { deep: true },
  )
}

/** Reads an image file and scales it down (icons to 128px, wallpapers to 1920px) as a data URL. */
export function readImage(file: File, maxSize: number): Promise<string> {
  return new Promise((resolve, reject) => {
    if (!file.type.startsWith('image/')) return reject(new Error('Choose an image file (PNG, JPG, WebP, GIF or SVG).'))
    const reader = new FileReader()
    reader.onerror = () => reject(new Error('Could not read that file.'))
    reader.onload = () => {
      const url = reader.result as string
      // SVGs stay vector (and small).
      if (file.type === 'image/svg+xml') return file.size < 200_000 ? resolve(url) : reject(new Error('That SVG is too large (max 200 KB).'))
      const img = new Image()
      img.onerror = () => reject(new Error('That image could not be opened.'))
      img.onload = () => {
        const ratio = Math.min(1, maxSize / Math.max(img.width, img.height))
        const canvas = document.createElement('canvas')
        canvas.width = Math.round(img.width * ratio)
        canvas.height = Math.round(img.height * ratio)
        canvas.getContext('2d')!.drawImage(img, 0, 0, canvas.width, canvas.height)
        resolve(canvas.toDataURL('image/webp', 0.88))
      }
      img.src = url
    }
    reader.readAsDataURL(file)
  })
}
