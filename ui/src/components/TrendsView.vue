<script setup lang="ts">
import { computed, ref } from 'vue'
import type { MatchRecord, StatsDay } from '../bridge'
import TrendChart from './TrendChart.vue'

// Day-by-day charts: activity from match history, performance from daily season-stat snapshots.
const props = defineProps<{ matches: MatchRecord[]; statsHistory: StatsDay[] }>()

const span = ref<7 | 14 | 30>(14)

const dayKey = (d: Date) => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`
const days = computed(() => {
  const out: { key: string; label: string }[] = []
  for (let i = span.value - 1; i >= 0; i--) {
    const d = new Date()
    d.setDate(d.getDate() - i)
    out.push({ key: dayKey(d), label: d.toLocaleDateString('en-GB', { day: 'numeric', month: 'short' }) })
  }
  return out
})

const perDay = computed(() => {
  const map = new Map<string, MatchRecord[]>()
  for (const m of props.matches) {
    const k = dayKey(new Date(m.startedUtc))
    map.set(k, [...(map.get(k) ?? []), m])
  }
  return map
})

const matchesPerDay = computed(() => days.value.map((d) => ({ label: d.label, value: perDay.value.get(d.key)?.length ?? 0 })))
const minutesPerDay = computed(() =>
  days.value.map((d) => ({
    label: d.label,
    value: (perDay.value.get(d.key) ?? []).reduce((s, m) => s + (m.endedUtc ? (Date.parse(m.endedUtc) - Date.parse(m.startedUtc)) / 60_000 : 0), 0),
  })),
)

const snapshots = computed(() => new Map(props.statsHistory.map((s) => [s.day, s.overall])))
const seasonKd = computed(() => days.value.map((d) => ({ label: d.label, value: snapshots.value.get(d.key)?.kd ?? null })))
const seasonWinRate = computed(() => days.value.map((d) => ({ label: d.label, value: snapshots.value.get(d.key)?.winRate ?? null })))

// A day's own K/D: kills and deaths gained since the previous snapshot.
const dailyKd = computed(() => {
  const all = [...props.statsHistory].sort((a, b) => a.day.localeCompare(b.day))
  const byDay = new Map<string, number>()
  for (let i = 1; i < all.length; i++) {
    const [a, b] = [all[i - 1]!.overall, all[i]!.overall]
    const deaths = b.deaths - a.deaths
    if (b.matches > a.matches && deaths >= 0) byDay.set(all[i]!.day, (b.kills - a.kills) / Math.max(1, deaths))
  }
  return days.value.map((d) => ({ label: d.label, value: byDay.get(d.key) ?? null }))
})

const hasSnapshots = computed(() => props.statsHistory.length >= 2)
const fmtMin = (v: number) => (v >= 60 ? `${Math.floor(v / 60)}h${String(Math.round(v % 60)).padStart(2, '0')}` : `${Math.round(v)}m`)
</script>

<template>
  <div class="trends">
    <div class="seg" role="tablist" aria-label="Period">
      <button v-for="n in [7, 14, 30] as const" :key="n" type="button" role="tab" :aria-selected="span === n" :class="{ on: span === n }" @click="span = n">
        {{ n }} days
      </button>
    </div>

    <div class="grid">
      <section class="card">
        <h3 class="card-title">Matches per day</h3>
        <TrendChart :points="matchesPerDay" />
      </section>
      <section class="card">
        <h3 class="card-title">Time in matches</h3>
        <TrendChart :points="minutesPerDay" :format="fmtMin" color="var(--rarity-epic)" />
      </section>
      <section class="card">
        <h3 class="card-title">Your K/D each day</h3>
        <TrendChart v-if="hasSnapshots" :points="dailyKd" kind="line" :format="(v) => v.toFixed(2)" color="var(--live)" />
        <p v-else class="muted">Builds up from today: the app saves your season stats once a day while it's open.</p>
      </section>
      <section class="card">
        <h3 class="card-title">Season K/D &amp; win rate</h3>
        <template v-if="hasSnapshots">
          <TrendChart :points="seasonKd" kind="line" :format="(v) => v.toFixed(2)" />
          <TrendChart :points="seasonWinRate" kind="line" :format="(v) => `${v.toFixed(1)}%`" color="var(--rarity-legendary)" />
        </template>
        <p v-else class="muted">Builds up from today: one point per day you play with the app open.</p>
      </section>
    </div>
  </div>
</template>

<style scoped>
.trends {
  display: flex;
  flex-direction: column;
  gap: var(--s4);
}
.seg {
  align-self: flex-start;
}
.grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(460px, 1fr));
  gap: var(--s4);
}
</style>
