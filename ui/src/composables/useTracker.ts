import { ref, onMounted, onUnmounted } from 'vue'
import {
  on,
  send,
  type LeaderboardEntry,
  type LobbySnapshot,
  type MatchRecord,
  type OverlayCorner,
  type Platform,
  type PlayerProfile,
  type PlayerStats,
  type RankProgress,
  type SessionRecord,
  type Settings,
  type MatchDetail,
  type TeammateSummary,
  type StatsDay,
} from '../bridge'

export function useTracker() {
  const snapshot = ref<LobbySnapshot | null>(null)
  const settings = ref<Settings | null>(null)
  const history = ref<MatchRecord[]>([])
  const sessions = ref<SessionRecord[]>([])
  const squadRanks = ref<Record<string, RankProgress[]>>({})
  const lookupResult = ref<PlayerStats | null>(null)
  const lookingUp = ref(false)
  const profile = ref<PlayerProfile | null>(null)
  const profileLoading = ref<{ accountId: string | null; name: string | null } | null>(null)
  const leaderboard = ref<LeaderboardEntry[] | null>(null)
  const windowState = ref({ maximized: true, fullscreen: false })
  const openMatch = ref<MatchRecord | null>(null)
  const matchDetail = ref<MatchDetail | null>(null)
  const teammates = ref<TeammateSummary[] | null>(null)
  const statsHistory = ref<StatsDay[]>([])
  const recapResult = ref<string | null>(null)

  const unsubscribers: (() => void)[] = []

  onMounted(() => {
    unsubscribers.push(
      on('snapshot', (s) => (snapshot.value = s)),
      on('settings', (s) => (settings.value = s)),
      on('history', (h) => (history.value = h)),
      on('sessions', (s) => (sessions.value = s)),
      on('ranks', (r) => (squadRanks.value = r)),
      on('lookupResult', (r) => {
        lookupResult.value = r
        lookingUp.value = false
      }),
      on('profile', (p) => {
        // Ignore a late answer for a profile the user already closed.
        if (!profileLoading.value && !profile.value) return
        profile.value = p
        profileLoading.value = null
      }),
      on('leaderboard', (l) => (leaderboard.value = l)),
      on('windowState', (w) => (windowState.value = w)),
      on('matchDetail', (d) => {
        if (d && openMatch.value && d.match.startedUtc === openMatch.value.startedUtc) matchDetail.value = d
      }),
      on('teammates', (t) => (teammates.value = t)),
      on('statsHistory', (h) => (statsHistory.value = h)),
      on('recapResult', (r) => (recapResult.value = r)),
    )
    // Ask the host for the current state; it may have published before the page loaded.
    send({ type: 'ready' })
  })

  onUnmounted(() => unsubscribers.forEach((off) => off()))

  function lookup(name: string, platform: Platform) {
    lookingUp.value = true
    lookupResult.value = null
    send({ type: 'lookup', name, platform })
  }

  function openProfile(accountId: string | null, name: string | null) {
    profile.value = null
    profileLoading.value = { accountId, name }
    send({ type: 'profile', accountId, name })
  }

  function closeProfile() {
    profile.value = null
    profileLoading.value = null
  }

  return {
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
    windowState,
    openMatch,
    matchDetail,
    teammates,
    statsHistory,
    recapResult,
    setDiscordRecap: (url: string | null, enabled: boolean) => {
      recapResult.value = null
      send({ type: 'setDiscordRecap', url, enabled })
    },
    postRecap: () => {
      recapResult.value = 'Posting…'
      send({ type: 'postRecap' })
    },
    showMatch: (m: MatchRecord) => {
      openMatch.value = m
      matchDetail.value = null
      send({ type: 'match', startedUtc: m.startedUtc })
    },
    closeMatch: () => {
      openMatch.value = null
      matchDetail.value = null
    },
    loadTeammates: () => send({ type: 'teammates' }),
    lookup,
    openProfile,
    closeProfile,
    follow: (accountId: string | null, name: string, enabled: boolean) => send({ type: 'follow', accountId, name, enabled }),
    loadLeaderboard: () => send({ type: 'leaderboard' }),
    saveApiKey: (key: string) => send({ type: 'setApiKey', key }),
    setRichPresence: (enabled: boolean) => send({ type: 'setRichPresence', enabled }),
    setNotify: (enabled: boolean) => send({ type: 'setNotify', enabled }),
    setNotifyRanks: (enabled: boolean) => send({ type: 'setNotifyRanks', enabled }),
    setOverlay: (enabled?: boolean, corner?: OverlayCorner) => send({ type: 'setOverlay', enabled, corner }),
    applyUpdate: () => send({ type: 'applyUpdate' }),
  }
}
