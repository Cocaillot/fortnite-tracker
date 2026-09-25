<script setup lang="ts">
import { computed } from 'vue'
import { statsFor, threatLabel, type PlayerStats, type RankProgress } from '../bridge'
import RankBadge from './RankBadge.vue'
import PlayerAvatar from './PlayerAvatar.vue'

const props = withDefaults(
  defineProps<{
    player: PlayerStats
    bucket: string | null
    modeLabel?: string
    isYou?: boolean
    fallbackName?: string | null
    /** Your own stats, to compare an opponent against ("2.3× your K/D"). */
    you?: PlayerStats | null
    variant?: 'squad' | 'eliminator' | 'opponent'
    /** Every rank Fortnite shared for this player (you, party, friends); current season is shown. */
    ranks?: RankProgress[]
  }>(),
  { variant: 'squad', modeLabel: undefined, fallbackName: null, you: null, ranks: () => [] },
)

const emit = defineEmits<{ open: [accountId: string | null, name: string | null] }>()

const statusText: Partial<Record<PlayerStats['status'], string>> = {
  Private: 'Stats private: they can make them public in Fortnite settings.',
  NotFound: 'No stats found: likely a bot, or a name that changed.',
  NoApiKey: 'Add your API key in Settings to load stats.',
  Error: 'Stats unavailable right now.',
  Hidden: 'Streamer Mode: Fortnite hides their real name.',
}

const name = computed(() =>
  props.player.status === 'Hidden' ? 'Streamer Mode player' : (props.player.epicName ?? props.fallbackName ?? 'Squad member'),
)
const stats = computed(() => (props.player.status === 'Ok' ? statsFor(props.player, props.bucket) : null))
const loading = computed(() => props.player.status === 'Loading')
const seasonRanks = computed(() =>
  props.ranks.filter((r) => r.isCurrentSeason).sort((a, b) => b.current + b.progress - (a.current + a.progress)),
)

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
</script>

<template>
  <article class="player" :class="[variant, stats ? `r-${stats.kdRarity}` : 'r-Common']">
    <header>
      <span v-if="loading" class="skeleton avatar-skeleton" />
      <PlayerAvatar v-else :name="player.status === 'Hidden' ? '?' : name" :size="46" :you="isYou" />
      <span v-if="loading" class="skeleton name-skeleton" />
      <button v-else-if="canOpen" type="button" class="name link" :title="`Open ${name}'s profile`" @click="open">{{ name }}</button>
      <span v-else class="name">{{ name }}</span>
      <span v-if="isYou" class="tag you">You</span>
      <span v-if="player.threat && variant !== 'squad'" class="tag threat" :class="`t-${player.threat}`">
        {{ threatLabel[player.threat] }}
      </span>
      <span v-if="stats && modeLabel" class="mode">{{ modeLabel }} stats</span>
    </header>

    <div v-if="seasonRanks.length" class="ranks" aria-label="Ranks this season">
      <span v-for="r in seasonRanks" :key="r.track" class="rank-chip">
        <span class="rank-mode">{{ r.trackName }}</span>
        <RankBadge :rank="r" size="md" />
      </span>
    </div>

    <div v-if="loading" class="stats">
      <div v-for="i in 6" :key="i" class="tile"><span class="skeleton value-skeleton" /></div>
    </div>

    <div v-else-if="stats" class="stats">
      <div class="tile rarity" :class="`r-${stats.kdRarity}`" title="Kills per death">
        <span class="label">K/D</span><span class="value">{{ stats.kd.toFixed(2) }}</span>
      </div>
      <div class="tile rarity" :class="`r-${stats.winRateRarity}`" title="Share of matches won">
        <span class="label">Win rate</span><span class="value">{{ stats.winRate.toFixed(1) }}%</span>
      </div>
      <div class="tile"><span class="label">Wins</span><span class="value plain">{{ stats.wins }}</span></div>
      <div class="tile"><span class="label">Matches</span><span class="value plain">{{ stats.matches }}</span></div>
      <div class="tile"><span class="label">Kills</span><span class="value plain">{{ stats.kills }}</span></div>
      <div class="tile"><span class="label">Kills / match</span><span class="value plain">{{ stats.killsPerMatch.toFixed(2) }}</span></div>
    </div>

    <p v-else class="status">{{ statusText[player.status] }}</p>

    <p v-if="versus" class="versus" :class="{ worse: versus.worse }">{{ versus.text }}</p>
  </article>
</template>

<style scoped>
.player {
  position: relative;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: var(--s4) var(--s5) var(--s4) calc(var(--s5) + 4px);
  overflow: hidden;
  display: flex;
  flex-direction: column;
  gap: var(--s3);
}
/* Rarity strip on the left edge, from the player's K/D. */
.player::before {
  content: '';
  position: absolute;
  inset: 0 auto 0 0;
  width: 5px;
  background: var(--r);
}
.player {
  transition: transform 0.15s ease, box-shadow 0.15s ease, border-color 0.15s ease;
}
.player:hover {
  transform: translateY(-2px);
  border-color: color-mix(in srgb, var(--r) 45%, var(--border));
  box-shadow: 0 12px 28px -18px color-mix(in srgb, var(--r) 70%, transparent);
}
.player.eliminator {
  border-color: color-mix(in srgb, var(--danger) 45%, var(--border));
  background: linear-gradient(160deg, color-mix(in srgb, var(--danger) 10%, var(--surface)) 0%, var(--surface) 55%);
}
header {
  display: flex;
  align-items: center;
  gap: var(--s3);
  min-height: 46px;
}
.avatar-skeleton {
  width: 46px;
  height: 46px;
  flex: none;
}
.name {
  font-family: var(--display);
  font-weight: 800;
  font-size: 26px;
  line-height: 1.1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  min-width: 0;
}
.name.link {
  background: none;
  border: none;
  padding: 0;
  color: inherit;
  text-align: left;
}
.name.link:hover {
  color: var(--accent);
}
.tag {
  flex: none;
  font-family: var(--display);
  font-weight: 800;
  font-size: 13px;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  border-radius: 4px;
  padding: 1px 8px;
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
  font-size: 12px;
  color: var(--faint);
  white-space: nowrap;
}
.ranks {
  display: flex;
  flex-wrap: wrap;
  gap: var(--s2);
}
.rank-chip {
  display: inline-flex;
  flex-direction: column;
  gap: 2px;
  background: var(--surface-2);
  border-radius: var(--radius-sm);
  padding: 6px 10px;
}
.rank-mode {
  font-size: 11px;
  color: var(--muted);
}
.stats {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(96px, 1fr));
  gap: var(--s2);
}
.tile {
  display: flex;
  flex-direction: column;
  justify-content: center;
  gap: 2px;
  background: var(--surface-2);
  border-radius: var(--radius-sm);
  padding: var(--s2) var(--s3);
  min-height: 62px;
}
.tile.rarity {
  background: radial-gradient(120% 120% at 50% 110%, color-mix(in srgb, var(--r) 22%, transparent), var(--surface-2) 70%);
  box-shadow: inset 0 -2px 0 color-mix(in srgb, var(--r) 70%, transparent);
}
.label {
  font-family: var(--display);
  font-weight: 700;
  font-size: 12px;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--muted);
}
.value {
  font-family: var(--display);
  font-weight: 800;
  font-size: 28px;
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
}
.versus {
  margin: 0;
  font-family: var(--display);
  font-weight: 800;
  font-size: 17px;
  letter-spacing: 0.02em;
  color: var(--rarity-uncommon);
}
.versus.worse {
  color: var(--danger);
}
.name-skeleton {
  width: 40%;
  height: 24px;
}
.value-skeleton {
  width: 70%;
  height: 26px;
}
</style>
