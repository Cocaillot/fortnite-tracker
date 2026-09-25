import { ref, onMounted, onUnmounted } from 'vue'
import {
  on,
  send,
  type LobbySnapshot,
  type MatchRecord,
  type OverlayCorner,
  type Platform,
  type PlayerStats,
  type Settings,
} from '../bridge'

export function useTracker() {
  const snapshot = ref<LobbySnapshot | null>(null)
  const settings = ref<Settings | null>(null)
  const history = ref<MatchRecord[]>([])
  const lookupResult = ref<PlayerStats | null>(null)
  const lookingUp = ref(false)

  const unsubscribers: (() => void)[] = []

  onMounted(() => {
    unsubscribers.push(
      on('snapshot', (s) => (snapshot.value = s)),
      on('settings', (s) => (settings.value = s)),
      on('history', (h) => (history.value = h)),
      on('lookupResult', (r) => {
        lookupResult.value = r
        lookingUp.value = false
      }),
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

  return {
    snapshot,
    settings,
    history,
    lookupResult,
    lookingUp,
    lookup,
    saveApiKey: (key: string) => send({ type: 'setApiKey', key }),
    setRichPresence: (enabled: boolean) => send({ type: 'setRichPresence', enabled }),
    setNotify: (enabled: boolean) => send({ type: 'setNotify', enabled }),
    setOverlay: (enabled?: boolean, corner?: OverlayCorner) => send({ type: 'setOverlay', enabled, corner }),
    applyUpdate: () => send({ type: 'applyUpdate' }),
  }
}
