<script setup lang="ts">
import { t, tn } from './i18n'
import { computed, nextTick, onMounted, onUnmounted, ref } from 'vue'
import { isHosted, send } from './bridge'
import { useTracker } from './composables/useTracker'
import TitleBar from './components/TitleBar.vue'
import SideBar, { type Page } from './components/SideBar.vue'
import LiveView from './components/LiveView.vue'
import ApiKeyForm from './components/ApiKeyForm.vue'
import HistoryView from './components/HistoryView.vue'
import SettingsView from './components/SettingsView.vue'
import LeaderboardView from './components/LeaderboardView.vue'
import ProfilePanel from './components/ProfilePanel.vue'
import AppearanceView from './components/AppearanceView.vue'
import ToastHost from './components/ToastHost.vue'
import MatchDrawer from './components/MatchDrawer.vue'
import OnboardingGuide from './components/OnboardingGuide.vue'
import WhatsNew from './components/WhatsNew.vue'
import { unseen } from './changelog'

const {
  snapshot,
  settings,
  history,
  sessions,
  squadRanks,
  profile,
  profileLoading,
  leaderboard,
  windowState,
  openMatch,
  matchDetail,
  teammates,
  statsHistory,
  recapResult,
  setDiscordRecap,
  postRecap,
  showMatch,
  closeMatch,
  loadTeammates,
  openProfile,
  closeProfile,
  follow,
  loadLeaderboard,
  saveApiKey,
  setRichPresence,
  setNotify,
  setNotifyRanks,
  setOverlay,
  applyUpdate,
} = useTracker()

// 'profile' is a player opened from elsewhere; 'me' is your own profile from the sidebar.
const page = ref<Page | 'profile'>('live')
const previousPage = ref<Page>('live')
const content = ref<HTMLElement | null>(null)

const selfId = computed(() => (snapshot.value?.localName ? (snapshot.value.squad[0]?.accountId ?? null) : null))

async function navigate(p: Page) {
  closeProfile()
  page.value = p
  if (p === 'me' && selfId.value) openProfile(selfId.value, null)
  await nextTick()
  content.value?.scrollTo({ top: 0 })
}

async function showProfile(accountId: string | null, name: string | null) {
  if (page.value !== 'profile') previousPage.value = page.value as Page
  page.value = 'profile'
  openProfile(accountId, name)
  await nextTick()
  content.value?.scrollTo({ top: 0 })
}

function back() {
  navigate(previousPage.value)
}

// Ticks every second so the match timer counts up live.
const now = ref(Date.now())
const clock = window.setInterval(() => (now.value = Date.now()), 1000)

function onKey(e: KeyboardEvent) {
  if (e.key === 'F11') {
    e.preventDefault()
    send({ type: 'window', action: 'fullscreen' })
  }
}
onMounted(() => window.addEventListener('keydown', onKey))
onUnmounted(() => {
  window.clearInterval(clock)
  window.removeEventListener('keydown', onKey)
})

const status = computed(() => {
  const s = snapshot.value
  if (!s) return { state: 'idle' as const, label: t('Waiting for Fortnite'), timer: null, detail: null }
  if (!s.gameRunning) return { state: 'idle' as const, label: t('Fortnite not running'), timer: null, detail: null }
  if (!s.inMatch)
    return { state: 'lobby' as const, label: t('In lobby'), timer: null, detail: s.mode !== 'Match' ? t('{mode} selected', { mode: tn(s.mode) }) : null }
  const secs = s.matchStartedUtc ? Math.max(0, Math.floor((now.value - Date.parse(s.matchStartedUtc)) / 1000)) : null
  const timer = secs === null ? null : `${Math.floor(secs / 60)}:${String(secs % 60).padStart(2, '0')}`
  return { state: 'live' as const, label: tn(s.mode), timer, detail: t('Stats: {label}', { label: tn(s.statsLabel) }) }
})

const whatsNew = computed(() =>
  settings.value?.onboardingDone ? unseen(settings.value.version, settings.value.lastSeenVersion) : [],
)

const overlayOn = computed(() => settings.value?.overlay.enabled ?? false)
const profileShown = computed(() => page.value === 'profile' || (page.value === 'me' && (profile.value || profileLoading.value)))
</script>

<template>
  <div class="app">
    <TitleBar
      :overlay-on="overlayOn"
      :overlay-keys="settings?.hotkeys.overlay ?? 'Ctrl+Shift+O'"
      :maximized="windowState.maximized"
      :fullscreen="windowState.fullscreen"
      @toggle-overlay="setOverlay(!overlayOn)"
      @search="(name) => showProfile(null, name)"
    />

    <div class="body">
      <SideBar
        :page="page"
        :status="status"
        :version="settings?.version ?? null"
        :update-version="settings?.updateVersion ?? null"
        @navigate="navigate"
        @apply-update="applyUpdate"
      />

      <main ref="content" class="content">
        <p v-if="!isHosted" class="notice">{{ t('This page talks to the desktop app. Run it inside FortniteTracker.exe to see live data.') }}</p>

        <div v-if="settings && !settings.hasApiKey && page !== 'settings' && page !== 'appearance'" class="key-banner">
          <ApiKeyForm :has-key="false" @save="saveApiKey" />
        </div>

        <Transition name="page" mode="out-in">
        <ProfilePanel
          v-if="profileShown"
          :profile="profile"
          :loading-name="profileLoading?.name ?? null"
          :can-go-back="page === 'profile'"
          @close="back"
          @follow="follow"
        />

        <div v-else-if="page === 'me'" class="page">
          <div class="page-header"><div><h1>{{ t('My profile') }}</h1><p>{{ t('Launch Fortnite once so the app knows which account is yours.') }}</p></div></div>
        </div>

        <LiveView
          v-else-if="page === 'live'"
          :snapshot="snapshot"
          :ranks="squadRanks"
          :history="history"
          :timer="status.timer"
          @open="showProfile"
        />

        <LeaderboardView v-else-if="page === 'leaderboard'" :entries="leaderboard" @refresh="loadLeaderboard" @open="showProfile" />

        <HistoryView
          v-else-if="page === 'history'"
          :matches="history"
          :sessions="sessions"
          :teammates="teammates"
          :stats-history="statsHistory"
          @open="showProfile"
          @match="showMatch"
          @load-teammates="loadTeammates"
        />

        <AppearanceView v-else-if="page === 'appearance'" />

        <SettingsView
          v-else-if="page === 'settings' && settings"
          :settings="settings"
          :recap-result="recapResult"
          @save-key="saveApiKey"
          @rich-presence="setRichPresence"
          @notify="setNotify"
          @notify-ranks="setNotifyRanks"
          @discord-recap="setDiscordRecap"
          @post-recap="postRecap"
          @overlay="setOverlay"
        />
        </Transition>
      </main>
    </div>
    <MatchDrawer
      v-if="openMatch"
      :match="openMatch"
      :detail="matchDetail"
      @close="closeMatch"
      @open="(id, n) => { closeMatch(); showProfile(id, n) }"
    />
    <ToastHost />
    <OnboardingGuide v-if="settings && !settings.onboardingDone" :settings="settings" :snapshot="snapshot" />
    <WhatsNew v-else-if="whatsNew.length" :releases="whatsNew" />
  </div>
</template>

<style scoped>
.app {
  height: 100vh;
  display: flex;
  flex-direction: column;
}
.body {
  flex: 1;
  min-height: 0;
  display: flex;
}
.content {
  flex: 1;
  min-width: 0;
  overflow-y: auto;
  scroll-behavior: smooth;
  /* A faint accent glow in the top corner gives the page some depth. */
  background: radial-gradient(1100px 520px at 0% 0%, color-mix(in srgb, var(--accent) 7%, transparent), transparent 70%);
}
.page-enter-active {
  transition: opacity 0.2s ease, transform 0.25s ease;
}
.page-leave-active {
  transition: opacity 0.1s ease;
}
.page-enter-from {
  opacity: 0;
  transform: translateY(8px);
}
.page-leave-to {
  opacity: 0;
}
.key-banner {
  max-width: 1440px;
  margin: 0 auto;
  padding: var(--s5) var(--s6) 0;
}
.notice {
  margin: var(--s4) var(--s6) 0;
  color: var(--muted);
  font-size: 13px;
  border: 1px dashed var(--border);
  border-radius: 8px;
  padding: var(--s2) var(--s3);
}
</style>
