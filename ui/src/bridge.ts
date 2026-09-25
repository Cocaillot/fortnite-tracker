// Messages exchanged with the .NET host through WebView2 (see UiBridge.cs).

export type StatsStatus = 'Ok' | 'Private' | 'NotFound' | 'NoApiKey' | 'Error' | 'Hidden' | 'Loading'
export type Platform = 'epic' | 'psn' | 'xbl'
export type Rarity = 'Common' | 'Uncommon' | 'Rare' | 'Epic' | 'Legendary'
export type Threat = 'BotLikely' | 'Casual' | 'Average' | 'Skilled' | 'Sweat'
export type OverlayCorner = 'TopLeft' | 'TopRight' | 'BottomLeft' | 'BottomRight'

export interface ModeStats {
  wins: number
  winRate: number
  kd: number
  kills: number
  matches: number
  kdRarity: Rarity
  winRateRarity: Rarity
}

export interface PlayerStats {
  accountId: string | null
  epicName: string | null
  status: StatsStatus
  overall: ModeStats | null
  /** fortnite-api buckets: solo, duo, squad, ltm (Ranked counts as ltm). */
  byMode: Record<string, ModeStats> | null
  threat: Threat | null
}

export interface LobbySnapshot {
  gameRunning: boolean
  inMatch: boolean
  localName: string | null
  mode: string
  matchStartedUtc: string | null
  squad: PlayerStats[]
  eliminatedBy: PlayerStats | null
  spectated: PlayerStats[]
  statsBucket: string | null
  statsLabel: string
  lastMatchStartedUtc: string | null
  lastMatchEndedUtc: string | null
}

export interface MatchRecord {
  startedUtc: string
  endedUtc: string | null
  mode: string
  playlist: string | null
  squadSize: number
  finished: boolean
  eliminatedBy: string | null
  kills: number | null
  won: boolean | null
  eliminatorKd: number | null
  eliminatorThreat: Threat | null
}

export interface Settings {
  hasApiKey: boolean
  richPresence: { available: boolean; enabled: boolean }
  notifyOnElimination: boolean
  overlay: { enabled: boolean; corner: OverlayCorner }
  version: string
  updateVersion: string | null
}

export interface HostMessages {
  snapshot: LobbySnapshot
  settings: Settings
  history: MatchRecord[]
  lookupResult: PlayerStats
}

export type UiMessage =
  | { type: 'ready' }
  | { type: 'lookup'; name: string; platform: Platform }
  | { type: 'setApiKey'; key: string }
  | { type: 'setRichPresence'; enabled: boolean }
  | { type: 'setNotify'; enabled: boolean }
  | { type: 'setOverlay'; enabled?: boolean; corner?: OverlayCorner }
  | { type: 'applyUpdate' }
  | { type: 'window'; action: 'drag' | 'minimize' | 'close' }

interface WebView {
  postMessage(message: unknown): void
  addEventListener(type: 'message', listener: (e: MessageEvent) => void): void
  removeEventListener(type: 'message', listener: (e: MessageEvent) => void): void
}

declare global {
  interface Window {
    chrome?: { webview?: WebView }
  }
}

const webview = window.chrome?.webview

/** False when the page is opened in a normal browser instead of the desktop app. */
export const isHosted = webview !== undefined

export function send(message: UiMessage) {
  webview?.postMessage(message)
}

export function on<K extends keyof HostMessages>(type: K, handler: (data: HostMessages[K]) => void) {
  const listener = (e: MessageEvent) => {
    if (e.data?.type === type) handler(e.data.data)
  }
  webview?.addEventListener('message', listener)
  return () => webview?.removeEventListener('message', listener)
}

/** Stats for the current mode's bucket, falling back to all modes (mirrors PlayerStats.For in C#). */
export function statsFor(p: PlayerStats, bucket: string | null): ModeStats | null {
  return (bucket && p.byMode?.[bucket]) || p.overall
}

export const threatLabel: Record<Threat, string> = {
  BotLikely: 'Bot?',
  Casual: 'Casual',
  Average: 'Average',
  Skilled: 'Skilled',
  Sweat: 'Sweat',
}

export const isAnonymous = (name: string) => /\[\d+\]$/.test(name)
