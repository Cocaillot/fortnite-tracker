<script setup lang="ts">
import { computed } from 'vue'
import { statsFor, threatLabel, type PlayerStats, type RankProgress } from '../bridge'
import RankBadge from './RankBadge.vue'

const props = withDefaults(defineProps<{
  player: PlayerStats
  bucket: string | null
  modeLabel?: string
  isYou?: boolean
  fallbackName?: string | null
  /** Your own stats, to compare an opponent against ("2.3× your K/D"). */
  you?: PlayerStats | null
  variant?: 'squad' | 'eliminator' | 'opponent'
  /** Current-mode rank, when Fortnite shared it (you, party, friends). */
  rank?: RankProgress | null
}>(), { variant: 'squad', modeLabel: undefined, fallbackName: null, you: null, rank: null })

const emit = defineEmits<{ open: [accountId: string | null, name: string | null] }>()

const statusText: Partial<Record<PlayerStats['status'], string>> = {
  Private: 'Stats private: they can make them public in Fortnite settings',
  NotFound: 'No stats found: likely a bot, or a name that changed',
  NoApiKey: 'Add your API key in Settings to load stats',
  Error: 'Stats unavailable right now',
  Hidden: 'Streamer Mode: Fortnite hides their real name',
}

const name = computed(() =>
  props.player.status === 'Hidden'
    ? 'Streamer Mode player'
    : (props.player.epicName ?? props.fallbackName ?? 'Squad member'),
)
const stats = computed(() => (props.player.status === 'Ok' ? statsFor(props.player, props.bucket) : null))
const loading = computed(() => props.player.status === 'Loading')

const canOpen = computed(() => props.player.status !== 'Hidden' && !loading.value)
function open() {
  if (canOpen.value) emit('open', props.player.accountId, props.player.accountId ? null : name.value)
}

const versus = computed(() => {
  const mine = props.you && props.you.status === 'Ok' ? statsFor(props.you, props.bucket) : null
  if (!stats.value || !mine || mine.kd <= 0) return null
  const ratio = stats.value.kd / mine.kd
  return ratio >= 1
    ? { text: `${ratio.toFixed(1)}× your K/D`, worse: true }
    : { text: `${(1 / ratio).toFixed(1)}× lower K/D than you`, worse: false }
})

const fmt = (n: number, digits = 0) => n.toFixed(digits)
</script>

<template>
  <article class="card" :class="[variant, stats ? `r-${stats.kdRarity}` : 'r-Common']">
    <header>
      <span v-if="loading" class="skeleton name-skeleton" />
      <span
        v-else
        class="name"
        :class="{ 'player-link': canOpen }"
        :title="canOpen ? `${name}: open profile` : name"
        :role="canOpen ? 'button' : undefined"
        :tabindex="canOpen ? 0 : undefined"
        @click="open"
        @keydown.enter="open"
        >{{ name }}</span
      >
      <span v-if="isYou" class="tag you">You</span>
      <span v-if="player.threat && variant !== 'squad'" class="tag threat" :class="`t-${player.threat}`">
        {{ threatLabel[player.threat] }}
      </span>
      <span v-if="stats && modeLabel" class="mode">{{ modeLabel }}</span>
    </header>

    <div v-if="rank" class="rank-row"><RankBadge :rank="rank" show-track /></div>

    <div v-if="loading" class="stats">
      <div v-for="i in 4" :key="i" class="tile"><span class="skeleton value-skeleton" /></div>
    </div>

    <div v-else-if="stats" class="stats">
      <div class="tile rarity" :class="`r-${stats.kdRarity}`">
        <span class="label">K/D</span>
        <span class="value">{{ fmt(stats.kd, 2) }}</span>
      </div>
      <div class="tile rarity" :class="`r-${stats.winRateRarity}`">
        <span class="label">Win %</span>
        <span class="value">{{ fmt(stats.winRate, 1) }}</span>
      </div>
      <div class="tile">
        <span class="label">Wins</span>
        <span class="value plain">{{ stats.wins }}</span>
      </div>
      <div class="tile">
        <span class="label">Matches</span>
        <span class="value plain">{{ stats.matches }}</span>
      </div>
    </div>

    <p v-else class="status">{{ statusText[player.status] }}</p>

    <p v-if="versus" class="versus" :class="{ worse: versus.worse }">{{ versus.text }}</p>
  </article>
</template>

<style scoped>
.card {
  position: relative;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 12px;
  padding: 12px 14px 12px 17px;
  overflow: hidden;
}
/* Rarity strip on the left edge, from the player's K/D. */
.card::before {
  content: '';
  position: absolute;
  inset: 0 auto 0 0;
  width: 4px;
  background: var(--r);
}
.card.eliminator {
  border-color: color-mix(in srgb, var(--danger) 45%, var(--border));
  background: linear-gradient(160deg, color-mix(in srgb, var(--danger) 10%, var(--surface)) 0%, var(--surface) 55%);
}

header {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 10px;
  min-height: 24px;
}
.name {
  font-family: var(--display);
  font-weight: 800;
  font-size: 20px;
  letter-spacing: 0.01em;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.tag {
  flex: none;
  font-family: var(--display);
  font-weight: 800;
  font-size: 12px;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  border-radius: 4px;
  padding: 1px 7px;
  transform: skewX(-8deg);
}
.you {
  background: var(--accent);
  color: var(--accent-ink);
}
.threat {
  border: 1px solid currentColor;
}
.t-Sweat { color: var(--danger); background: color-mix(in srgb, var(--danger) 14%, transparent); }
.t-Skilled { color: var(--rarity-epic); }
.t-Average { color: var(--rarity-rare); }
.t-Casual { color: var(--rarity-uncommon); }
.t-BotLikely { color: var(--rarity-common); }
.mode {
  margin-left: auto;
  flex: none;
  font-size: 11px;
  color: var(--faint);
  white-space: nowrap;
}

.rank-row {
  margin: -4px 0 8px;
}
.stats {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 6px;
}
.tile {
  display: flex;
  flex-direction: column;
  background: var(--surface-2);
  border-radius: 8px;
  padding: 6px 8px;
  min-height: 52px;
  justify-content: center;
}
.tile.rarity {
  background: radial-gradient(120% 120% at 50% 110%, color-mix(in srgb, var(--r) 22%, transparent), var(--surface-2) 70%);
  box-shadow: inset 0 -2px 0 color-mix(in srgb, var(--r) 70%, transparent);
}
.label {
  font-family: var(--display);
  font-weight: 700;
  font-size: 11px;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--muted);
}
.value {
  font-family: var(--display);
  font-weight: 800;
  font-size: 24px;
  line-height: 1.05;
  color: var(--r);
  font-variant-numeric: tabular-nums;
}
.value.plain {
  color: var(--text);
}
.status {
  margin: 0;
  color: var(--muted);
  font-size: 13px;
}
.versus {
  margin: 8px 0 0;
  font-family: var(--display);
  font-weight: 700;
  font-size: 15px;
  letter-spacing: 0.02em;
  color: var(--rarity-uncommon);
}
.versus.worse {
  color: var(--danger);
}
.name-skeleton {
  width: 45%;
  height: 18px;
}
.value-skeleton {
  width: 70%;
  height: 22px;
}
</style>
