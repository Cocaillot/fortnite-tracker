<script setup lang="ts">
import { t, tn, locale } from '../i18n'
import { computed } from 'vue'
import type { RankProgress } from '../bridge'
import RankEmblem from './RankEmblem.vue'

const props = withDefaults(defineProps<{ rank: RankProgress; showTrack?: boolean; size?: 'sm' | 'md' }>(), {
  showTrack: false,
  size: 'sm',
})

const unranked = computed(() => props.rank.lastUpdatedUtc === null)
// Division pips (I, II, III) for the shield tiers; the text shows the full name too.
const division = computed(() => (props.rank.current < 9 && !unranked.value ? (props.rank.current % 3) + 1 : 0))
// Unreal has a leaderboard position instead of progress.
const showProgress = computed(() => !unranked.value && props.rank.position === null && props.rank.tier !== 'Beyond')
const title = computed(() => {
  const parts = [`${tn(props.rank.trackName)}: ${tn(props.rank.rankName)}`]
  if (showProgress.value) parts.push(t('{p}% to next rank', { p: Math.round(props.rank.progress * 100) }))
  if (!unranked.value) parts.push(t('best {rank}', { rank: tn(props.rank.highestName) }))
  if (props.rank.lastUpdatedUtc) parts.push(t('updated {date}', { date: new Date(props.rank.lastUpdatedUtc).toLocaleDateString(locale()) }))
  return parts.join(' · ')
})
</script>

<template>
  <span class="rank" :class="[`tier-${rank.tier}`, size, { past: !rank.isCurrentSeason }]" :title="title">
    <RankEmblem :tier="rank.tier" :division="division" :size="size === 'md' ? 26 : 18" />
    <span class="text">
      <span class="name">{{ showTrack ? `${tn(rank.trackName)} · ` : '' }}{{ tn(rank.rankName) }}</span>
      <span v-if="showProgress" class="bar"><span :style="{ width: `${Math.round(rank.progress * 100)}%` }" /></span>
    </span>
  </span>
</template>

<style scoped>
.rank {
  display: inline-flex;
  align-items: center;
  gap: 7px;
  color: var(--t);
  min-width: 0;
}
.rank.past {
  opacity: 0.55;
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
