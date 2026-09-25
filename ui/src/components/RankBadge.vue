<script setup lang="ts">
import { computed } from 'vue'
import type { RankProgress } from '../bridge'

const props = withDefaults(defineProps<{ rank: RankProgress; showTrack?: boolean; size?: 'sm' | 'md' }>(), {
  showTrack: false,
  size: 'sm',
})

const unranked = computed(() => props.rank.lastUpdatedUtc === null)
// Unreal has a leaderboard position instead of progress.
const showProgress = computed(() => !unranked.value && props.rank.position === null && props.rank.tier !== 'Beyond')
const title = computed(() => {
  const parts = [`${props.rank.trackName}: ${props.rank.rankName}`]
  if (showProgress.value) parts.push(`${Math.round(props.rank.progress * 100)}% to next rank`)
  if (!unranked.value) parts.push(`best ${props.rank.highestName}`)
  if (props.rank.lastUpdatedUtc) parts.push(`updated ${new Date(props.rank.lastUpdatedUtc).toLocaleDateString('en-GB')}`)
  return parts.join(' · ')
})
</script>

<template>
  <span class="rank" :class="[`tier-${rank.tier}`, size, { past: !rank.isCurrentSeason }]" :title="title">
    <svg class="gem" viewBox="0 0 16 16" aria-hidden="true"><path d="M8 1l6 5-6 9-6-9z" /></svg>
    <span class="text">
      <span class="name">{{ showTrack ? `${rank.trackName} · ` : '' }}{{ rank.rankName }}</span>
      <span v-if="showProgress" class="bar"><span :style="{ width: `${Math.round(rank.progress * 100)}%` }" /></span>
    </span>
  </span>
</template>

<style scoped>
.rank {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  color: var(--t);
  min-width: 0;
}
.rank.past {
  opacity: 0.55;
}
.gem {
  flex: none;
  width: 13px;
  height: 13px;
  fill: currentColor;
  filter: drop-shadow(0 0 4px color-mix(in srgb, var(--t) 50%, transparent));
}
.md .gem {
  width: 18px;
  height: 18px;
}
.text {
  display: flex;
  flex-direction: column;
  gap: 2px;
  min-width: 0;
}
.name {
  font-family: var(--display);
  font-weight: 800;
  font-size: 13px;
  letter-spacing: 0.03em;
  text-transform: uppercase;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}
.md .name {
  font-size: 16px;
}
.bar {
  display: block;
  height: 3px;
  width: 64px;
  border-radius: 2px;
  background: var(--surface-2);
  overflow: hidden;
}
.md .bar {
  width: 100%;
  min-width: 90px;
  height: 4px;
}
.bar span {
  display: block;
  height: 100%;
  background: var(--t);
}
</style>
