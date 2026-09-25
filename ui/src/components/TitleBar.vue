<script setup lang="ts">
import { send } from '../bridge'

defineProps<{ overlayOn: boolean }>()
const emit = defineEmits<{ toggleOverlay: [] }>()

// Only the empty part of the bar drags the window; buttons stop the event themselves.
function startDrag(e: MouseEvent) {
  if (e.button === 0) send({ type: 'window', action: 'drag' })
}
</script>

<template>
  <header class="titlebar" @mousedown="startDrag">
    <svg class="logo" viewBox="0 0 24 24" aria-hidden="true">
      <rect x="3" y="12" width="4.5" height="8" rx="1" />
      <rect x="9.75" y="8" width="4.5" height="12" rx="1" />
      <rect x="16.5" y="4" width="4.5" height="16" rx="1" />
    </svg>
    <span class="name">Fortnite Tracker</span>

    <div class="buttons" @mousedown.stop>
      <button
        type="button"
        class="icon"
        :class="{ on: overlayOn }"
        :aria-pressed="overlayOn"
        title="In-game overlay (Ctrl+Shift+O)"
        @click="emit('toggleOverlay')"
      >
        <svg viewBox="0 0 24 24" aria-hidden="true">
          <rect x="3" y="5" width="18" height="14" rx="2" fill="none" stroke="currentColor" stroke-width="1.8" />
          <rect x="12" y="7.5" width="6.5" height="4" rx="1" />
        </svg>
      </button>
      <button type="button" class="icon" title="Minimize" @click="send({ type: 'window', action: 'minimize' })">
        <svg viewBox="0 0 24 24" aria-hidden="true"><rect x="6" y="11.2" width="12" height="1.6" rx="0.8" /></svg>
      </button>
      <button
        type="button"
        class="icon close"
        title="Close to tray (keeps tracking)"
        @click="send({ type: 'window', action: 'close' })"
      >
        <svg viewBox="0 0 24 24" aria-hidden="true">
          <path d="M7 7l10 10M17 7L7 17" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" />
        </svg>
      </button>
    </div>
  </header>
</template>

<style scoped>
.titlebar {
  height: 40px;
  flex: none;
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 0 4px 0 14px;
  cursor: default;
}
.logo {
  width: 18px;
  height: 18px;
  fill: var(--accent);
}
.name {
  font-family: var(--display);
  font-weight: 800;
  font-size: 17px;
  letter-spacing: 0.06em;
  text-transform: uppercase;
}
.buttons {
  margin-left: auto;
  display: flex;
  gap: 2px;
}
.icon {
  width: 34px;
  height: 30px;
  padding: 0;
  display: grid;
  place-items: center;
  background: none;
  border: none;
  border-radius: 6px;
  color: var(--muted);
}
.icon svg {
  width: 17px;
  height: 17px;
  fill: currentColor;
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
