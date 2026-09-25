<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { relationLabel, type LeaderboardEntry, type Relation, type RankProgress } from '../bridge'
import RankBadge from './RankBadge.vue'

const props = defineProps<{ entries: LeaderboardEntry[] | null }>()
const emit = defineEmits<{ refresh: []; open: [accountId: string | null, name: string | null] }>()

onMounted(() => emit('refresh'))

type View = 'ranks' | 'stats'
const view = ref<View>('ranks')

type Filter = 'all' | 'Friend' | 'Party' | 'Followed'
const filters: { id: Filter; label: string }[] = [
  { id: 'all', label: 'Everyone' },
  { id: 'Friend', label: 'Friends' },
  { id: 'Party', label: 'Party' },
  { id: 'Followed', label: 'Following' },
]
const filter = ref<Filter>('all')
const text = ref('')

// Ranked modes someone on the board played this season, most popular first: one column each.
const modes = computed(() => {
  const counts = new Map<string, { name: string; count: number }>()
  for (const e of props.entries ?? [])
    for (const r of e.ranks)
      if (r.isCurrentSeason) counts.set(r.track, { name: r.trackName, count: (counts.get(r.track)?.count ?? 0) + 1 })
  return [...counts.entries()].sort((a, b) => b[1].count - a[1].count).map(([track, v]) => ({ track, name: v.name }))
})

type StatKey = 'kd' | 'winRate' | 'wins' | 'matches' | 'killsPerMatch'
const statColumns: { key: StatKey; label: string; title: string }[] = [
  { key: 'kd', label: 'K/D', title: 'Kills per death, this season, all modes' },
  { key: 'winRate', label: 'Win rate', title: 'Share of matches won, this season' },
  { key: 'wins', label: 'Wins', title: 'Victory Royales this season' },
  { key: 'matches', label: 'Matches', title: 'Matches played this season' },
  { key: 'killsPerMatch', label: 'Kills / match', title: 'Average kills per match this season' },
]

// Sort key: a ranked mode's track id, or a stat.
const sortKey = ref<string | null>(null)
const sortDesc = ref(true)
const activeSort = computed(() => sortKey.value ?? (view.value === 'ranks' ? (modes.value[0]?.track ?? 'kd') : 'kd'))

function sortBy(key: string) {
  if (activeSort.value === key) sortDesc.value = !sortDesc.value
  else {
    sortKey.value = key
    sortDesc.value = true
  }
}

const rankIn = (e: LeaderboardEntry, track: string): RankProgress | null =>
  e.ranks.find((r) => r.track === track && r.isCurrentSeason) ?? null

// Higher rank first, then progress; Unreal is ordered by leaderboard position (lower is better).
const rankScore = (r: RankProgress | null) => (!r ? -1 : r.position !== null ? 1000 + 1 / r.position : r.current + r.progress)

function sortValue(e: LeaderboardEntry): number {
  const key = activeSort.value
  if (modes.value.some((m) => m.track === key)) return rankScore(rankIn(e, key))
  const o = e.stats.overall
  return o ? o[key as StatKey] : -1
}

const rows = computed(() => {
  const q = text.value.trim().toLowerCase()
  const list = (props.entries ?? []).filter(
    (e) =>
      (filter.value === 'all' || e.relation === (filter.value as Relation) || (filter.value === 'Party' && e.relation === 'You')) &&
      (!q || (e.name ?? '').toLowerCase().includes(q)),
  )
  // Missing values always sink to the bottom, whatever the direction.
  return list.sort((a, b) => {
    const va = sortValue(a)
    const vb = sortValue(b)
    if (va < 0 || vb < 0) return vb - va
    return sortDesc.value ? vb - va : va - vb
  })
})

const loaded = computed(() => props.entries?.filter((e) => e.stats.status !== 'Loading').length ?? 0)
const total = computed(() => props.entries?.length ?? 0)

const displayName = (e: LeaderboardEntry) =>
  e.name ?? (e.stats.status === 'Private' ? 'Private profile' : e.stats.status === 'Loading' ? 'Loading…' : 'Unknown player')

function formatStat(e: LeaderboardEntry, key: StatKey) {
  const o = e.stats.overall
  if (!o) return '–'
  switch (key) {
    case 'kd': return o.kd.toFixed(2)
    case 'winRate': return `${o.winRate.toFixed(1)}%`
    case 'killsPerMatch': return o.killsPerMatch.toFixed(2)
    default: return String(o[key])
  }
}

const arrow = (key: string) => (activeSort.value === key ? (sortDesc.value ? ' ↓' : ' ↑') : '')
</script>

<template>
  <div class="page">
    <div class="page-header">
      <div>
        <h1>Leaderboard</h1>
        <p>You, your party, friends who played ranked this season, and players you follow. Click a column to sort, or a player to open their profile.</p>
      </div>
      <div class="seg" role="tablist" aria-label="Columns">
        <button type="button" role="tab" :aria-selected="view === 'ranks'" :class="{ on: view === 'ranks' }" @click="view = 'ranks'; sortKey = null">Ranks</button>
        <button type="button" role="tab" :aria-selected="view === 'stats'" :class="{ on: view === 'stats' }" @click="view = 'stats'; sortKey = null">Stats</button>
      </div>
    </div>

    <div class="toolbar">
      <div class="seg" role="tablist" aria-label="Show">
        <button v-for="f in filters" :key="f.id" type="button" role="tab" :aria-selected="filter === f.id" :class="{ on: filter === f.id }" @click="filter = f.id">
          {{ f.label }}
        </button>
      </div>
      <input v-model="text" class="filter" placeholder="Filter by name" aria-label="Filter by name" />
      <span v-if="entries && loaded < total" class="progress" role="status">
        Loading stats {{ loaded }}/{{ total }}
        <span class="bar"><span :style="{ width: `${(100 * loaded) / Math.max(1, total)}%` }" /></span>
      </span>
    </div>

    <div v-if="!entries" class="skeleton" style="height: 320px" />
    <div v-else-if="!entries.length" class="card empty">Nobody to rank yet. Launch Fortnite once so your friends' ranks can be read from the game.</div>

    <div v-else class="table-wrap">
      <table class="data">
        <thead>
          <tr>
            <th class="pos">#</th>
            <th>Player</th>
            <template v-if="view === 'ranks'">
              <th
                v-for="m in modes"
                :key="m.track"
                class="sortable"
                :class="{ sorted: activeSort === m.track }"
                :title="`Rank this season in ${m.name}. Click to sort.`"
                @click="sortBy(m.track)"
              >
                {{ m.name }}{{ arrow(m.track) }}
              </th>
              <th class="num sortable" :class="{ sorted: activeSort === 'kd' }" :title="statColumns[0].title" @click="sortBy('kd')">K/D{{ arrow('kd') }}</th>
            </template>
            <template v-else>
              <th
                v-for="c in statColumns"
                :key="c.key"
                class="num sortable"
                :class="{ sorted: activeSort === c.key }"
                :title="c.title"
                @click="sortBy(c.key)"
              >
                {{ c.label }}{{ arrow(c.key) }}
              </th>
            </template>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="(e, i) in rows"
            :key="e.accountId ?? e.name ?? i"
            class="clickable"
            :class="{ you: e.relation === 'You' }"
            tabindex="0"
            @click="emit('open', e.accountId, e.accountId ? null : e.name)"
            @keydown.enter="emit('open', e.accountId, e.accountId ? null : e.name)"
          >
            <td class="pos" :class="{ podium: i < 3 }">{{ i + 1 }}</td>
            <td class="who">
              <span class="player-name" :class="{ faint: !e.name }">{{ displayName(e) }}</span>
              <span class="rel" :class="e.relation">{{ relationLabel[e.relation] }}</span>
            </td>
            <template v-if="view === 'ranks'">
              <td v-for="m in modes" :key="m.track" class="rank-cell">
                <RankBadge v-if="rankIn(e, m.track)" :rank="rankIn(e, m.track)!" size="md" />
                <span v-else class="faint" title="Not ranked in this mode this season">–</span>
              </td>
              <td class="num">
                <span v-if="e.stats.status === 'Loading'" class="skeleton cell-skeleton" />
                <span v-else class="stat-value" :class="e.stats.overall ? `r-${e.stats.overall.kdRarity}` : ''">{{ formatStat(e, 'kd') }}</span>
              </td>
            </template>
            <template v-else>
              <td v-for="c in statColumns" :key="c.key" class="num">
                <span v-if="e.stats.status === 'Loading'" class="skeleton cell-skeleton" />
                <span
                  v-else
                  class="stat-value"
                  :class="c.key === 'kd' && e.stats.overall ? `r-${e.stats.overall.kdRarity}` : c.key === 'winRate' && e.stats.overall ? `r-${e.stats.overall.winRateRarity}` : ''"
                >
                  {{ formatStat(e, c.key) }}
                </span>
              </td>
            </template>
          </tr>
        </tbody>
      </table>
    </div>

    <p class="legend">
      Ranks are this season's, as Fortnite shares them for you, your party and friends; "–" means not ranked in that mode this season.
      Stats are this season, all modes. K/D colours: <span class="r-Common key">under 1</span> <span class="r-Uncommon key">1–2</span>
      <span class="r-Rare key">2–3</span> <span class="r-Epic key">3–5</span> <span class="r-Legendary key">5+</span>.
      Private profiles hide their name and stats.
    </p>
  </div>
</template>

<style scoped>
.toolbar {
  display: flex;
  align-items: center;
  gap: var(--s4);
  flex-wrap: wrap;
}
.filter {
  flex: 0 1 260px;
}
.progress {
  display: flex;
  align-items: center;
  gap: var(--s2);
  margin-left: auto;
  font-size: 13px;
  color: var(--muted);
}
.progress .bar {
  width: 120px;
  height: 4px;
  border-radius: 2px;
  background: var(--surface-2);
  overflow: hidden;
}
.progress .bar span {
  display: block;
  height: 100%;
  background: var(--accent);
  transition: width 0.3s ease;
}
.empty {
  color: var(--muted);
  text-align: center;
}
th.pos,
td.pos {
  width: 56px;
  text-align: center;
}
td.pos {
  font-family: var(--display);
  font-weight: 800;
  font-size: 20px;
  color: var(--faint);
}
td.pos.podium {
  color: var(--rarity-legendary);
}
tr.you td {
  background: color-mix(in srgb, var(--accent) 9%, transparent);
}
.who {
  min-width: 200px;
}
.player-name {
  display: block;
  font-family: var(--display);
  font-weight: 800;
  font-size: 20px;
  line-height: 1.15;
}
.rel {
  font-size: 12px;
  color: var(--faint);
}
.rel.You,
.rel.Party {
  color: var(--accent);
}
.rel.Followed {
  color: var(--rarity-epic);
}
.rank-cell {
  min-width: 150px;
}
.cell-skeleton {
  display: inline-block;
  width: 44px;
  height: 18px;
}
.legend {
  margin: 0;
  font-size: 13px;
  color: var(--faint);
  line-height: 1.7;
}
.key {
  color: var(--r);
  font-weight: 600;
}
</style>
