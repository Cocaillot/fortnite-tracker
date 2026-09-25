<script setup lang="ts">
import { computed } from 'vue'
import type { PlayerStats } from '../bridge'

const props = defineProps<{ player: PlayerStats; isYou?: boolean; fallbackName?: string | null }>()

const statusText: Record<Exclude<PlayerStats['status'], 'Ok'>, string> = {
  Private: 'Stats private: they can enable public stats in Fortnite settings',
  NotFound: 'Player not found',
  NoApiKey: 'Add your API key to load stats',
  Error: 'Stats unavailable right now',
}

const name = computed(() => props.player.epicName ?? props.fallbackName ?? 'Squad member')
const fmt = (n: number | null, digits = 0) => (n === null ? '–' : n.toFixed(digits))
</script>

<template>
  <article class="card">
    <header>
      <span class="name">{{ name }}</span>
      <span v-if="isYou" class="you">You</span>
    </header>

    <dl v-if="player.status === 'Ok'" class="stats">
      <div><dt>K/D</dt><dd>{{ fmt(player.kd, 2) }}</dd></div>
      <div><dt>Win %</dt><dd>{{ fmt(player.winRate, 1) }}</dd></div>
      <div><dt>Wins</dt><dd>{{ fmt(player.wins) }}</dd></div>
      <div><dt>Matches</dt><dd>{{ fmt(player.matches) }}</dd></div>
    </dl>
    <p v-else class="status">{{ statusText[player.status] }}</p>
  </article>
</template>

<style scoped>
.card {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 10px;
  padding: 12px 14px;
}
header {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 10px;
}
.name {
  font-weight: 600;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.you {
  font-size: 11px;
  font-weight: 600;
  color: var(--accent-ink);
  background: var(--accent);
  border-radius: 999px;
  padding: 1px 8px;
}
.stats {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 8px;
  margin: 0;
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
.status {
  margin: 0;
  color: var(--muted);
  font-size: 13px;
}
</style>
