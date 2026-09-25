<script setup lang="ts">
import { computed, ref } from 'vue'
import {
  defaultTheme,
  fonts,
  normalize,
  presets,
  readImage,
  replaceTheme,
  theme,
  type FontId,
  type IconSlot,
} from '../composables/useTheme'

const colorFields: { key: 'accent' | 'background' | 'surface' | 'text' | 'danger'; label: string; hint: string }[] = [
  { key: 'accent', label: 'Accent', hint: 'Buttons, highlights, active tab' },
  { key: 'background', label: 'Background', hint: 'Behind everything' },
  { key: 'surface', label: 'Panels', hint: 'Cards, tables, tiles' },
  { key: 'text', label: 'Text', hint: 'Secondary text and borders are derived from it' },
  { key: 'danger', label: 'Alerts', hint: 'Eliminations, sweats' },
]

const iconSlots: { id: IconSlot; label: string }[] = [
  { id: 'live', label: 'Live' },
  { id: 'leaderboard', label: 'Leaderboard' },
  { id: 'history', label: 'History' },
  { id: 'me', label: 'My profile' },
  { id: 'appearance', label: 'Appearance' },
  { id: 'settings', label: 'Settings' },
]

const activePreset = computed(
  () =>
    presets.find((p) => Object.entries(p.colors).every(([k, v]) => theme[k as keyof typeof p.colors].toLowerCase() === v))?.id ?? null,
)

function applyPreset(id: string) {
  const p = presets.find((x) => x.id === id)
  if (p) Object.assign(theme, p.colors)
}

const error = ref<string | null>(null)
const message = ref<string | null>(null)

async function pick(e: Event, maxSize: number, set: (url: string) => void) {
  const input = e.target as HTMLInputElement
  const file = input.files?.[0]
  input.value = ''
  if (!file) return
  error.value = null
  try {
    set(await readImage(file, maxSize))
  } catch (err) {
    error.value = (err as Error).message
  }
}

const setIcon = (slot: IconSlot) => (url: string) => (theme.icons = { ...theme.icons, [slot]: url })
function clearIcon(slot: IconSlot) {
  const next = { ...theme.icons }
  delete next[slot]
  theme.icons = next
}

// ---- Share ----
const importText = ref('')

async function exportTheme() {
  const json = JSON.stringify(theme)
  try {
    await navigator.clipboard.writeText(json)
    message.value = `Theme copied (${Math.round(json.length / 1024)} KB). Paste it in Discord or send it to a friend.`
  } catch {
    importText.value = json
    message.value = 'Copying was blocked; the theme is in the box below: select it and copy it.'
  }
}

function importTheme() {
  error.value = null
  message.value = null
  try {
    const next = normalize(JSON.parse(importText.value))
    if (!next) throw new Error()
    replaceTheme(next)
    importText.value = ''
    message.value = 'Theme imported.'
  } catch {
    error.value = "That text isn't a Fortnite Tracker theme. Paste the whole text a friend exported."
  }
}

function resetAll() {
  replaceTheme(defaultTheme())
  message.value = 'Back to the default look.'
}

const fontIds = Object.keys(fonts) as FontId[]
</script>

<template>
  <div class="page">
    <div class="page-header">
      <div>
        <h1>Appearance</h1>
        <p>Make the app yours: colours, fonts, icons and a background image. Changes apply instantly and are saved on this PC.</p>
      </div>
      <button type="button" class="ghost" @click="resetAll">Reset to default</button>
    </div>

    <p v-if="error" class="alert error" role="alert">{{ error }}</p>
    <p v-else-if="message" class="alert" role="status">{{ message }}</p>

    <section class="card">
      <h2 class="card-title">Themes</h2>
      <div class="presets">
        <button
          v-for="p in presets"
          :key="p.id"
          type="button"
          class="preset"
          :class="{ on: activePreset === p.id }"
          :aria-pressed="activePreset === p.id"
          :style="{ background: p.colors.background, color: p.colors.text, borderColor: activePreset === p.id ? p.colors.accent : undefined }"
          @click="applyPreset(p.id)"
        >
          <span class="mini" :style="{ background: p.colors.surface }">
            <span class="mini-bar" :style="{ background: p.colors.accent }" />
            <span class="mini-line" :style="{ background: p.colors.text }" />
            <span class="mini-line short" :style="{ background: p.colors.text }" />
          </span>
          <span class="preset-name">{{ p.name }}</span>
        </button>
      </div>
    </section>

    <div class="grid">
      <section class="card">
        <h2 class="card-title">Colours</h2>
        <div class="colors">
          <label v-for="f in colorFields" :key="f.key" class="color">
            <input v-model="theme[f.key]" type="color" :aria-label="f.label" />
            <span class="color-text">
              <span class="color-label">{{ f.label }}</span>
              <span class="hint-small">{{ f.hint }}</span>
            </span>
            <code>{{ theme[f.key] }}</code>
          </label>
        </div>
      </section>

      <section class="card">
        <h2 class="card-title">Text &amp; shape</h2>
        <label class="field">
          <span class="field-label">Headings &amp; numbers font</span>
          <select v-model="theme.displayFont">
            <option v-for="id in fontIds" :key="id" :value="id">{{ fonts[id].label }}</option>
          </select>
        </label>
        <label class="field">
          <span class="field-label">Body text font</span>
          <select v-model="theme.bodyFont">
            <option v-for="id in fontIds" :key="id" :value="id">{{ fonts[id].label }}</option>
          </select>
        </label>
        <label class="field">
          <span class="field-label">Size <b>{{ Math.round(theme.scale * 100) }}%</b></span>
          <input v-model.number="theme.scale" type="range" min="0.85" max="1.3" step="0.05" />
        </label>
        <label class="field">
          <span class="field-label">Corner roundness <b>{{ theme.radius }} px</b></span>
          <input v-model.number="theme.radius" type="range" min="0" max="24" step="1" />
        </label>
      </section>

      <section class="card">
        <h2 class="card-title">Icons</h2>
        <p class="hint-small">PNG, JPG, WebP, GIF or SVG. Square images work best; they're scaled to 128 px.</p>
        <div class="icon-row logo-row">
          <span class="icon-preview">
            <img v-if="theme.logo" :src="theme.logo" alt="" />
            <svg v-else viewBox="0 0 24 24" aria-hidden="true"><rect x="3" y="12" width="4.5" height="8" rx="1" /><rect x="9.75" y="8" width="4.5" height="12" rx="1" /><rect x="16.5" y="4" width="4.5" height="16" rx="1" /></svg>
          </span>
          <span class="icon-name">App logo</span>
          <label class="ghost small">Upload<input type="file" accept="image/*" hidden @change="pick($event, 128, (u) => (theme.logo = u))" /></label>
          <button v-if="theme.logo" type="button" class="ghost small" @click="theme.logo = null">Reset</button>
        </div>
        <div v-for="s in iconSlots" :key="s.id" class="icon-row">
          <span class="icon-preview">
            <img v-if="theme.icons[s.id]" :src="theme.icons[s.id]" alt="" />
            <span v-else class="faint">–</span>
          </span>
          <span class="icon-name">{{ s.label }}</span>
          <label class="ghost small">Upload<input type="file" accept="image/*" hidden @change="pick($event, 128, setIcon(s.id))" /></label>
          <button v-if="theme.icons[s.id]" type="button" class="ghost small" @click="clearIcon(s.id)">Reset</button>
        </div>
      </section>

      <section class="card">
        <h2 class="card-title">Background image</h2>
        <div class="wallpaper" :style="{ backgroundImage: theme.wallpaper ? `url(${theme.wallpaper})` : undefined }">
          <span v-if="!theme.wallpaper" class="faint">No background image</span>
        </div>
        <div class="row">
          <label class="ghost">{{ theme.wallpaper ? 'Change image' : 'Choose image' }}<input type="file" accept="image/*" hidden @change="pick($event, 1920, (u) => (theme.wallpaper = u))" /></label>
          <button v-if="theme.wallpaper" type="button" class="ghost" @click="theme.wallpaper = null">Remove</button>
        </div>
        <template v-if="theme.wallpaper">
          <label class="field">
            <span class="field-label">Darken <b>{{ theme.wallpaperDim }}%</b></span>
            <input v-model.number="theme.wallpaperDim" type="range" min="30" max="95" step="5" />
          </label>
          <label class="check">
            <input v-model="theme.glass" type="checkbox" />
            Glass panels (see-through, blurred)
          </label>
        </template>
      </section>
    </div>

    <section class="card">
      <h2 class="card-title">Share a theme</h2>
      <p class="hint-small">Export copies your whole look, images included, as text you can paste in Discord. To use a friend's theme, paste it below.</p>
      <div class="row">
        <button type="button" class="btn-primary" @click="exportTheme">Copy my theme</button>
      </div>
      <textarea v-model="importText" rows="3" placeholder="Paste a theme here…" aria-label="Theme to import" />
      <div class="row">
        <button type="button" class="ghost" :disabled="!importText.trim()" @click="importTheme">Import</button>
      </div>
    </section>
  </div>
</template>

<style scoped>
.ghost {
  background: none;
  border: 1px solid var(--border);
  color: var(--text);
  font-family: var(--display);
  font-weight: 700;
  font-size: 15px;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  padding: 7px 14px;
  border-radius: var(--radius-sm);
  cursor: pointer;
  display: inline-flex;
  align-items: center;
}
.ghost:hover {
  border-color: var(--accent);
  color: var(--accent);
}
.ghost.small {
  font-size: 13px;
  padding: 4px 10px;
}
.alert {
  margin: 0;
  padding: var(--s3) var(--s4);
  border-radius: var(--radius-sm);
  border: 1px solid color-mix(in srgb, var(--accent) 40%, transparent);
  background: color-mix(in srgb, var(--accent) 10%, var(--surface));
}
.alert.error {
  border-color: color-mix(in srgb, var(--danger) 50%, transparent);
  background: color-mix(in srgb, var(--danger) 10%, var(--surface));
}
.grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(420px, 1fr));
  gap: var(--s5);
  align-items: start;
}
.presets {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
  gap: var(--s3);
}
.preset {
  border: 2px solid transparent;
  border-radius: var(--radius);
  padding: var(--s3);
  display: flex;
  flex-direction: column;
  gap: var(--s2);
  text-align: left;
  cursor: pointer;
  box-shadow: 0 0 0 1px var(--border);
}
.preset:hover {
  transform: translateY(-2px);
}
.mini {
  height: 56px;
  border-radius: var(--radius-sm);
  padding: 8px;
  display: flex;
  flex-direction: column;
  gap: 6px;
}
.mini-bar {
  width: 40%;
  height: 8px;
  border-radius: 3px;
}
.mini-line {
  height: 5px;
  border-radius: 3px;
  opacity: 0.5;
}
.mini-line.short {
  width: 60%;
}
.preset-name {
  font-family: var(--display);
  font-weight: 800;
  font-size: 16px;
  letter-spacing: 0.04em;
  text-transform: uppercase;
}
.colors {
  display: flex;
  flex-direction: column;
  gap: var(--s3);
}
.color {
  display: flex;
  align-items: center;
  gap: var(--s3);
  cursor: pointer;
}
.color input[type='color'] {
  width: 44px;
  height: 36px;
  padding: 2px;
  flex: none;
  cursor: pointer;
}
.color-text {
  display: flex;
  flex-direction: column;
  flex: 1;
}
.color-label {
  font-weight: 600;
}
code {
  font-family: Consolas, monospace;
  font-size: 13px;
  color: var(--muted);
}
.hint-small {
  font-size: 13px;
  color: var(--muted);
  margin: 0 0 var(--s3);
}
.color .hint-small {
  margin: 0;
}
.field {
  display: flex;
  flex-direction: column;
  gap: var(--s1);
  margin-bottom: var(--s4);
}
.field-label {
  font-weight: 600;
}
.field-label b {
  color: var(--accent);
  margin-left: var(--s1);
}
input[type='range'] {
  accent-color: var(--accent);
  padding: 0;
  border: none;
  background: none;
}
.icon-row {
  display: flex;
  align-items: center;
  gap: var(--s3);
  padding: var(--s2) 0;
  border-top: 1px solid var(--border);
}
.logo-row {
  border-top: none;
}
.icon-preview {
  width: 36px;
  height: 36px;
  flex: none;
  display: grid;
  place-items: center;
  background: var(--surface-2);
  border-radius: var(--radius-sm);
}
.icon-preview img {
  width: 28px;
  height: 28px;
  object-fit: contain;
}
.icon-preview svg {
  width: 22px;
  height: 22px;
  fill: var(--accent);
}
.icon-name {
  flex: 1;
  font-weight: 600;
}
.wallpaper {
  height: 150px;
  border-radius: var(--radius-sm);
  border: 1px dashed var(--border);
  background-size: cover;
  background-position: center;
  display: grid;
  place-items: center;
  margin-bottom: var(--s3);
}
.row {
  display: flex;
  gap: var(--s2);
  margin-bottom: var(--s3);
}
.check {
  display: flex;
  align-items: center;
  gap: var(--s2);
  cursor: pointer;
}
.check input {
  accent-color: var(--accent);
  width: 16px;
  height: 16px;
}
textarea {
  width: 100%;
  font: 13px Consolas, monospace;
  color: var(--text);
  background: var(--surface-2);
  border: 1px solid var(--border);
  border-radius: var(--radius-sm);
  padding: var(--s2) var(--s3);
  margin-bottom: var(--s3);
  resize: vertical;
  user-select: text;
}
</style>
