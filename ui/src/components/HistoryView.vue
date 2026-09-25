<script setup lang="ts">
import { computed, ref } from 'vue'
import { isAnonymous, threatLabel, type MatchRecord, type SessionRecord, type Threat } from '../bridge'
import SessionsView from './SessionsView.vue'

const props = defineProps<{ matches: MatchRecord[]; sessions: SessionRecord[] }>()
const emit = defineEmits<{ open: [accountId: string | null, name: string | null] }>()

const view = ref<'matches' | 'sessions'>('matches')
type Range = 'today' | 'all'
const range = ref<Range>('today')

const dayKey = (iso: string) => new Date(iso).toDateString()
const minutes = (m: MatchRecord) => (m.endedUtc ? (Date.parse(m.endedUtc) - Date.parse(m.startedUtc)) / 60_000 : null)

const inRange = computed(() =>
  range.value === 'all' ? props.matches : props.matches.filter((m) => dayKey(m.startedUtc) === new Date().toDateString()),
)

function mostCommon<T>(items: T[]): { value: T; count: number } | null {
  const counts = new Map<T, number>()
  for (const i of items) counts.set(i, (counts.get(i) ?? 0) + 1)
  let best: { value: T; count: number } | null = null
  for (const [value, count] of counts) if (!best || count > best.count) best = { value, count }
  return best
}

const summary = computed(() => {
  const ms = inRange.value
  const tracked = ms.filter((m) => m.kills !== null)
  const eliminators = ms.map((m) => m.eliminatedBy).filter((n): n is string => !!n)
  const kds = ms.map((m) => m.eliminatorKd).filter((k): k is number => k !== null)
  return {
    count: ms.length,
    played: Math.round(ms.reduce((sum, m) => sum + (minutes(m) ?? 0), 0)),
    kills: tracked.length ? tracked.reduce((sum, m) => sum + (m.kills ?? 0), 0) : null,
    tracked: tracked.length,
    wins: ms.filter((m) => m.won).length,
    topMode: mostCommon(ms.map((m) => m.mode)),
    eliminated: eliminators.length,
    nemesis: mostCommon(eliminators.filter((n) => !isAnonymous(n))),
    hidden: eliminators.filter(isAnonymous).length,
    topThreat: mostCommon(ms.map((m) => m.eliminatorThreat).filter((t): t is Threat => !!t)),
    withThreat: ms.filter((m) => m.eliminatorThreat).length,
    avgEliminatorKd: kds.length ? kds.reduce((a, b) => a + b, 0) / kds.length : null,
  }
})

const threatGroup: Record<Threat, string> = {
  Sweat: 'Sweats',
  Skilled: 'Skilled players',
  Average: 'Average players',
  Casual: 'Casual players',
  BotLikely: 'Bots',
}

// A named nemesis needs at least two eliminations. Otherwise describe the kind of player that
// eliminates you most, counted only among eliminators whose stats are known.
const nemesis = computed(() => {
  const s = summary.value
  if (s.nemesis && s.nemesis.count >= 2) return { title: s.nemesis.value, detail: `eliminated you ${s.nemesis.count}×`, name: s.nemesis.value }
  if (s.topThreat && s.topThreat.count >= 2)
    return { title: threatGroup[s.topThreat.value], detail: `${s.topThreat.count} of ${s.withThreat} eliminators with known stats`, threat: s.topThreat.value }
  if (s.hidden >= 2) return { title: 'Streamer Mode', detail: `hidden players: ${s.hidden} of ${s.eliminated} eliminations` }
  return null
})

// Matches grouped by day, newest first, flattened for the table (a header row per day).
const rows = computed(() => {
  const out: ({ kind: 'day'; label: string; count: number } | { kind: 'match'; m: MatchRecord })[] = []
  let current = ''
  for (const m of inRange.value) {
    const key = dayKey(m.startedUtc)
    if (key !== current) {
      current = key
      out.push({ kind: 'day', label: dayLabel(key), count: inRange.value.filter((x) => dayKey(x.startedUtc) === key).length })
    }
    out.push({ kind: 'match', m })
  }
  return out
})

function dayLabel(key: string) {
  const d = new Date(key)
  const diff = Math.round((new Date(new Date().toDateString()).getTime() - d.getTime()) / 86_400_000)
  if (diff === 0) return 'Today'
  if (diff === 1) return 'Yesterday'
  return d.toLocaleDateString('en-GB', { weekday: 'long', day: 'numeric', month: 'long' })
}

const time = (iso: string) => new Date(iso).toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' })

function duration(m: MatchRecord) {
  const min = minutes(m)
  if (min === null) return '–'
  return min < 1 ? '<1 min' : `${Math.round(min)} min`
}

// Only your own party is known; random teammates filled by matchmaking aren't in the log.
const party = (size: number) => (size === 1 ? 'Solo / no party' : `Party of ${size}`)
const hours = (min: number) => (min >= 60 ? `${Math.floor(min / 60)}h ${String(min % 60).padStart(2, '0')}` : `${min} min`)
</script>

<template>
  <div class="page">
    <div class="page-header">
      <div>
        <h1>History</h1>
        <p>Every match read from Fortnite's log, including recent sessions played before the app was installed.</p>
      </div>
      <div class="controls">
        <div class="seg" role="tablist" aria-label="View">
          <button type="button" role="tab" :aria-selected="view === 'matches'" :class="{ on: view === 'matches' }" @click="view = 'matches'">Matches</button>
          <button type="button" role="tab" :aria-selected="view === 'sessions'" :class="{ on: view === 'sessions' }" @click="view = 'sessions'">Sessions</button>
        </div>
        <div v-if="view === 'matches'" class="seg" role="tablist" aria-label="Period">
          <button type="button" role="tab" :aria-selected="range === 'today'" :class="{ on: range === 'today' }" @click="range = 'today'">Today</button>
          <button type="button" role="tab" :aria-selected="range === 'all'" :class="{ on: range === 'all' }" @click="range = 'all'">All time</button>
        </div>
      </div>
    </div>

    <SessionsView v-if="view === 'sessions'" :matches="matches" :sessions="sessions" @open="(id, n) => emit('open', id, n)" />

    <template v-else>
      <div class="tiles">
        <div class="tile"><span class="label">Matches</span><span class="value">{{ summary.count }}</span></div>
        <div class="tile"><span class="label">Time in matches</span><span class="value">{{ hours(summary.played) }}</span></div>
        <div class="tile" :title="summary.tracked ? `From ${summary.tracked} matches with tracked results` : 'Kills are tracked for matches played with the app running'">
          <span class="label">Kills</span><span class="value">{{ summary.kills ?? '–' }}</span>
        </div>
        <div class="tile"><span class="label">Wins</span><span class="value" :class="{ gold: summary.wins }">{{ summary.wins }}</span></div>
        <div class="tile wide">
          <span class="label">Most played</span>
          <span class="big">{{ summary.topMode?.value ?? '–' }}</span>
          <span v-if="summary.topMode" class="small">{{ summary.topMode.count }} of {{ summary.count }} matches</span>
        </div>
        <div class="tile wide nemesis">
          <span class="label">Nemesis</span>
          <button
            v-if="nemesis?.name"
            type="button"
            class="big link"
            :title="`Open ${nemesis.name}'s profile`"
            @click="emit('open', null, nemesis.name)"
          >
            {{ nemesis.title }}
          </button>
          <span v-else class="big" :class="nemesis?.threat ? `t-${nemesis.threat}` : ''">{{ nemesis?.title ?? '–' }}</span>
          <span class="small">
            {{ nemesis?.detail ?? 'Not enough eliminations yet' }}<template v-if="summary.avgEliminatorKd !== null"> · average eliminator K/D {{ summary.avgEliminatorKd.toFixed(2) }}</template>
          </span>
        </div>
      </div>

      <div v-if="!inRange.length" class="card empty">
        {{ range === 'today' ? 'No matches yet today.' : "Matches appear here as you play. Older sessions are imported from Fortnite's recent logs." }}
      </div>

      <div v-else class="table-wrap">
        <table class="data">
          <thead>
            <tr>
              <th>Time</th>
              <th>Mode</th>
              <th>Party</th>
              <th>Result</th>
              <th class="num" title="Kills are tracked for matches played with the app running">Kills</th>
              <th class="num">Duration</th>
              <th>Eliminated by</th>
            </tr>
          </thead>
          <tbody>
            <template v-for="(row, i) in rows" :key="i">
              <tr v-if="row.kind === 'day'" class="day">
                <td colspan="7">{{ row.label }} <span class="faint">· {{ row.count }} {{ row.count === 1 ? 'match' : 'matches' }}</span></td>
              </tr>
              <tr v-else :class="{ won: row.m.won }">
                <td class="muted">{{ time(row.m.startedUtc) }}</td>
                <td class="mode" :title="row.m.playlist ?? undefined">{{ row.m.mode }}</td>
                <td class="muted">{{ party(row.m.squadSize) }}</td>
                <td>
                  <span v-if="row.m.won" class="win">Victory</span>
                  <span v-else-if="!row.m.finished" class="faint">Left early</span>
                  <span v-else class="muted">Eliminated</span>
                </td>
                <td class="num">{{ row.m.kills ?? '–' }}</td>
                <td class="num muted">{{ duration(row.m) }}</td>
                <td>
                  <template v-if="row.m.eliminatedBy">
                    <button
                      v-if="!isAnonymous(row.m.eliminatedBy)"
                      type="button"
                      class="link"
                      :class="row.m.eliminatorThreat ? `t-${row.m.eliminatorThreat}` : ''"
                      @click="emit('open', null, row.m.eliminatedBy)"
                    >
                      {{ row.m.eliminatedBy }}
                    </button>
                    <span v-else class="muted">Streamer Mode player</span>
                    <span v-if="row.m.eliminatorThreat" class="threat faint"> · {{ threatLabel[row.m.eliminatorThreat] }}</span>
                  </template>
                  <span v-else class="faint">–</span>
                </td>
              </tr>
            </template>
          </tbody>
        </table>
      </div>
    </template>
  </div>
</template>

<style scoped>
.controls {
  display: flex;
  gap: var(--s3);
  flex-wrap: wrap;
}
.tiles {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr)) repeat(2, minmax(0, 1.6fr));
  gap: var(--s3);
}
@media (max-width: 1250px) {
  .tiles {
    grid-template-columns: repeat(4, minmax(0, 1fr));
  }
  .tile.wide {
    grid-column: span 2;
  }
}
.tile {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: var(--s4);
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
}
.label {
  font-family: var(--display);
  font-weight: 700;
  font-size: 13px;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--muted);
}
.value {
  font-family: var(--display);
  font-weight: 800;
  font-size: 34px;
  line-height: 1.05;
  font-variant-numeric: tabular-nums;
}
.value.gold {
  color: var(--rarity-legendary);
}
.big {
  font-family: var(--display);
  font-weight: 800;
  font-size: 24px;
  line-height: 1.15;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  text-align: left;
}
.nemesis .big {
  color: var(--danger);
}
.small {
  font-size: 13px;
  color: var(--muted);
}
.link {
  background: none;
  border: none;
  padding: 0;
  font: inherit;
  color: inherit;
  cursor: pointer;
}
.link:hover {
  text-decoration: underline;
  text-underline-offset: 3px;
}
.empty {
  color: var(--muted);
  text-align: center;
}
tr.day td {
  background: var(--surface-2);
  font-family: var(--display);
  font-weight: 800;
  font-size: 15px;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  padding-top: var(--s2);
  padding-bottom: var(--s2);
}
tr.won td {
  background: color-mix(in srgb, var(--rarity-legendary) 9%, transparent);
}
td.mode {
  font-family: var(--display);
  font-weight: 800;
  font-size: 17px;
}
.win {
  font-family: var(--display);
  font-weight: 800;
  font-size: 13px;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: #1d1400;
  background: var(--rarity-legendary);
  border-radius: 3px;
  padding: 1px 8px;
}
.t-Sweat { color: var(--danger); }
.t-Skilled { color: var(--rarity-epic); }
.t-Average { color: var(--rarity-rare); }
.t-Casual { color: var(--rarity-uncommon); }
.t-BotLikely { color: var(--rarity-common); }
.threat {
  font-size: 13px;
}
</style>
