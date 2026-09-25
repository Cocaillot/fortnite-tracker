import { ref, onMounted, onUnmounted } from 'vue'
import { on, send, type LobbySnapshot, type Platform, type PlayerStats } from '../bridge'

export function useTracker() {
  const snapshot = ref<LobbySnapshot | null>(null)
  const hasApiKey = ref(true)
  const lookupResult = ref<PlayerStats | null>(null)
  const lookingUp = ref(false)

  const unsubscribers: (() => void)[] = []

  onMounted(() => {
    unsubscribers.push(
      on('snapshot', (s) => (snapshot.value = s)),
      on('settings', (s) => (hasApiKey.value = s.hasApiKey)),
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

  function saveApiKey(key: string) {
    send({ type: 'setApiKey', key })
  }

  return { snapshot, hasApiKey, lookupResult, lookingUp, lookup, saveApiKey }
}
