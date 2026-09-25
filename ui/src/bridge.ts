// Messages exchanged with the .NET host through WebView2 (see UiBridge.cs).

export type StatsStatus = 'Ok' | 'Private' | 'NotFound' | 'NoApiKey' | 'Error' | 'Hidden'
export type Platform = 'epic' | 'psn' | 'xbl'

export interface PlayerStats {
  accountId: string | null
  epicName: string | null
  status: StatsStatus
  wins: number | null
  winRate: number | null
  kd: number | null
  kills: number | null
  matches: number | null
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
}

export interface MatchRecord {
  startedUtc: string
  endedUtc: string | null
  mode: string
  playlist: string | null
  squadSize: number
  finished: boolean
  eliminatedBy: string | null
}

export interface Settings {
  hasApiKey: boolean
  richPresence: { available: boolean; enabled: boolean }
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
  | { type: 'applyUpdate' }

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
