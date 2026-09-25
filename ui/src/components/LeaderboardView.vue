<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { relationLabel, type LeaderboardEntry, type RankProgress } from '../bridge'
import RankBadge from './RankBadge.vue'

const props = defineProps<{ entries: LeaderboardEntry[] | null }>()
const emit = defineEmits<{ refresh: []; open: [accountId: string | null, name: string | null] }>()

onMounted(() => emit('refresh'))

type Sort = 'rank' | 'kd' | 'wins' | 'winRate' | 'matches'
const sorts: { id: Sort; label: string }[] = [
  { id: 'rank', label: 'Rank' },
  { id: 'kd', label: 'K/D' },
  { id: 'wins', label: 'Wins' },
  { id: 'winRate', label: 'Win %' },
  { id: 'matches', label: 'Matches' },
]
const sort = ref<Sort>('rank')

// Ranked modes that someone on the board played this season, most popular first.
const tracks = computed(() => {
  const counts = new Map<string, { name: string; count: number }>()
  for (const e of props.entries ?? [])
    for (const r of e.ranks)
      if (r.isCurrentSeason) counts.set(r.track, { name: r.trackName, count: (counts.get(r.track)?.count ?? 0) + 1 })
  return [...counts.entries()].sort((a, b) => b[1].count - a[1].count).map(([id, v]) => ({ id, name: v.name }))
})
const track = ref<string | null>(null)
const activeTrack = computed(() => track.value ?? tracks.value[0]?.id ?? null)

const rankOf = (e: LeaderboardEntry): RankProgress | null =>
  e.ranks.find((r) => r.track === activeTrack.value && r.isCurrentSeason) ?? null

// Higher rank first, then progress; Unreal sorts by position (lower is better).
const rankScore = (r: RankProgress | null) =>
  !r ? -1 : r.position !== null ? 1000 + 1 / r.position : r.current + r.progress

const value = (e: LeaderboardEntry): number => {
  const o = e.stats.overall
  switch (sort.value) {
    case 'rank': return rankScore(rankOf(e))
    case 'kd': return o?.kd ?? -1
    case 'wins': return o?.wins ?? -1
    case 'winRate': return o?.winRate ?? -1
    case 'matches': return o?.matches ?? -1
  }
}

const rows = computed(() => [...(props.entries ?? [])].sort((a, b) => value(b) - value(a)))
const loading = computed(() => props.entries?.some((e) => e.stats.status === 'Loading') ?? true)

const displayName = (e: LeaderboardEntry) =>
  e.name ?? (e.stats.status === 'Private' ? 'Private profile' : e.stats.status === 'Loading' ? '…' : 'Unknown')
</script>

<template>
  <section class="board">
    <p class="intro">
      You, your party, friends with a rank this season, and players you follow. Open a profile to follow someone.
    </p>

    <div class="controls">
      <label v-if="sort === 'rank' && tracks.length > 1" class="select">
        <span class="sr-only">Ranked mode</span>
        <select :value="activeTrack ?? ''" aria-label="Ranked mode" @change="track = ($event.target as HTMLSelectElement).value">
          <option v-for="t in tracks" :key="t.id" :value="t.id">{{ t.name }}</option>
        </select>
      </label>
      <div class="seg" role="tablist" aria-label="Sort by">
        <button v-for="s in sorts" :key="s.id" type="button" role="tab" :aria-selected="sort === s.id" :class="{ on: sort === s.id }" @click="sort = s.id">
          {{ s.label }}
        </button>
      </div>
    </div>

    <p v-if="loading && entries?.length" class="hint loading">Loading stats… ({{ entries.filter((e) => e.stats.status !== 'Loading').length }}/{{ entries.length }})</p>

    <ol v-if="entries?.length" class="rows">
      <li
        v-for="(e, i) in rows"
        :key="e.accountId ?? e.name ?? i"
        :class="{ you: e.relation === 'You' }"
        role="button"
        tabindex="0"
        @click="emit('open', e.accountId, e.accountId ? null : e.name)"
        @keydown.enter="emit('open', e.accountId, e.accountId ? null : e.name)"
      >
        <span class="pos" :class="{ podium: i < 3 }">{{ i + 1 }}</span>
        <span class="who">
          <span class="name" :class="{ faint: !e.name }">{{ displayName(e) }}</span>
          <span class="rel">{{ relationLabel[e.relation] }}</span>
        </span>
        <span class="rank">
          <RankBadge v-if="rankOf(e)" :rank="rankOf(e)!" />
          <span v-else class="faint">–</span>
        </span>
        <span class="num" :class="e.stats.overall ? `r-${e.stats.overall.kdRarity}` : ''">
          <template v-if="e.stats.status === 'Loading'"><span class="skeleton num-skeleton" /></template>
          <template v-else-if="e.stats.overall">
            {{ sort === 'wins' ? e.stats.overall.wins : sort === 'winRate' ? `${e.stats.overall.winRate.toFixed(1)}%` : sort === 'matches' ? e.stats.overall.matches : e.stats.overall.kd.toFixed(2) }}
          </template>
          <template v-else>–</template>
        </span>
      </li>
    </ol>
    <p v-else-if="entries" class="empty">Nobody to rank yet. Launch Fortnite so your friends' ranks can be read.</p>
    <div v-else class="skeleton" style="height: 200px" />

    <p class="hint">Season stats, all modes. {{ sort === 'rank' ? 'Ranks come from Fortnite for you, your party and friends.' : '' }}</p>
  </section>
</template>

<style scoped>
.board {
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.intro {
  margin: 0;
  color: var(--muted);
  font-size: 13px;
}
.controls {
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
  align-items: center;
}
.select select {
  padding: 5px 8px;
  font-size: 13px;
}
.seg {
  display: inline-flex;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 8px;
  padding: 2px;
}
.seg button {
  border: none;
  background: none;
  border-radius: 6px;
  padding: 3px 9px;
  color: var(--muted);
  font-family: var(--display);
  font-weight: 700;
  font-size: 13px;
  letter-spacing: 0.04em;
  text-transform: uppercase;
}
.seg button.on {
  background: var(--surface-2);
  color: var(--text);
}
.loading {
  margin: 0;
}
.rows {
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
  grid-template-columns: 26px minmax(0, 1fr) auto 52px;
  gap: 8px;
  align-items: center;
  padding: 8px 12px 8px 10px;
  border-top: 1px solid var(--border);
  cursor: pointer;
}
li:first-child {
  border-top: none;
}
li:hover {
  background: var(--surface-2);
}
li.you {
  background: color-mix(in srgb, var(--accent) 10%, transparent);
}
.pos {
  font-family: var(--display);
  font-weight: 800;
  font-size: 18px;
  color: var(--faint);
  text-align: center;
}
.pos.podium {
  color: var(--rarity-legendary);
}
.who {
  display: flex;
  flex-direction: column;
  min-width: 0;
}
.name {
  font-family: var(--display);
  font-weight: 800;
  font-size: 17px;
  line-height: 1.1;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.rel {
  font-size: 11px;
  color: var(--faint);
}
.faint {
  color: var(--faint);
}
.rank {
  max-width: 120px;
  min-width: 0;
}
.num {
  font-family: var(--display);
  font-weight: 800;
  font-size: 19px;
  text-align: right;
  color: var(--r, var(--text));
  font-variant-numeric: tabular-nums;
}
.num-skeleton {
  display: inline-block;
  width: 36px;
  height: 16px;
}
.sr-only {
  position: absolute;
  width: 1px;
  height: 1px;
  overflow: hidden;
  clip: rect(0 0 0 0);
}
</style>
