import { computed, ref } from 'vue'
import { on, send, statsFor, type LobbySnapshot, type MatchRecord, type RankProgress } from '../bridge'
import { RANK_NAMES } from '../ranks'
import { t, tn } from '../i18n'

// Personal goals. The host stores the list as-is (goals.json); progress is computed here from
// your ranks, match history and stats.
export type Period = 'today' | 'week'
export type Goal =
  | { id: string; kind: 'rank'; track: string; trackName: string; target: number; done?: boolean }
  | { id: string; kind: 'wins' | 'matches' | 'kills'; period: Period; target: number; done?: boolean }
  | { id: string; kind: 'kd'; target: number; done?: boolean }

export const goals = ref<Goal[]>([])

export function initGoals() {
  on('goals', (g) => (goals.value = Array.isArray(g) ? (g as Goal[]) : []))
}

function save() {
  send({ type: 'setGoals', goals: JSON.parse(JSON.stringify(goals.value)) })
}

export function addGoal(goal: Goal) {
  goals.value = [...goals.value, goal]
  save()
}

export function removeGoal(id: string) {
  goals.value = goals.value.filter((g) => g.id !== id)
  save()
}

export function markDone(id: string) {
  goals.value = goals.value.map((g) => (g.id === id ? { ...g, done: true } : g))
  save()
}

const since = (period: Period) => {
  const d = new Date()
  d.setHours(0, 0, 0, 0)
  if (period === 'week') d.setDate(d.getDate() - ((d.getDay() + 6) % 7)) // Monday
  return d.getTime()
}

export function describe(g: Goal): string {
  switch (g.kind) {
    case 'rank':
      return t('Reach {rank} in {mode}', { rank: tn(RANK_NAMES[g.target] ?? `Rank ${g.target}`), mode: tn(g.trackName) })
    case 'kd':
      return t('Season K/D of {kd}', { kd: g.target.toFixed(2) })
    default: {
      const key = { wins: 'Win {n} matches', kills: 'Get {n} kills', matches: 'Play {n} matches' }[g.kind]
      return `${t(key, { n: g.target })} ${t(g.period === 'today' ? 'today' : 'this week')}`
    }
  }
}

/** Progress of each goal, 0–1, with a short "3 / 5" style text. */
export function useGoalProgress(
  snapshot: () => LobbySnapshot | null,
  ranks: () => RankProgress[],
  history: () => MatchRecord[],
) {
  return computed(() =>
    goals.value.map((g) => {
      let value = 0
      let text = ''
      if (g.kind === 'rank') {
        const r = ranks().find((x) => x.track === g.track && x.isCurrentSeason)
        const now = r ? r.current + r.progress : 0
        value = now / g.target
        text = r ? `${tn(r.rankName)} ${Math.round(r.progress * 100)}%` : t('Not ranked yet')
      } else if (g.kind === 'kd') {
        const s = snapshot()
        const me = s?.localName ? s.squad[0] : null
        const kd = me && me.status === 'Ok' ? (statsFor(me, null)?.kd ?? 0) : 0
        value = kd / g.target
        text = `${kd.toFixed(2)} / ${g.target.toFixed(2)}`
      } else {
        const from = since(g.period)
        const ms = history().filter((m) => Date.parse(m.startedUtc) >= from)
        const n =
          g.kind === 'matches' ? ms.length : g.kind === 'wins' ? ms.filter((m) => m.won).length : ms.reduce((s, m) => s + (m.kills ?? 0), 0)
        value = n / g.target
        text = `${n} / ${g.target}`
      }
      return { goal: g, value: Math.min(1, Math.max(0, value)), text }
    }),
  )
}
