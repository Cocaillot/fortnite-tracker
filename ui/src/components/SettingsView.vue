<script setup lang="ts">
import { t, keysLabel, langChoice, setLanguage, type LangChoice } from '../i18n'
import type { OverlayCorner, Settings } from '../bridge'
import { ref } from 'vue'
import ApiKeyForm from './ApiKeyForm.vue'
import HotkeyInput from './HotkeyInput.vue'
import { on, send } from '../bridge'
import { onUnmounted } from 'vue'

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

const setHotkey = (which: 'window' | 'overlay', keys: string) => send({ type: 'setHotkeys', [which]: keys })

// ---- Export, backup, restore ----
const dataResult = ref<string | null>(null)
const busy = ref(false)
const confirmRestore = ref(false)
const offData = on('dataResult', (r) => {
  busy.value = false
  dataResult.value = r
})
onUnmounted(offData)
function runData(action: 'csv' | 'backup' | 'restore') {
  busy.value = true
  dataResult.value = null
  confirmRestore.value = false
  send({ type: 'data', action })
}
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
      <p class="hint flush">{{ t('Also used for notifications, the overlay and Discord recaps.') }}</p>
    </div>

    <div class="panel option">
      <label class="switch">
        <input type="checkbox" :checked="settings.launchWithFortnite" @change="send({ type: 'setLaunchWithFortnite', enabled: checked($event) })" />
        <span class="track" aria-hidden="true" />
        <span class="text">{{ t('Open with Fortnite') }}</span>
      </label>
      <p class="hint">
        {{ t('The app starts quietly with Windows, waits in the tray, and opens by itself when Fortnite starts. It uses almost no memory while waiting.') }}
      </p>
    </div>

    <div class="panel option">
      <label class="switch">
        <input type="checkbox" :checked="settings.overlay.enabled" @change="emit('overlay', checked($event))" />
        <span class="track" aria-hidden="true" />
        <span class="text">{{ t('In-game overlay') }}</span>
      </label>
      <p class="hint">
        {{ t("A small bar with your squad's K/D, and your eliminator after a death. Clicks go through it. Shows while Fortnite runs in") }}
        <strong>{{ t('Windowed Fullscreen') }}</strong>. {{ t('Shortcut: {keys}.', { keys: keysLabel(settings.hotkeys.overlay) }) }}
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
      <p v-if="recapResult" class="result">{{ t(recapResult) }}</p>
    </div>

    <div class="panel option shortcuts">
      <span class="text">{{ t('Shortcuts') }}</span>
      <p class="hint flush">{{ t('Click a shortcut, then press the keys you want. They work even while Fortnite has focus.') }}</p>
      <div class="hotkeys">
        <HotkeyInput
          :value="settings.hotkeys.window"
          :ok="settings.hotkeys.windowOk"
          fallback="Ctrl+Shift+F"
          :label="t('Show or hide this window, even in game')"
          @change="setHotkey('window', $event)"
        />
        <HotkeyInput
          :value="settings.hotkeys.overlay"
          :ok="settings.hotkeys.overlayOk"
          fallback="Ctrl+Shift+O"
          :label="t('Turn the in-game overlay on or off')"
          @change="setHotkey('overlay', $event)"
        />
        <div class="fixed-key"><span class="keys">F11</span><span>{{ t('Full screen') }}</span></div>
      </div>
    </div>

    <div class="panel option data">
      <span class="text">{{ t('Your data') }}</span>
      <p class="hint flush">{{ t('Export your matches to open them in Excel, or back up everything (history, ranks, notes, goals, theme, settings) to move it to another PC.') }}</p>
      <div class="row">
        <button type="button" class="ghost" :disabled="busy" @click="runData('csv')">{{ t('Export matches (CSV)') }}</button>
        <button type="button" class="ghost" :disabled="busy" @click="runData('backup')">{{ t('Back up my data') }}</button>
        <button type="button" class="ghost" :disabled="busy" @click="confirmRestore = true">{{ t('Restore a backup') }}</button>
      </div>
      <div v-if="confirmRestore" class="confirm">
        <p>{{ t('Restoring replaces your current data with the backup and restarts the app. A copy of your current data is kept in the backups folder.') }}</p>
        <div class="row">
          <button type="button" class="btn-primary" @click="runData('restore')">{{ t('Choose a backup…') }}</button>
          <button type="button" class="ghost" @click="confirmRestore = false">{{ t('Cancel') }}</button>
        </div>
      </div>
      <p v-if="dataResult" class="result">{{ dataResult }}</p>
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
.hotkeys {
  display: flex;
  flex-direction: column;
  gap: var(--s3);
}
.fixed-key {
  display: grid;
  grid-template-columns: 150px 1fr;
  gap: var(--s3);
  align-items: center;
  color: var(--muted);
  font-size: 14px;
}
.fixed-key .keys {
  font-family: var(--display);
  font-weight: 800;
  letter-spacing: 0.04em;
  color: var(--text);
  text-align: center;
}
.option .hint.flush,
.shortcuts .hint.flush,
.data .hint.flush {
  margin: var(--s2) 0 var(--s3);
}
.data .row {
  display: flex;
  flex-wrap: wrap;
  gap: var(--s2);
}
.confirm {
  margin-top: var(--s3);
  padding: var(--s3);
  border: 1px solid var(--border);
  border-radius: var(--radius-sm);
  background: var(--surface-2);
}
.confirm p {
  margin: 0 0 var(--s3);
  font-size: 14px;
  line-height: 1.5;
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
