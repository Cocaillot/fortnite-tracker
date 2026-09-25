<script setup lang="ts">
import { t } from '../i18n'
import { ref } from 'vue'
import { send } from '../bridge'
import { theme } from '../composables/useTheme'

defineProps<{ overlayOn: boolean; maximized: boolean; fullscreen: boolean }>()
const emit = defineEmits<{ toggleOverlay: []; search: [name: string] }>()

const query = ref('')

// Only the empty part of the bar drags the window; controls stop the event themselves.
function startDrag(e: MouseEvent) {
  if (e.button === 0 && e.detail === 1) send({ type: 'window', action: 'drag' })
}

function submit() {
  const name = query.value.trim()
  if (!name) return
  emit('search', name)
  query.value = ''
}
</script>

<template>
  <header class="titlebar" @mousedown="startDrag" @dblclick="send({ type: 'window', action: 'maximize' })">
    <div class="brand">
      <img v-if="theme.logo" class="logo custom" :src="theme.logo" alt="" />
      <svg v-else class="logo" viewBox="0 0 24 24" aria-hidden="true">
        <rect x="3" y="12" width="4.5" height="8" rx="1" />
        <rect x="9.75" y="8" width="4.5" height="12" rx="1" />
        <rect x="16.5" y="4" width="4.5" height="16" rx="1" />
      </svg>
      <span class="name">Fortnite Tracker</span>
    </div>

    <form class="search" role="search" @mousedown.stop @dblclick.stop @submit.prevent="submit">
      <svg viewBox="0 0 24 24" aria-hidden="true">
        <circle cx="11" cy="11" r="6.5" fill="none" stroke="currentColor" stroke-width="1.8" />
        <path d="M16 16l4.5 4.5" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
      </svg>
      <input v-model="query" :placeholder="t('Search a player (Epic, PSN or Xbox name) and press Enter')" :aria-label="t('Search a player')" />
    </form>

    <div class="buttons" @mousedown.stop @dblclick.stop>
      <button
        type="button"
        class="icon"
        :class="{ on: overlayOn }"
        :aria-pressed="overlayOn"
        :title="t('In-game overlay (Ctrl+Shift+O)')"
        @click="emit('toggleOverlay')"
      >
        <svg viewBox="0 0 24 24" aria-hidden="true">
          <rect x="3" y="5" width="18" height="14" rx="2" fill="none" stroke="currentColor" stroke-width="1.8" />
          <rect x="12" y="7.5" width="6.5" height="4" rx="1" />
        </svg>
      </button>
      <button type="button" class="icon" :title="fullscreen ? t('Exit full screen (F11)') : t('Full screen (F11)')" @click="send({ type: 'window', action: 'fullscreen' })">
        <svg viewBox="0 0 24 24" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round">
          <path v-if="!fullscreen" d="M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5" />
          <path v-else d="M9 4v5H4M15 4v5h5M9 20v-5H4M15 20v-5h5" />
        </svg>
      </button>
      <span class="sep" aria-hidden="true" />
      <button type="button" class="icon" :title="t('Minimize')" @click="send({ type: 'window', action: 'minimize' })">
        <svg viewBox="0 0 24 24" aria-hidden="true"><rect x="6" y="11.2" width="12" height="1.6" rx="0.8" /></svg>
      </button>
      <button type="button" class="icon" :title="maximized ? t('Restore') : t('Maximize')" @click="send({ type: 'window', action: 'maximize' })">
        <svg viewBox="0 0 24 24" aria-hidden="true" fill="none" stroke="currentColor" stroke-width="1.6">
          <rect v-if="!maximized" x="6" y="6" width="12" height="12" rx="1.5" />
          <template v-else>
            <rect x="5" y="8.5" width="10.5" height="10.5" rx="1.5" />
            <path d="M8.5 8.5V6.5a1.5 1.5 0 0 1 1.5-1.5h7.5A1.5 1.5 0 0 1 19 6.5V14a1.5 1.5 0 0 1-1.5 1.5h-2" />
          </template>
        </svg>
      </button>
      <button type="button" class="icon close" :title="t('Close to tray (keeps tracking)')" @click="send({ type: 'window', action: 'close' })">
        <svg viewBox="0 0 24 24" aria-hidden="true">
          <path d="M7 7l10 10M17 7L7 17" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
        </svg>
      </button>
    </div>
  </header>
</template>

<style scoped>
.titlebar {
  height: 52px;
  flex: none;
  display: grid;
  grid-template-columns: 240px minmax(0, 1fr) auto;
  align-items: center;
  gap: var(--s4);
  padding: 0 var(--s2) 0 var(--s5);
  border-bottom: 1px solid var(--border);
  background: var(--chrome);
  cursor: default;
}
.brand {
  display: flex;
  align-items: center;
  gap: var(--s3);
}
.logo {
  width: 22px;
  height: 22px;
  fill: var(--accent);
}
.logo.custom {
  width: 28px;
  height: 28px;
  object-fit: contain;
  border-radius: 6px;
}
.name {
  font-family: var(--display);
  font-weight: 800;
  font-size: 20px;
  letter-spacing: 0.06em;
  text-transform: uppercase;
}
.search {
  justify-self: center;
  width: min(560px, 100%);
  display: flex;
  align-items: center;
  gap: var(--s2);
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius-sm);
  padding: 0 var(--s3);
}
.search:focus-within {
  border-color: var(--accent);
}
.search svg {
  width: 18px;
  height: 18px;
  color: var(--faint);
  flex: none;
}
.search input {
  border: none;
  background: none;
  padding: 9px 0;
}
.search input:focus-visible {
  outline: none;
}
.buttons {
  display: flex;
  align-items: center;
  gap: 2px;
}
.sep {
  width: 1px;
  height: 22px;
  background: var(--border);
  margin: 0 var(--s2);
}
.icon {
  width: 44px;
  height: 36px;
  padding: 0;
  display: grid;
  place-items: center;
  background: none;
  border: none;
  border-radius: var(--radius-sm);
  color: var(--muted);
}
.icon svg {
  width: 18px;
  height: 18px;
  fill: currentColor;
}
.icon svg[fill='none'] {
  fill: none;
}
.icon:hover {
  background: var(--surface-2);
  color: var(--text);
}
.icon.on {
  color: var(--accent);
}
.icon.close:hover {
  background: #c42b1c;
  color: #fff;
}
</style>
