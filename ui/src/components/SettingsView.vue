<script setup lang="ts">
import type { OverlayCorner, Settings } from '../bridge'
import ApiKeyForm from './ApiKeyForm.vue'

defineProps<{ settings: Settings }>()
const emit = defineEmits<{
  saveKey: [key: string]
  richPresence: [enabled: boolean]
  notify: [enabled: boolean]
  overlay: [enabled?: boolean, corner?: OverlayCorner]
}>()

const corners: { id: OverlayCorner; label: string }[] = [
  { id: 'TopLeft', label: 'Top left' },
  { id: 'TopRight', label: 'Top right' },
  { id: 'BottomLeft', label: 'Bottom left' },
  { id: 'BottomRight', label: 'Bottom right' },
]

const checked = (e: Event) => (e.target as HTMLInputElement).checked
</script>

<template>
  <div class="page">
    <div class="page-header">
      <div>
        <h1>Settings</h1>
        <p>Everything is stored on this PC only.</p>
      </div>
    </div>
  <section class="settings">
    <ApiKeyForm :has-key="settings.hasApiKey" @save="emit('saveKey', $event)" />

    <div class="panel option">
      <label class="switch">
        <input type="checkbox" :checked="settings.overlay.enabled" @change="emit('overlay', checked($event))" />
        <span class="track" aria-hidden="true" />
        <span class="text">In-game overlay</span>
      </label>
      <p class="hint">
        A small bar with your squad's K/D, and your eliminator after a death. Clicks go through it. Shows while
        Fortnite runs in <strong>Windowed Fullscreen</strong>. Shortcut: Ctrl+Shift+O.
      </p>
      <div class="corners" role="radiogroup" aria-label="Overlay position">
        <button
          v-for="c in corners"
          :key="c.id"
          type="button"
          role="radio"
          :aria-checked="settings.overlay.corner === c.id"
          :class="['corner', c.id, { on: settings.overlay.corner === c.id }]"
          :title="c.label"
          @click="emit('overlay', undefined, c.id)"
        >
          <span class="dot" />
        </button>
      </div>
    </div>

    <div class="panel option">
      <label class="switch">
        <input type="checkbox" :checked="settings.notifyOnElimination" @change="emit('notify', checked($event))" />
        <span class="track" aria-hidden="true" />
        <span class="text">Notify me who eliminated me</span>
      </label>
      <p class="hint">
        A Windows notification with their stats. If none appear during games, turn off Do Not Disturb for games in
        Windows Settings → System → Notifications.
      </p>
    </div>

    <div class="panel option">
      <label class="switch" :class="{ disabled: !settings.richPresence.available }">
        <input
          type="checkbox"
          :checked="settings.richPresence.available && settings.richPresence.enabled"
          :disabled="!settings.richPresence.available"
          @change="emit('richPresence', checked($event))"
        />
        <span class="track" aria-hidden="true" />
        <span class="text">Show my stats on Discord</span>
      </label>
      <p class="hint">
        <template v-if="settings.richPresence.available">
          Shows your mode, party and K/D on your Discord profile while Fortnite runs.
        </template>
        <template v-else>Not available in this version yet.</template>
      </p>
    </div>

    <div class="panel option shortcuts">
      <span class="text">Shortcuts</span>
      <dl>
        <dt>Ctrl+Shift+F</dt><dd>Show or hide this window, even in game</dd>
        <dt>Ctrl+Shift+O</dt><dd>Turn the in-game overlay on or off</dd>
        <dt>F11</dt><dd>Full screen</dd>
      </dl>
    </div>
  </section>
    <p class="about">Fortnite Tracker {{ settings.version }} · Not affiliated with Epic Games · Font: Barlow (SIL OFL)</p>
  </div>
</template>

<style scoped>
.settings {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(420px, 1fr));
  gap: var(--s4);
  align-items: start;
}
.settings > * {
  padding: var(--s5);
}
.shortcuts dl {
  display: grid;
  grid-template-columns: auto 1fr;
  gap: var(--s2) var(--s4);
  margin: var(--s3) 0 0;
  font-size: 14px;
}
.shortcuts dt {
  font-family: var(--display);
  font-weight: 800;
  letter-spacing: 0.04em;
}
.shortcuts dd {
  margin: 0;
  color: var(--muted);
}
.option .hint {
  margin: 6px 0 0 52px;
}
.switch {
  display: flex;
  align-items: center;
  gap: 12px;
  cursor: pointer;
}
.switch.disabled {
  cursor: default;
  opacity: 0.6;
}
.switch input {
  position: absolute;
  opacity: 0;
  width: 1px;
  height: 1px;
}
.track {
  flex: none;
  width: 40px;
  height: 22px;
  border-radius: 11px;
  background: var(--surface-2);
  border: 1px solid var(--border);
  position: relative;
  transition: background 0.2s ease;
}
.track::after {
  content: '';
  position: absolute;
  top: 2px;
  left: 2px;
  width: 16px;
  height: 16px;
  border-radius: 50%;
  background: var(--muted);
  transition: transform 0.2s ease, background 0.2s ease;
}
.switch input:checked + .track {
  background: var(--accent);
  border-color: var(--accent);
}
.switch input:checked + .track::after {
  transform: translateX(18px);
  background: var(--accent-ink);
}
.switch input:focus-visible + .track {
  outline: 2px solid var(--accent);
  outline-offset: 2px;
}
.text {
  font-family: var(--display);
  font-weight: 800;
  font-size: 17px;
  letter-spacing: 0.02em;
}

/* Overlay position picker: a tiny screen with a dot per corner. */
.corners {
  margin: 10px 0 0 52px;
  width: 108px;
  height: 64px;
  border: 1px solid var(--border);
  border-radius: 6px;
  background: var(--bg);
  position: relative;
}
.corner {
  position: absolute;
  width: 30px;
  height: 22px;
  padding: 0;
  border: none;
  background: none;
  display: grid;
  place-items: center;
}
.corner .dot {
  width: 18px;
  height: 10px;
  border-radius: 3px;
  background: var(--surface-2);
  border: 1px solid var(--border);
}
.corner:hover .dot {
  border-color: var(--muted);
}
.corner.on .dot {
  background: var(--accent);
  border-color: var(--accent);
}
.TopLeft { top: 2px; left: 2px; }
.TopRight { top: 2px; right: 2px; }
.BottomLeft { bottom: 2px; left: 2px; }
.BottomRight { bottom: 2px; right: 2px; }

.about {
  color: var(--faint);
  font-size: 12px;
  margin: 4px 0 0;
}
</style>
