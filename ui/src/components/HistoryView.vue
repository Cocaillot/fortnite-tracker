<script setup lang="ts">
import { t, tn, tp, locale } from '../i18n'
import { computed, ref } from 'vue'
import { isAnonymous, threatLabel, type MatchRecord, type SessionRecord, type Threat } from '../bridge'
import SessionsView from './SessionsView.vue'
import TeammatesView from './TeammatesView.vue'
import TrendsView from './TrendsView.vue'
import type { StatsDay, TeammateSummary } from '../bridge'

const props = defineProps<{
  matches: MatchRecord[]
  sessions: SessionRecord[]
  teammates: TeammateSummary[] | null
  statsHistory: StatsDay[]
}>()
const emit = defineEmits<{
  open: [accountId: string | null, name: string | null]
  match: [m: MatchRecord]
  loadTeammates: []
}>()

const view = ref<'matches' | 'sessions' | 'teammates' | 'trends'>('matches')
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

// English keys, translated where shown.
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
  if (s.nemesis && s.nemesis.count >= 2) return { title: s.nemesis.value, detail: t('eliminated you {n}×', { n: s.nemesis.count }), name: s.nemesis.value }
  if (s.topThreat && s.topThreat.count >= 2)
    return { title: t(threatGroup[s.topThreat.value]), detail: t('{n} of {total} eliminators with known stats', { n: s.topThreat.count, total: s.withThreat }), threat: s.topThreat.value }
  if (s.hidden >= 2) return { title: t('Streamer Mode'), detail: t('hidden players: {n} of {total} eliminations', { n: s.hidden, total: s.eliminated }) }
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
  if (diff === 0) return t('Today')
  if (diff === 1) return t('Yesterday')
  return d.toLocaleDateString(locale(), { weekday: 'long', day: 'numeric', month: 'long' })
}

const time = (iso: string) => new Date(iso).toLocaleTimeString(locale(), { hour: '2-digit', minute: '2-digit' })

function duration(m: MatchRecord) {
  const min = minutes(m)
  if (min === null) return '–'
  return min < 1 ? '< 1 min' : `${Math.round(min)} min`
}

// Only your own party is known; random teammates filled by matchmaking aren't in the log.
const party = (size: number) => (size === 1 ? t('Solo / no party') : t('Party of {n}', { n: size }))
const untracked = computed(() =>
  t('Kills and wins are measured from your stats before and after each match, so they only exist for matches played while the app was open.'),
)
const periodLabel = computed(() => (range.value === 'today' ? t('Today') : t('All time')))

const hours = (min: number) => (min >= 60 ? `${Math.floor(min / 60)} h ${String(min % 60).padStart(2, '0')}` : `${min} min`)
</script>

<template>
  <div class="page">
    <div class="page-header">
      <div>
        <h1>{{ t('History') }}</h1>
        <p>{{ t("Every match read from Fortnite's log, including recent sessions played before the app was installed.") }}</p>
      </div>
      <div class="controls">
        <div class="seg" role="tablist" :aria-label="t('View')">
          <button type="button" role="tab" :aria-selected="view === 'matches'" :class="{ on: view === 'matches' }" @click="view = 'matches'">{{ t('Matches') }}</button>
          <button type="button" role="tab" :aria-selected="view === 'sessions'" :class="{ on: view === 'sessions' }" @click="view = 'sessions'">{{ t('Sessions') }}</button>
          <button type="button" role="tab" :aria-selected="view === 'teammates'" :class="{ on: view === 'teammates' }" @click="view = 'teammates'">{{ t('Teammates') }}</button>
          <button type="button" role="tab" :aria-selected="view === 'trends'" :class="{ on: view === 'trends' }" @click="view = 'trends'">{{ t('Trends') }}</button>
        </div>
        <div v-if="view === 'matches'" class="seg" role="tablist" :aria-label="t('Period')">
          <button type="button" role="tab" :aria-selected="range === 'today'" :class="{ on: range === 'today' }" @click="range = 'today'">{{ t('Today') }}</button>
          <button type="button" role="tab" :aria-selected="range === 'all'" :class="{ on: range === 'all' }" @click="range = 'all'">{{ t('All time') }}</button>
        </div>
      </div>
    </div>

    <SessionsView v-if="view === 'sessions'" :matches="matches" :sessions="sessions" @open="(id, n) => emit('open', id, n)" />

    <TrendsView v-else-if="view === 'trends'" :matches="matches" :stats-history="statsHistory" />

    <TeammatesView v-else-if="view === 'teammates'" :teammates="teammates" @refresh="emit('loadTeammates')" @open="(id, n) => emit('open', id, n)" />

    <template v-else>
      <h2 class="period">{{ periodLabel }} <span class="faint">· {{ tp(summary.count, '{n} match', '{n} matches') }}</span></h2>
      <div class="tiles">
        <div class="tile"><span class="label">{{ t('Matches') }}</span><span class="value">{{ summary.count }}</span></div>
        <div class="tile"><span class="label">{{ t('Time in matches') }}</span><span class="value">{{ hours(summary.played) }}</span></div>
        <div class="tile" :title="untracked">
          <span class="label">{{ t('Kills') }}</span><span class="value">{{ summary.kills ?? '–' }}</span>
          <span class="small">{{ summary.tracked ? t('in {n} of {total} matches', { n: summary.tracked, total: summary.count }) : t('Counted from your next matches') }}</span>
        </div>
        <div class="tile" :title="untracked">
          <span class="label">{{ t('Wins') }}</span><span class="value" :class="{ gold: summary.wins }">{{ summary.tracked ? summary.wins : '–' }}</span>
          <span class="small">{{ summary.tracked ? t('in {n} of {total} matches', { n: summary.tracked, total: summary.count }) : t('Counted from your next matches') }}</span>
        </div>
        <div class="tile wide">
          <span class="label">{{ t('Most played') }}</span>
          <span class="big">{{ summary.topMode ? tn(summary.topMode.value) : '–' }}</span>
          <span v-if="summary.topMode" class="small">{{ t('{n} of {total} matches', { n: summary.topMode.count, total: summary.count }) }}</span>
        </div>
        <div class="tile wide nemesis">
          <span class="label">{{ t('Nemesis') }}</span>
          <button
            v-if="nemesis?.name"
            type="button"
            class="big link"
            :title="t('Open {name}\'s profile', { name: nemesis.name })"
            @click="emit('open', null, nemesis.name)"
          >
            {{ nemesis.title }}
          </button>
          <span v-else class="big" :class="nemesis?.threat ? `t-${nemesis.threat}` : ''">{{ nemesis?.title ?? '–' }}</span>
          <span class="small">
            {{ nemesis?.detail ?? t('Not enough eliminations yet') }}<template v-if="summary.avgEliminatorKd !== null"> · {{ t('average eliminator K/D {kd}', { kd: summary.avgEliminatorKd.toFixed(2) }) }}</template>
          </span>
        </div>
      </div>

      <div v-if="!inRange.length" class="card empty">
        {{ range === 'today' ? t('No matches yet today.') : t("Matches appear here as you play. Older sessions are imported from Fortnite's recent logs.") }}
      </div>

      <div v-else class="table-wrap">
        <table class="data">
          <thead>
            <tr>
              <th>{{ t('Time') }}</th>
              <th>{{ t('Mode') }}</th>
              <th>{{ t('Party') }}</th>
              <th>{{ t('Result') }}</th>
              <th class="num" :title="t('Kills are tracked for matches played with the app running')">{{ t('Kills') }}</th>
              <th class="num">{{ t('Duration') }}</th>
              <th>{{ t('Eliminated by') }}</th>
            </tr>
          </thead>
          <tbody>
            <template v-for="(row, i) in rows" :key="i">
              <tr v-if="row.kind === 'day'" class="day">
                <td colspan="7">{{ row.label }} <span class="faint">· {{ tp(row.count, '{n} match', '{n} matches') }}</span></td>
              </tr>
              <tr
                v-else
                class="clickable"
                :class="{ won: row.m.won }"
                tabindex="0"
                :title="t('Open match details')"
                @click="emit('match', row.m)"
                @keydown.enter="emit('match', row.m)"
              >
                <td class="muted">{{ time(row.m.startedUtc) }}</td>
                <td class="mode" :title="row.m.playlist ?? undefined">{{ tn(row.m.mode) }}</td>
                <td class="muted">{{ party(row.m.squadSize) }}</td>
                <td>
                  <span v-if="row.m.won" class="win">{{ t('Victory') }}</span>
                  <span v-else-if="!row.m.finished" class="faint">{{ t('Left early') }}</span>
                  <span v-else class="muted">{{ t('Eliminated') }}</span>
                </td>
                <td class="num" :title="row.m.kills === null ? untracked : undefined">{{ row.m.kills ?? '–' }}</td>
                <td class="num muted">{{ duration(row.m) }}</td>
                <td>
                  <template v-if="row.m.eliminatedBy">
                    <button
                      v-if="!isAnonymous(row.m.eliminatedBy)"
                      type="button"
                      class="link"
                      :class="row.m.eliminatorThreat ? `t-${row.m.eliminatorThreat}` : ''"
                      @click.stop="emit('open', null, row.m.eliminatedBy)"
                    >
                      {{ row.m.eliminatedBy }}
                    </button>
                    <span v-else class="muted">{{ t('Streamer Mode player') }}</span>
                    <span v-if="row.m.eliminatorThreat" class="threat faint"> · {{ t(threatLabel[row.m.eliminatorThreat]) }}</span>
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
.period {
  margin: 0 0 calc(-1 * var(--s3));
  font-family: var(--display);
  font-weight: 800;
  font-size: 18px;
  letter-spacing: 0.06em;
  text-transform: uppercase;
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
/* Mode names like "Ranked Reload Duos · Zero Build" get two lines rather than an ellipsis. */
.tile.wide:not(.nemesis) .big {
  white-space: normal;
  font-size: 21px;
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
