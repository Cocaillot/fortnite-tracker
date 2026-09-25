<script setup lang="ts">
import { computed, onUnmounted, ref } from 'vue'
import { isHosted } from './bridge'
import { useTracker } from './composables/useTracker'
import TitleBar from './components/TitleBar.vue'
import LiveView from './components/LiveView.vue'
import ApiKeyForm from './components/ApiKeyForm.vue'
import HistoryView from './components/HistoryView.vue'
import SettingsView from './components/SettingsView.vue'
import LeaderboardView from './components/LeaderboardView.vue'
import ProfilePanel from './components/ProfilePanel.vue'

const {
  snapshot,
  settings,
  history,
  sessions,
  squadRanks,
  lookupResult,
  lookingUp,
  profile,
  profileLoading,
  leaderboard,
  lookup,
  openProfile,
  closeProfile,
  follow,
  loadLeaderboard,
  saveApiKey,
  setRichPresence,
  setNotify,
  setOverlay,
  applyUpdate,
} = useTracker()

type Tab = 'live' | 'board' | 'history' | 'settings'
const tabs: { id: Tab; label: string }[] = [
  { id: 'live', label: 'Live' },
  { id: 'board', label: 'Leaderboard' },
  { id: 'history', label: 'History' },
]
const tab = ref<Tab>('live')

function selectTab(t: Tab) {
  closeProfile()
  tab.value = t
}

const profileOpen = computed(() => profile.value !== null || profileLoading.value !== null)

// Ticks every second so the match timer counts up live.
const now = ref(Date.now())
const clock = window.setInterval(() => (now.value = Date.now()), 1000)
onUnmounted(() => window.clearInterval(clock))

const status = computed(() => {
  const s = snapshot.value
  if (!s) return { state: 'idle', label: 'Waiting for Fortnite', timer: null }
  if (!s.gameRunning) return { state: 'idle', label: 'Fortnite not running', timer: null }
  if (!s.inMatch) return { state: 'lobby', label: 'In lobby', timer: null }
  const secs = s.matchStartedUtc ? Math.max(0, Math.floor((now.value - Date.parse(s.matchStartedUtc)) / 1000)) : null
  const timer = secs === null ? null : `${Math.floor(secs / 60)}:${String(secs % 60).padStart(2, '0')}`
  return { state: 'live', label: s.mode, timer }
})

const overlayOn = computed(() => settings.value?.overlay.enabled ?? false)
</script>

<template>
  <div class="app">
    <TitleBar :overlay-on="overlayOn" @toggle-overlay="setOverlay(!overlayOn)" />

    <div class="status" :class="status.state">
      <span class="pulse" aria-hidden="true" />
      <span class="label">{{ status.label }}</span>
      <span v-if="status.timer" class="timer">{{ status.timer }}</span>
      <span v-if="snapshot?.gameRunning" class="bucket">Stats: {{ snapshot.statsLabel }}</span>
    </div>

    <nav class="tabs" role="tablist">
      <button
        v-for="t in tabs"
        :key="t.id"
        type="button"
        role="tab"
        :aria-selected="tab === t.id"
        :class="{ active: tab === t.id && !profileOpen }"
        @click="selectTab(t.id)"
      >
        {{ t.label }}
      </button>
      <button
        type="button"
        role="tab"
        class="gear"
        :aria-selected="tab === 'settings'"
        :class="{ active: tab === 'settings' && !profileOpen }"
        title="Settings"
        aria-label="Settings"
        @click="selectTab('settings')"
      >
        <svg viewBox="0 0 24 24" aria-hidden="true">
          <path d="M12 8.5a3.5 3.5 0 1 0 0 7 3.5 3.5 0 0 0 0-7zm8.4 4.6l1.7 1.3-1.8 3.2-2-.7a7.6 7.6 0 0 1-1.9 1.1l-.3 2.1h-3.7l-.3-2.1a7.6 7.6 0 0 1-1.9-1.1l-2 .7-1.8-3.2 1.7-1.3a7.3 7.3 0 0 1 0-2.2L3.9 9.6l1.8-3.2 2 .7a7.6 7.6 0 0 1 1.9-1.1L9.9 4h3.7l.3 2.1c.7.3 1.3.6 1.9 1.1l2-.7 1.8 3.2-1.7 1.3a7.3 7.3 0 0 1 0 2.1z" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linejoin="round" />
        </svg>
      </button>
    </nav>

    <main>
      <p v-if="!isHosted" class="notice">
        This page talks to the desktop app. Run it inside FortniteTracker.exe to see live data.
      </p>

      <div v-if="settings?.updateVersion" class="update">
        <span>Version {{ settings.updateVersion }} is ready.</span>
        <button type="button" class="btn-primary" @click="applyUpdate">Restart to update</button>
      </div>

      <ProfilePanel
        v-if="profileOpen"
        :profile="profile"
        :loading-name="profileLoading?.name ?? null"
        @close="closeProfile"
        @follow="follow"
      />

      <template v-else-if="tab === 'live'">
        <ApiKeyForm v-if="settings && !settings.hasApiKey" :has-key="false" class="key-prompt" @save="saveApiKey" />
        <LiveView
          :snapshot="snapshot"
          :lookup-result="lookupResult"
          :looking-up="lookingUp"
          :ranks="squadRanks"
          @lookup="lookup"
          @open="openProfile"
        />
      </template>

      <LeaderboardView v-else-if="tab === 'board'" :entries="leaderboard" @refresh="loadLeaderboard" @open="openProfile" />

      <HistoryView v-else-if="tab === 'history'" :matches="history" :sessions="sessions" @open="openProfile" />

      <SettingsView
        v-else-if="settings"
        :settings="settings"
        @save-key="saveApiKey"
        @rich-presence="setRichPresence"
        @notify="setNotify"
        @overlay="setOverlay"
      />
    </main>
  </div>
</template>

<style scoped>
.app {
  height: 100vh;
  display: flex;
  flex-direction: column;
}

.status {
  flex: none;
  display: flex;
  align-items: center;
  gap: 8px;
  margin: 0 14px;
  padding: 7px 12px;
  border-radius: 8px;
  background: var(--surface);
  border: 1px solid var(--border);
  font-family: var(--display);
  font-weight: 700;
  font-size: 15px;
  letter-spacing: 0.03em;
  text-transform: uppercase;
  color: var(--muted);
}
.pulse {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--faint);
  flex: none;
}
.status.lobby .pulse {
  background: var(--accent);
}
.status.lobby .label {
  color: var(--accent);
}
.status.live {
  border-color: color-mix(in srgb, var(--live) 40%, var(--border));
}
.status.live .label {
  color: var(--live);
}
.status.live .pulse {
  background: var(--live);
  animation: pulse 1.6s ease-out infinite;
}
@keyframes pulse {
  0% { box-shadow: 0 0 0 0 color-mix(in srgb, var(--live) 60%, transparent); }
  100% { box-shadow: 0 0 0 8px transparent; }
}
.timer {
  color: var(--text);
  font-variant-numeric: tabular-nums;
  font-weight: 800;
}
.bucket {
  margin-left: auto;
  font-size: 12px;
  color: var(--faint);
  white-space: nowrap;
}

.tabs {
  flex: none;
  display: flex;
  gap: 4px;
  padding: 10px 14px 0;
  border-bottom: 1px solid var(--border);
}
.tabs button {
  background: none;
  border: none;
  border-radius: 0;
  padding: 6px 12px 8px;
  margin-bottom: -1px;
  color: var(--muted);
  font-family: var(--display);
  font-weight: 800;
  font-size: 17px;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  position: relative;
}
.tabs .gear {
  margin-left: auto;
  padding: 6px 8px 8px;
}
.tabs .gear svg {
  width: 19px;
  height: 19px;
  display: block;
}
.tabs button:hover {
  color: var(--text);
}
.tabs button.active {
  color: var(--text);
}
/* Skewed underline, in the style of Fortnite's menus. */
.tabs button.active::after {
  content: '';
  position: absolute;
  left: 8px;
  right: 8px;
  bottom: 0;
  height: 3px;
  background: var(--accent);
  transform: skewX(-20deg);
}

main {
  flex: 1;
  overflow-y: auto;
  padding: 14px 14px 20px;
}
.key-prompt {
  margin-bottom: 18px;
}
.notice {
  color: var(--muted);
  font-size: 13px;
  border: 1px dashed var(--border);
  border-radius: 8px;
  padding: 8px 10px;
  margin: 0 0 14px;
}
.update {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 10px;
  margin-bottom: 14px;
  background: color-mix(in srgb, var(--accent) 12%, var(--surface));
  border: 1px solid color-mix(in srgb, var(--accent) 35%, transparent);
  border-radius: 10px;
  padding: 8px 8px 8px 12px;
}
</style>
