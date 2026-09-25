<script setup lang="ts">
import { computed, onUnmounted, ref } from 'vue'
import { isHosted, type Platform } from './bridge'
import { useTracker } from './composables/useTracker'
import PlayerCard from './components/PlayerCard.vue'
import ApiKeyForm from './components/ApiKeyForm.vue'
import HistoryView from './components/HistoryView.vue'
import SettingsView from './components/SettingsView.vue'

const { snapshot, settings, history, lookupResult, lookingUp, lookup, saveApiKey, setRichPresence, applyUpdate } =
  useTracker()

type Tab = 'squad' | 'history' | 'settings'
const tabs: { id: Tab; label: string }[] = [
  { id: 'squad', label: 'Squad' },
  { id: 'history', label: 'History' },
  { id: 'settings', label: 'Settings' },
]
const tab = ref<Tab>('squad')

const searchName = ref('')
const platform = ref<Platform>('epic')

// Ticks once a minute so the in-match timer stays current.
const now = ref(Date.now())
const clock = window.setInterval(() => (now.value = Date.now()), 30_000)
onUnmounted(() => window.clearInterval(clock))

const phase = computed(() => {
  const s = snapshot.value
  if (!s) return { label: 'Waiting for Fortnite', cls: 'idle' }
  if (!s.gameRunning) return { label: 'Fortnite not running', cls: 'idle' }
  if (!s.inMatch) return { label: 'In lobby', cls: 'lobby' }
  const mins = s.matchStartedUtc ? Math.max(0, Math.floor((now.value - Date.parse(s.matchStartedUtc)) / 60_000)) : null
  return { label: `${s.mode}${mins !== null ? ` · ${mins} min` : ''}`, cls: 'live' }
})

function submitSearch() {
  if (searchName.value.trim()) lookup(searchName.value.trim(), platform.value)
}
</script>

<template>
  <main>
    <header class="top">
      <h1>Fortnite Tracker</h1>
      <span class="pill" :class="phase.cls">{{ phase.label }}</span>
    </header>

    <p v-if="!isHosted" class="notice">
      This page talks to the desktop app. Run it inside FortniteTracker.exe to see live data.
    </p>

    <div v-if="settings?.updateVersion" class="update">
      <span>Version {{ settings.updateVersion }} is ready.</span>
      <button type="button" @click="applyUpdate">Restart to update</button>
    </div>

    <nav class="tabs" role="tablist">
      <button
        v-for="t in tabs"
        :key="t.id"
        type="button"
        role="tab"
        :aria-selected="tab === t.id"
        :class="{ active: tab === t.id }"
        @click="tab = t.id"
      >
        {{ t.label }}
      </button>
    </nav>

    <template v-if="tab === 'squad'">
      <ApiKeyForm v-if="settings && !settings.hasApiKey" :has-key="false" @save="saveApiKey" />

      <section>
        <h2>Your squad</h2>
        <div v-if="snapshot?.squad.length" class="list">
          <PlayerCard
            v-for="(p, i) in snapshot.squad"
            :key="p.accountId ?? i"
            :player="p"
            :is-you="i === 0 && !!snapshot.localName"
            :fallback-name="i === 0 ? snapshot.localName : null"
          />
        </div>
        <p v-else class="empty">Launch Fortnite. Your squad appears here automatically.</p>
      </section>

      <section>
        <h2>Look up a player</h2>
        <form class="row" @submit.prevent="submitSearch">
          <input v-model="searchName" placeholder="Epic name, e.g. who just eliminated you" />
          <select v-model="platform" aria-label="Platform">
            <option value="epic">Epic</option>
            <option value="psn">PSN</option>
            <option value="xbl">Xbox</option>
          </select>
          <button type="submit" :disabled="lookingUp">{{ lookingUp ? '…' : 'Search' }}</button>
        </form>
        <PlayerCard v-if="lookupResult" :player="lookupResult" class="result" />
      </section>
    </template>

    <HistoryView v-else-if="tab === 'history'" :matches="history" />

    <SettingsView
      v-else-if="settings"
      :settings="settings"
      @save-key="saveApiKey"
      @rich-presence="setRichPresence"
    />

    <footer>Ctrl+Shift+F shows or hides this window. Closing it keeps the app running in the tray.</footer>
  </main>
</template>

<style>
:root {
  --bg: #0f1115;
  --surface: #181b22;
  --border: #262a33;
  --text: #e8eaf0;
  --muted: #8b91a0;
  --accent: #7cc4ff;
  --accent-ink: #0b1a26;
  --live: #4ade80;
  color-scheme: dark;
  font-family: 'Segoe UI Variable', 'Segoe UI', system-ui, sans-serif;
  font-size: 14px;
  color: var(--text);
  background: var(--bg);
}
body {
  margin: 0;
  background: var(--bg);
}
input,
select,
button {
  font: inherit;
  color: inherit;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 8px;
  padding: 7px 10px;
}
input {
  flex: 1;
  min-width: 0;
}
button {
  cursor: pointer;
  background: var(--accent);
  color: var(--accent-ink);
  border-color: transparent;
  font-weight: 600;
}
button:disabled {
  opacity: 0.6;
  cursor: default;
}
:focus-visible {
  outline: 2px solid var(--accent);
  outline-offset: 1px;
}
h2 {
  font-size: 12px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: var(--muted);
  margin: 0 0 8px;
}
.panel {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 10px;
  padding: 12px 14px;
}
.panel label {
  font-weight: 600;
}
.row {
  display: flex;
  gap: 6px;
}
.hint,
.empty {
  color: var(--muted);
  font-size: 13px;
  margin: 4px 0 10px;
}
</style>

<style scoped>
main {
  display: flex;
  flex-direction: column;
  gap: 16px;
  padding: 16px;
}
.top {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 8px;
}
h1 {
  font-size: 16px;
  margin: 0;
  white-space: nowrap;
}
.pill {
  font-size: 12px;
  border-radius: 999px;
  padding: 2px 10px;
  border: 1px solid var(--border);
  color: var(--muted);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.pill.live {
  color: var(--live);
  border-color: color-mix(in srgb, var(--live) 40%, transparent);
}
.pill.lobby {
  color: var(--accent);
  border-color: color-mix(in srgb, var(--accent) 40%, transparent);
}
.update {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  background: color-mix(in srgb, var(--accent) 12%, var(--surface));
  border: 1px solid color-mix(in srgb, var(--accent) 35%, transparent);
  border-radius: 10px;
  padding: 8px 8px 8px 12px;
}
.tabs {
  display: flex;
  gap: 4px;
  border-bottom: 1px solid var(--border);
}
.tabs button {
  background: none;
  color: var(--muted);
  border: none;
  border-bottom: 2px solid transparent;
  border-radius: 0;
  padding: 6px 10px;
  margin-bottom: -1px;
}
.tabs button.active {
  color: var(--text);
  border-bottom-color: var(--accent);
}
.list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.notice {
  color: var(--muted);
  font-size: 13px;
  border: 1px dashed var(--border);
  border-radius: 8px;
  padding: 8px 10px;
  margin: 0;
}
.result {
  margin-top: 8px;
}
footer {
  color: var(--muted);
  font-size: 12px;
}
</style>
