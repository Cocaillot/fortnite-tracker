<script setup lang="ts">
import { computed, ref } from 'vue'
import { isHosted, type Platform } from './bridge'
import { useTracker } from './composables/useTracker'
import PlayerCard from './components/PlayerCard.vue'

const { snapshot, hasApiKey, lookupResult, lookingUp, lookup, saveApiKey } = useTracker()

const apiKeyInput = ref('')
const searchName = ref('')
const platform = ref<Platform>('epic')

const phase = computed(() => {
  if (!snapshot.value) return { label: 'Waiting for Fortnite', cls: 'idle' }
  if (!snapshot.value.gameRunning) return { label: 'Fortnite not running', cls: 'idle' }
  return snapshot.value.inMatch ? { label: 'In match', cls: 'live' } : { label: 'In lobby', cls: 'lobby' }
})

function submitKey() {
  if (apiKeyInput.value.trim()) saveApiKey(apiKeyInput.value.trim())
  apiKeyInput.value = ''
}

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

    <form v-if="!hasApiKey" class="panel" @submit.prevent="submitKey">
      <label for="key">fortnite-api.com API key</label>
      <p class="hint">
        Free key from fortnite-api.com/dashboard. It is stored only on this PC.
      </p>
      <div class="row">
        <input id="key" v-model="apiKeyInput" type="password" autocomplete="off" placeholder="Paste key" />
        <button type="submit">Save</button>
      </div>
    </form>

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

    <footer>Ctrl+Shift+F shows or hides this window.</footer>
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
</style>

<style scoped>
main {
  display: flex;
  flex-direction: column;
  gap: 18px;
  padding: 16px;
}
.top {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
h1 {
  font-size: 16px;
  margin: 0;
}
h2 {
  font-size: 12px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  color: var(--muted);
  margin: 0 0 8px;
}
.pill {
  font-size: 12px;
  border-radius: 999px;
  padding: 2px 10px;
  border: 1px solid var(--border);
  color: var(--muted);
}
.pill.live {
  color: var(--live);
  border-color: color-mix(in srgb, var(--live) 40%, transparent);
}
.pill.lobby {
  color: var(--accent);
  border-color: color-mix(in srgb, var(--accent) 40%, transparent);
}
.list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}
.row {
  display: flex;
  gap: 6px;
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
.hint,
.empty,
.notice,
footer {
  color: var(--muted);
  font-size: 13px;
  margin: 4px 0 10px;
}
.notice {
  border: 1px dashed var(--border);
  border-radius: 8px;
  padding: 8px 10px;
}
.result {
  margin-top: 8px;
}
footer {
  font-size: 12px;
  margin: 0;
}
</style>
