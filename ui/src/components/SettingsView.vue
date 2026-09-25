<script setup lang="ts">
import { t, langChoice, setLanguage, type LangChoice } from '../i18n'
import type { OverlayCorner, Settings } from '../bridge'
import { ref } from 'vue'
import ApiKeyForm from './ApiKeyForm.vue'

const props = defineProps<{ settings: Settings; recapResult: string | null }>()
const webhook = ref('')
const emit = defineEmits<{
  saveKey: [key: string]
  richPresence: [enabled: boolean]
  notify: [enabled: boolean]
  notifyRanks: [enabled: boolean]
  discordRecap: [url: string | null, autoPost: boolean]
  postRecap: []
  overlay: [enabled?: boolean, corner?: OverlayCorner]
}>()

const languages: { id: LangChoice; label: string }[] = [
  { id: 'auto', label: 'Same as Windows' },
  { id: 'en', label: 'English' },
  { id: 'fr', label: 'Français' },
]

// Labels are English keys, translated where shown.
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
        <h1>{{ t('Settings') }}</h1>
        <p>{{ t('Everything is stored on this PC only.') }}</p>
      </div>
    </div>
  <section class="settings">
    <ApiKeyForm :has-key="settings.hasApiKey" @save="emit('saveKey', $event)" />

    <div class="panel option">
      <span class="text">{{ t('Language') }}</span>
      <div class="seg lang" role="radiogroup" :aria-label="t('Language')">
        <button
          v-for="l in languages"
          :key="l.id"
          type="button"
          role="radio"
          :aria-checked="langChoice === l.id"
          :class="{ on: langChoice === l.id }"
          @click="setLanguage(l.id)"
        >
          {{ l.id === 'auto' ? t(l.label) : l.label }}
        </button>
      </div>
      <p class="hint">{{ t('Also used for notifications, the overlay and Discord recaps.') }}</p>
    </div>

    <div class="panel option">
      <label class="switch">
        <input type="checkbox" :checked="settings.overlay.enabled" @change="emit('overlay', checked($event))" />
        <span class="track" aria-hidden="true" />
        <span class="text">{{ t('In-game overlay') }}</span>
      </label>
      <p class="hint">
        {{ t("A small bar with your squad's K/D, and your eliminator after a death. Clicks go through it. Shows while Fortnite runs in") }}
        <strong>{{ t('Windowed Fullscreen') }}</strong>. {{ t('Shortcut: Ctrl+Shift+O.') }}
      </p>
      <div class="corners" role="radiogroup" :aria-label="t('Overlay position')">
        <button
          v-for="c in corners"
          :key="c.id"
          type="button"
          role="radio"
          :aria-checked="settings.overlay.corner === c.id"
          :class="['corner', c.id, { on: settings.overlay.corner === c.id }]"
          :title="t(c.label)"
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
        <span class="text">{{ t('Notify me who eliminated me') }}</span>
      </label>
      <p class="hint">
        {{ t('A Windows notification with their stats. If none appear during games, turn off Do Not Disturb for games in Windows Settings → System → Notifications.') }}
      </p>
    </div>

    <div class="panel option">
      <label class="switch">
        <input type="checkbox" :checked="settings.notifyRankChanges" @change="emit('notifyRanks', checked($event))" />
        <span class="track" aria-hidden="true" />
        <span class="text">{{ t('Rank change alerts') }}</span>
      </label>
      <p class="hint">{{ t('When you rank up or down, and when a friend or party member ranks up.') }}</p>
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
        <span class="text">{{ t('Show my stats on Discord') }}</span>
      </label>
      <p class="hint">
        <template v-if="settings.richPresence.available">
          {{ t('Shows your mode, party and K/D on your Discord profile while Fortnite runs.') }}
        </template>
        <template v-else>{{ t('Not available in this version yet.') }}</template>
      </p>
    </div>

    <div class="panel option recap">
      <span class="text">{{ t('Session recap on Discord') }}</span>
      <p class="hint flush">
        {{ t('Posts your matches, wins, kills, K/D and rank changes to a Discord channel. In your server: Channel settings → Integrations → Webhooks → New webhook → Copy webhook URL, then paste it here.') }}
      </p>
      <form class="row" @submit.prevent="emit('discordRecap', webhook, props.settings.discordRecap.autoPost); webhook = ''">
        <input
          v-model="webhook"
          type="password"
          autocomplete="off"
          :placeholder="settings.discordRecap.hasWebhook ? t('Webhook saved. Paste a new one to replace it') : 'https://discord.com/api/webhooks/…'"
          :aria-label="t('Discord webhook link')"
        />
        <button type="submit" class="btn-primary" :disabled="!webhook.trim()">{{ t('Save') }}</button>
      </form>
      <template v-if="settings.discordRecap.hasWebhook">
        <label class="switch">
          <input type="checkbox" :checked="settings.discordRecap.autoPost" @change="emit('discordRecap', null, checked($event))" />
          <span class="track" aria-hidden="true" />
          <span class="text small-text">{{ t('Post automatically when I close Fortnite') }}</span>
        </label>
        <div class="row">
          <button type="button" class="ghost" @click="emit('postRecap')">{{ t('Post last session now') }}</button>
          <button type="button" class="ghost" @click="emit('discordRecap', '', false)">{{ t('Remove webhook') }}</button>
        </div>
      </template>
      <p v-if="recapResult" class="result">{{ recapResult }}</p>
    </div>

    <div class="panel option shortcuts">
      <span class="text">{{ t('Shortcuts') }}</span>
      <dl>
        <dt>{{ t('Ctrl+Shift+F') }}</dt><dd>{{ t('Show or hide this window, even in game') }}</dd>
        <dt>{{ t('Ctrl+Shift+O') }}</dt><dd>{{ t('Turn the in-game overlay on or off') }}</dd>
        <dt>F11</dt><dd>{{ t('Full screen') }}</dd>
      </dl>
    </div>
  </section>
    <p class="about">Fortnite Tracker {{ settings.version }} · {{ t('Not affiliated with Epic Games') }} · {{ t('Font: Barlow (SIL OFL)') }}</p>
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
.recap .hint.flush {
  margin: var(--s2) 0 var(--s3);
}
.recap .row {
  margin: var(--s3) 0 0;
}
.small-text {
  font-size: 15px;
}
.ghost {
  background: none;
  border: 1px solid var(--border);
  color: var(--text);
  font-family: var(--display);
  font-weight: 700;
  font-size: 14px;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  padding: 6px 12px;
}
.ghost:hover {
  border-color: var(--accent);
  color: var(--accent);
}
.result {
  margin: var(--s3) 0 0;
  color: var(--accent);
  font-weight: 600;
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
.lang {
  display: flex;
  width: fit-content;
  margin-top: 10px;
}
</style>
