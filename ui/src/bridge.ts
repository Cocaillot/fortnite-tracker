// Messages exchanged with the .NET host through WebView2 (see UiBridge.cs).

export type StatsStatus = 'Ok' | 'Private' | 'NotFound' | 'NoApiKey' | 'Error' | 'Hidden' | 'Loading'
export type Platform = 'epic' | 'psn' | 'xbl'
export type Rarity = 'Common' | 'Uncommon' | 'Rare' | 'Epic' | 'Legendary'
export type Threat = 'BotLikely' | 'Casual' | 'Average' | 'Skilled' | 'Sweat'
export type OverlayCorner = 'TopLeft' | 'TopRight' | 'BottomLeft' | 'BottomRight'
export type Relation = 'You' | 'Party' | 'Friend' | 'Followed' | 'Opponent'

export interface RankProgress {
  accountId: string
  track: string
  current: number
  highest: number
  progress: number
  position: number | null
  lastUpdatedUtc: string | null
  /** Each season of a mode is a separate track. */
  trackGuid: string
  rankName: string
  highestName: string
  /** Bronze, Silver, … Unreal; "Beyond" for 2026 values above Unreal; "Unranked". */
  tier: string
  trackName: string
  /** False when only the codename is known (the name is derived from it). */
  trackNameConfirmed: boolean
  isCurrentSeason: boolean
}

export interface PlayerProfile {
  accountId: string | null
  name: string
  relation: Relation
  followed: boolean
  season: PlayerStats
  lifetime: PlayerStats
  ranks: RankProgress[]
  encounters: { eliminatedYou: number; lastEliminatedYouUtc: string | null }
  /** Every season played, newest first. */
  seasons: RankProgress[]
  /** Rank points of each mode's current season, keyed by mode. */
  rankHistory: Record<string, RankPoint[]>
  /** Your record together, when they were in your party. */
  together: TeammateSummary | null
}

export interface RankPoint {
  at: string
  current: number
  progress: number
}

export interface LeaderboardEntry {
  accountId: string | null
  name: string | null
  relation: Relation
  stats: PlayerStats
  ranks: RankProgress[]
}

export interface SessionRecord {
  startedUtc: string
  lastMatchUtc: string
  baseline: ModeStats | null
  latest: ModeStats | null
  delta: ModeStats | null
}

export interface ModeStats {
  wins: number
  winRate: number
  kd: number
  kills: number
  matches: number
  top10: number
  top25: number
  minutesPlayed: number
  killsPerMatch: number
  deaths: number
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
  battlePassLevel: number | null
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
  /** Your party members at the start of the match (you excluded). */
  partyIds: string[] | null
}

export interface StatsDay {
  /** Local date, "2026-09-25". */
  day: string
  overall: ModeStats
}

export interface PlayerNote {
  accountId: string | null
  name: string
  tags: string[]
  text: string
  updatedUtc: string
}

export interface RankMove {
  track: string
  trackName: string
  before: RankPoint
  after: RankPoint
  beforeName: string
  afterName: string
  /** Progress change in percentage points (a whole rank = 100). */
  delta: number
}

export interface MatchDetail {
  match: MatchRecord
  eliminator: PlayerStats | null
  party: { accountId: string; name: string | null; stats: PlayerStats }[]
  rank: RankMove | null
}

export interface TeammateSummary {
  accountId: string
  name: string | null
  matches: number
  wins: number
  kills: number | null
  minutes: number
  lastPlayedUtc: string
  tracked: number
}

export interface Settings {
  hasApiKey: boolean
  richPresence: { available: boolean; enabled: boolean }
  notifyOnElimination: boolean
  notifyRankChanges: boolean
  discordRecap: { hasWebhook: boolean; autoPost: boolean }
  overlay: { enabled: boolean; corner: OverlayCorner }
  version: string
  updateVersion: string | null
  /** What the user picked; "auto" follows the Windows language. */
  language: 'en' | 'fr' | 'auto'
  /** The language in use. */
  effectiveLanguage: 'en' | 'fr'
  /** Global shortcuts as text ("Ctrl+Shift+F"), and whether Windows accepted them. */
  hotkeys: { window: string; overlay: string; windowOk: boolean; overlayOk: boolean }
  /** Open the app by itself when Fortnite starts. */
  launchWithFortnite: boolean
  /** False until the first-run guide is finished or skipped. */
  onboardingDone: boolean
  /** The newest version whose "What's new" was seen. */
  lastSeenVersion: string | null
  /** Whether Fortnite's log file exists on this PC. */
  fortniteFound: boolean
}

export interface HostMessages {
  snapshot: LobbySnapshot
  settings: Settings
  history: MatchRecord[]
  lookupResult: PlayerStats
  ranks: Record<string, RankProgress[]>
  sessions: SessionRecord[]
  profile: PlayerProfile
  leaderboard: LeaderboardEntry[]
  windowState: { maximized: boolean; fullscreen: boolean }
  /** The saved theme (format owned by composables/useTheme.ts), or null for the default look. */
  theme: unknown
  toast: { title: string; text: string; good: boolean }
  matchDetail: MatchDetail | null
  teammates: TeammateSummary[]
  notes: PlayerNote[]
  statsHistory: StatsDay[]
  /** Saved goals (format owned by composables/goals.ts). */
  goals: unknown
  recapResult: string
  apiKeyTest: 'ok' | 'invalid' | 'offline'
  /** Outcome of an export or backup, or null when the file dialog was cancelled. */
  dataResult: string | null
}

export type UiMessage =
  | { type: 'ready' }
  | { type: 'lookup'; name: string; platform: Platform }
  | { type: 'setApiKey'; key: string }
  | { type: 'setRichPresence'; enabled: boolean }
  | { type: 'setNotify'; enabled: boolean }
  | { type: 'setNotifyRanks'; enabled: boolean }
  | { type: 'match'; startedUtc: string }
  | { type: 'teammates' }
  | { type: 'setNote'; accountId: string | null; name: string; tags: string[]; text: string }
  | { type: 'setGoals'; goals: object[] }
  | { type: 'setDiscordRecap'; url: string | null; enabled: boolean }
  | { type: 'postRecap' }
  | { type: 'setOverlay'; enabled?: boolean; corner?: OverlayCorner }
  | { type: 'applyUpdate' }
  | { type: 'window'; action: 'drag' | 'minimize' | 'maximize' | 'fullscreen' | 'close' }
  | { type: 'profile'; accountId?: string | null; name?: string | null }
  | { type: 'follow'; accountId?: string | null; name: string; enabled: boolean }
  | { type: 'leaderboard' }
  | { type: 'setTheme'; theme: object | null }
  | { type: 'setLanguage'; lang: 'en' | 'fr' | 'auto' }
  | { type: 'setHotkeys'; window?: string; overlay?: string }
  | { type: 'setLaunchWithFortnite'; enabled: boolean }
  | { type: 'onboardingDone' }
  | { type: 'seenVersion' }
  | { type: 'testApiKey'; key: string }
  | { type: 'data'; action: 'csv' | 'backup' | 'restore' }

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

/** The rank for the mode a player played most recently (mirrors RankBook.Latest). */
export function latestRank(ranks: RankProgress[] | undefined): RankProgress | null {
  return (ranks ?? []).filter((r) => r.lastUpdatedUtc).sort((a, b) => Date.parse(b.lastUpdatedUtc!) - Date.parse(a.lastUpdatedUtc!))[0] ?? null
}

export const relationLabel: Record<Relation, string> = {
  You: 'You',
  Party: 'Party',
  Friend: 'Friend',
  Followed: 'Following',
  Opponent: 'Opponent',
}
