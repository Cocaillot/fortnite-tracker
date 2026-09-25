<script setup lang="ts">
import { computed } from 'vue'
import type { MatchRecord } from '../bridge'

const props = defineProps<{ matches: MatchRecord[] }>()

const minutes = (m: MatchRecord) =>
  m.endedUtc ? (Date.parse(m.endedUtc) - Date.parse(m.startedUtc)) / 60_000 : null

const dayKey = (iso: string) => new Date(iso).toDateString()

const today = computed(() => {
  const todays = props.matches.filter((m) => dayKey(m.startedUtc) === new Date().toDateString())
  const played = todays.reduce((sum, m) => sum + (minutes(m) ?? 0), 0)
  return { count: todays.length, played: Math.round(played) }
})

const days = computed(() => {
  const groups = new Map<string, MatchRecord[]>()
  for (const m of props.matches) {
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
  return d.toLocaleDateString(undefined, { weekday: 'long', day: 'numeric', month: 'short' })
}

const time = (iso: string) => new Date(iso).toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' })

function duration(m: MatchRecord) {
  const min = minutes(m)
  if (min === null) return m.finished ? '' : 'Left early'
  return min < 1 ? '<1 min' : `${Math.round(min)} min`
}

const squad = (size: number) => (size === 1 ? 'Solo' : `Squad of ${size}`)
</script>

<template>
  <section class="history">
    <dl class="summary">
      <div><dt>Matches today</dt><dd>{{ today.count }}</dd></div>
      <div><dt>Time in matches today</dt><dd>{{ today.played }} min</dd></div>
    </dl>

    <p v-if="!matches.length" class="empty">
      Matches appear here as you play. Older sessions are imported from Fortnite's recent logs.
    </p>

    <div v-for="day in days" :key="day.label" class="day">
      <h2>{{ day.label }}</h2>
      <ul>
        <li v-for="m in day.matches" :key="m.startedUtc">
          <span class="time">{{ time(m.startedUtc) }}</span>
          <span class="mode" :title="m.playlist ?? undefined">{{ m.mode }}</span>
          <span class="squad">{{ squad(m.squadSize) }}</span>
          <span class="duration" :class="{ left: !m.finished }">{{ duration(m) }}</span>
        </li>
      </ul>
    </div>
  </section>
</template>

<style scoped>
.history {
  display: flex;
  flex-direction: column;
  gap: 14px;
}
.summary {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 8px;
  margin: 0;
}
.summary div {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 10px;
  padding: 10px 12px;
}
dt {
  font-size: 11px;
  color: var(--muted);
}
dd {
  margin: 2px 0 0;
  font-size: 18px;
  font-weight: 600;
  font-variant-numeric: tabular-nums;
}
ul {
  list-style: none;
  margin: 0;
  padding: 0;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 10px;
}
li {
  display: grid;
  grid-template-columns: 44px 1fr auto;
  grid-template-areas: 'time mode duration' 'time squad duration';
  column-gap: 10px;
  padding: 8px 12px;
  border-top: 1px solid var(--border);
}
li:first-child {
  border-top: none;
}
.time {
  grid-area: time;
  color: var(--muted);
  font-variant-numeric: tabular-nums;
  align-self: center;
}
.mode {
  grid-area: mode;
  font-weight: 600;
}
.squad {
  grid-area: squad;
  font-size: 12px;
  color: var(--muted);
}
.duration {
  grid-area: duration;
  align-self: center;
  font-variant-numeric: tabular-nums;
}
.duration.left {
  color: var(--muted);
  font-size: 12px;
}
</style>
