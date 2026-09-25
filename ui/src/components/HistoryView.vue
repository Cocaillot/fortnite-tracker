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
  if (s.nemesis && s.nemesis.count >= 2) return { title: s.nemesis.value, detail: `eliminated you ${s.nemesis.count}×` }
  if (s.topThreat && s.topThreat.count >= 2) {
    return {
      title: threatGroup[s.topThreat.value],
      detail: `${s.topThreat.count} of ${s.withThreat} eliminators with known stats`,
      threat: s.topThreat.value,
    }
  }
  if (s.hidden >= 2) return { title: 'Streamer Mode', detail: `players hidden by Streamer Mode: ${s.hidden} of ${s.eliminated} eliminations` }
  return null
})

const days = computed(() => {
  const groups = new Map<string, MatchRecord[]>()
  for (const m of inRange.value) {
    const key = dayKey(m.startedUtc)
    groups.set(key, [...(groups.get(key) ?? []), m])
  }
  return [...groups.entries()].map(([key, matches]) => ({ label: dayLabel(key), matches }))
})

function dayLabel(key: string) {
  const d = new Date(key)
  const diff = Math.round((new Date(new Date().toDateString()).getTime() - d.getTime()) / 86_400_000)
  if (diff === 0) return 'Today'
  if (diff === 1) return 'Yesterday'
  return d.toLocaleDateString('en-GB', { weekday: 'long', day: 'numeric', month: 'short' })
}

const time = (iso: string) => new Date(iso).toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' })

function duration(m: MatchRecord) {
  const min = minutes(m)
  if (min === null) return m.finished ? '' : 'Left early'
  return min < 1 ? '<1 min' : `${Math.round(min)} min`
}

// Only your own party is known; random teammates filled by matchmaking aren't in the log.
const party = (size: number) => (size === 1 ? 'No party' : `Party of ${size}`)
const opponent = (name: string) => (isAnonymous(name) ? 'a Streamer Mode player' : name)
const hours = (min: number) => (min >= 60 ? `${Math.floor(min / 60)}h ${String(min % 60).padStart(2, '0')}` : `${min} min`)
</script>

<template>
  <section class="history">
    <div class="range views" role="tablist" aria-label="View">
      <button type="button" role="tab" :aria-selected="view === 'matches'" :class="{ on: view === 'matches' }" @click="view = 'matches'">Matches</button>
      <button type="button" role="tab" :aria-selected="view === 'sessions'" :class="{ on: view === 'sessions' }" @click="view = 'sessions'">Sessions</button>
    </div>

    <SessionsView v-if="view === 'sessions'" :matches="matches" :sessions="sessions" @open="(id, n) => emit('open', id, n)" />

    <template v-else>
    <div class="range" role="tablist" aria-label="Period">
      <button type="button" role="tab" :aria-selected="range === 'today'" :class="{ on: range === 'today' }" @click="range = 'today'">
        Today
      </button>
      <button type="button" role="tab" :aria-selected="range === 'all'" :class="{ on: range === 'all' }" @click="range = 'all'">
        All time
      </button>
    </div>

    <div class="tiles">
      <div class="tile"><span class="label">Matches</span><span class="value">{{ summary.count }}</span></div>
      <div class="tile"><span class="label">Time played</span><span class="value">{{ hours(summary.played) }}</span></div>
      <div class="tile" :title="summary.tracked ? `From ${summary.tracked} matches with tracked results` : 'Kills are tracked from matches played with 0.3.0 or later'">
        <span class="label">Kills</span><span class="value">{{ summary.kills ?? '–' }}</span>
      </div>
      <div class="tile"><span class="label">Wins</span><span class="value" :class="{ gold: summary.wins }">{{ summary.wins }}</span></div>
    </div>

    <div v-if="summary.topMode || nemesis" class="insights">
      <div v-if="summary.topMode" class="insight">
        <span class="label">Most played</span>
        <span class="big">{{ summary.topMode.value }}</span>
        <span class="small">{{ summary.topMode.count }} of {{ summary.count }} matches</span>
      </div>
      <div v-if="nemesis" class="insight nemesis">
        <span class="label">Nemesis</span>
        <span class="big" :class="nemesis.threat ? `t-${nemesis.threat}` : ''">{{ nemesis.title }}</span>
        <span class="small">
          {{ nemesis.detail }}<template v-if="summary.avgEliminatorKd !== null"> · avg eliminator K/D {{ summary.avgEliminatorKd.toFixed(2) }}</template>
        </span>
      </div>
    </div>

    <p v-if="!inRange.length" class="empty">
      {{ range === 'today' ? 'No matches yet today.' : 'Matches appear here as you play. Older sessions are imported from Fortnite\'s recent logs.' }}
    </p>

    <div v-for="day in days" :key="day.label" class="day">
      <h2 class="section-title">{{ day.label }}</h2>
      <ul>
        <li v-for="m in day.matches" :key="m.startedUtc" :class="{ won: m.won }">
          <span class="time">{{ time(m.startedUtc) }}</span>
          <span class="mode" :title="m.playlist ?? undefined">
            {{ m.mode }}
            <span v-if="m.won" class="win">Win</span>
          </span>
          <span class="detail">
            {{ party(m.squadSize) }}
            <template v-if="m.eliminatedBy">
              · by
              <span
                v-if="!isAnonymous(m.eliminatedBy)"
                class="player-link"
                :class="m.eliminatorThreat ? `t-${m.eliminatorThreat}` : ''"
                role="button"
                tabindex="0"
                @click="emit('open', null, m.eliminatedBy)"
                @keydown.enter="emit('open', null, m.eliminatedBy)"
                >{{ m.eliminatedBy }}</span
              >
              <span v-else>{{ opponent(m.eliminatedBy) }}</span>
              <template v-if="m.eliminatorThreat"> ({{ threatLabel[m.eliminatorThreat] }})</template>
            </template>
          </span>
          <span class="right">
            <span v-if="m.kills !== null" class="kills">{{ m.kills }} {{ m.kills === 1 ? 'kill' : 'kills' }}</span>
            <span class="duration" :class="{ left: !m.finished }">{{ duration(m) }}</span>
          </span>
        </li>
      </ul>
    </div>
    </template>
  </section>
</template>

<style scoped>
.history {
  display: flex;
  flex-direction: column;
  gap: 14px;
}
.range.views {
  align-self: stretch;
  display: flex;
}
.range.views button {
  flex: 1;
}
.range {
  display: inline-flex;
  align-self: flex-start;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 8px;
  padding: 2px;
}
.range button {
  border: none;
  background: none;
  border-radius: 6px;
  padding: 4px 12px;
  color: var(--muted);
  font-family: var(--display);
  font-weight: 700;
  font-size: 14px;
  letter-spacing: 0.04em;
  text-transform: uppercase;
}
.range button.on {
  background: var(--surface-2);
  color: var(--text);
}
.tiles {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 6px;
}
.tile,
.insight {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 10px;
  padding: 8px 10px;
  display: flex;
  flex-direction: column;
  min-width: 0;
}
.label {
  font-family: var(--display);
  font-weight: 700;
  font-size: 11px;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--muted);
  white-space: nowrap;
}
.value {
  font-family: var(--display);
  font-weight: 800;
  font-size: 24px;
  line-height: 1.1;
  font-variant-numeric: tabular-nums;
}
.value.gold {
  color: var(--rarity-legendary);
}
.insights {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 6px;
}
.big {
  font-family: var(--display);
  font-weight: 800;
  font-size: 19px;
  line-height: 1.15;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.nemesis .big {
  color: var(--danger);
}
.small {
  font-size: 12px;
  color: var(--muted);
}
ul {
  list-style: none;
  margin: 0;
  padding: 0;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 12px;
  overflow: hidden;
}
li {
  display: grid;
  grid-template-columns: 44px minmax(0, 1fr) auto;
  grid-template-areas: 'time mode right' 'time detail right';
  column-gap: 10px;
  padding: 8px 12px;
  border-top: 1px solid var(--border);
}
li:first-child {
  border-top: none;
}
li.won {
  background: linear-gradient(90deg, color-mix(in srgb, var(--rarity-legendary) 12%, transparent), transparent 60%);
}
.time {
  grid-area: time;
  color: var(--muted);
  font-variant-numeric: tabular-nums;
  align-self: center;
}
.mode {
  grid-area: mode;
  font-family: var(--display);
  font-weight: 800;
  font-size: 16px;
  display: flex;
  align-items: center;
  gap: 6px;
}
.win {
  font-size: 11px;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: #1d1400;
  background: var(--rarity-legendary);
  border-radius: 3px;
  padding: 0 6px;
  transform: skewX(-8deg);
}
.detail {
  grid-area: detail;
  font-size: 12px;
  color: var(--muted);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.right {
  grid-area: right;
  align-self: center;
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 2px;
}
.kills {
  font-family: var(--display);
  font-weight: 800;
  font-size: 15px;
}
.duration {
  font-variant-numeric: tabular-nums;
  font-size: 13px;
}
.duration.left {
  color: var(--faint);
  font-size: 12px;
}
.t-Sweat { color: var(--danger); }
.t-Skilled { color: var(--rarity-epic); }
.t-Average { color: var(--rarity-rare); }
.t-Casual { color: var(--rarity-uncommon); }
.t-BotLikely { color: var(--rarity-common); }
</style>
