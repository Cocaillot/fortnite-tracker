<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { relationLabel, type ModeStats, type PlayerProfile } from '../bridge'
import RankBadge from './RankBadge.vue'

const props = defineProps<{ profile: PlayerProfile | null; loadingName: string | null }>()
const emit = defineEmits<{ close: []; follow: [accountId: string | null, name: string, enabled: boolean] }>()

type Window = 'season' | 'lifetime'
const window_ = ref<Window>('season')
const mode = ref<string>('overall')

// A different player resets the pickers.
watch(() => props.profile?.accountId ?? props.profile?.name, () => {
  window_.value = 'season'
  mode.value = 'overall'
})

const modeNames: Record<string, string> = { overall: 'All modes', solo: 'Solo', duo: 'Duos', squad: 'Squads', ltm: 'Ranked & LTMs' }

const stats = computed(() => (props.profile ? (window_.value === 'season' ? props.profile.season : props.profile.lifetime) : null))
const modes = computed(() => ['overall', ...Object.keys(stats.value?.byMode ?? {}).filter((m) => m in modeNames)])
const selected = computed<ModeStats | null>(() =>
  !stats.value ? null : mode.value === 'overall' ? stats.value.overall : (stats.value.byMode?.[mode.value] ?? null),
)

const currentRanks = computed(() => props.profile?.ranks.filter((r) => r.isCurrentSeason) ?? [])
const pastRanks = computed(() => props.profile?.ranks.filter((r) => r.lastUpdatedUtc && !r.isCurrentSeason) ?? [])

const statusText: Record<string, string> = {
  Private: 'This player keeps their stats private. They can make them public in Fortnite settings.',
  NotFound: 'No stats found: likely a bot, or a name that changed.',
  NoApiKey: 'Add your API key in Settings to load stats.',
  Error: 'Stats unavailable right now.',
}

const hours = (min: number) => (min >= 60 ? `${Math.round(min / 60)}h` : `${min}m`)
const pct = (part: number, total: number) => (total ? `${((100 * part) / total).toFixed(1)}%` : '–')
// fortnite-api doesn't count placements for Ranked/LTMs, so all-zero tops mean "not tracked", not "never".
const placementsTracked = computed(() => !!selected.value && (selected.value.top25 > 0 || selected.value.matches < 10))
const when = (iso: string) => new Date(iso).toLocaleString('en-GB', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })
</script>

<template>
  <div class="profile" role="dialog" aria-modal="true" :aria-label="profile?.name ?? loadingName ?? 'Player profile'">
    <div class="top">
      <button type="button" class="back" @click="emit('close')">‹ Back</button>
    </div>

    <template v-if="!profile">
      <h2 class="name">{{ loadingName }}</h2>
      <div class="skeleton" style="height: 60px; margin-bottom: 10px" />
      <div class="skeleton" style="height: 160px" />
    </template>

    <template v-else>
      <header>
        <div class="title">
          <h2 class="name">{{ profile.name }}</h2>
          <div class="chips">
            <span class="chip" :class="profile.relation">{{ relationLabel[profile.relation] }}</span>
            <span v-if="profile.season.battlePassLevel" class="chip muted">Battle Pass {{ profile.season.battlePassLevel }}</span>
          </div>
        </div>
        <button
          v-if="profile.relation !== 'You'"
          type="button"
          :class="profile.followed ? 'following' : 'btn-primary'"
          @click="emit('follow', profile.accountId, profile.name, !profile.followed)"
        >
          {{ profile.followed ? 'Following ✓' : 'Follow' }}
        </button>
      </header>

      <p v-if="profile.encounters.eliminatedYou" class="encounter">
        Eliminated you <strong>{{ profile.encounters.eliminatedYou }}×</strong>
        <template v-if="profile.encounters.lastEliminatedYouUtc"> · last {{ when(profile.encounters.lastEliminatedYouUtc) }}</template>
      </p>

      <section>
        <h3 class="section-title">Ranks</h3>
        <div v-if="currentRanks.length" class="ranks">
          <div v-for="r in currentRanks" :key="r.track" class="rank-tile">
            <span class="track">{{ r.trackName }}</span>
            <RankBadge :rank="r" size="md" />
            <span class="best">Best {{ r.highestName }}</span>
          </div>
        </div>
        <p v-else class="hint">
          {{
            profile.relation === 'Opponent' || profile.relation === 'Followed'
              ? "Fortnite only shares ranks for you, your party and your friends."
              : 'No ranked matches this season.'
          }}
        </p>
        <details v-if="pastRanks.length" class="past">
          <summary>Past seasons ({{ pastRanks.length }})</summary>
          <ul>
            <li v-for="r in pastRanks" :key="r.track">
              <span>{{ r.trackName }}</span>
              <RankBadge :rank="r" />
              <span class="faint">best {{ r.highestName }} · {{ new Date(r.lastUpdatedUtc!).toLocaleDateString('en-GB', { month: 'short', year: 'numeric' }) }}</span>
            </li>
          </ul>
        </details>
      </section>

      <section>
        <div class="stats-head">
          <h3 class="section-title">Stats</h3>
          <div class="seg" role="tablist" aria-label="Period">
            <button type="button" role="tab" :aria-selected="window_ === 'season'" :class="{ on: window_ === 'season' }" @click="window_ = 'season'">Season</button>
            <button type="button" role="tab" :aria-selected="window_ === 'lifetime'" :class="{ on: window_ === 'lifetime' }" @click="window_ = 'lifetime'">Lifetime</button>
          </div>
        </div>

        <p v-if="stats && stats.status !== 'Ok'" class="hint">{{ statusText[stats.status] ?? 'Stats unavailable.' }}</p>
        <template v-else-if="stats">
          <div class="modes">
            <button v-for="m in modes" :key="m" type="button" :class="{ on: mode === m }" @click="mode = m">{{ modeNames[m] }}</button>
          </div>
          <div v-if="selected" class="grid">
            <div class="tile rarity" :class="`r-${selected.kdRarity}`"><span class="label">K/D</span><span class="value">{{ selected.kd.toFixed(2) }}</span></div>
            <div class="tile rarity" :class="`r-${selected.winRateRarity}`"><span class="label">Win %</span><span class="value">{{ selected.winRate.toFixed(1) }}</span></div>
            <div class="tile"><span class="label">Wins</span><span class="value plain">{{ selected.wins }}</span></div>
            <div class="tile"><span class="label">Matches</span><span class="value plain">{{ selected.matches }}</span></div>
            <div class="tile"><span class="label">Kills</span><span class="value plain">{{ selected.kills }}</span></div>
            <div class="tile"><span class="label">Kills / match</span><span class="value plain">{{ selected.killsPerMatch.toFixed(2) }}</span></div>
            <div class="tile"><span class="label">Top 10</span><span class="value plain">{{ placementsTracked ? pct(selected.top10, selected.matches) : '–' }}</span></div>
            <div class="tile"><span class="label">Top 25</span><span class="value plain">{{ placementsTracked ? pct(selected.top25, selected.matches) : '–' }}</span></div>
            <div class="tile"><span class="label">Time played</span><span class="value plain">{{ hours(selected.minutesPlayed) }}</span></div>
          </div>
          <p v-else class="hint">No matches in this mode.</p>
        </template>
      </section>
    </template>
  </div>
</template>

<style scoped>
.profile {
  display: flex;
  flex-direction: column;
  gap: 16px;
}
.top {
  margin-bottom: -6px;
}
.back {
  background: none;
  border: none;
  padding: 2px 0;
  color: var(--accent);
  font-family: var(--display);
  font-weight: 800;
  font-size: 15px;
  letter-spacing: 0.04em;
  text-transform: uppercase;
}
header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 10px;
}
.title {
  min-width: 0;
}
.name {
  margin: 0;
  font-family: var(--display);
  font-weight: 800;
  font-size: 30px;
  line-height: 1.05;
  overflow-wrap: anywhere;
}
.chips {
  display: flex;
  gap: 6px;
  margin-top: 6px;
  flex-wrap: wrap;
}
.chip {
  font-family: var(--display);
  font-weight: 800;
  font-size: 12px;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  border-radius: 4px;
  padding: 1px 7px;
  transform: skewX(-8deg);
  background: var(--surface-2);
  color: var(--muted);
}
.chip.You,
.chip.Party {
  background: var(--accent);
  color: var(--accent-ink);
}
.chip.Friend {
  background: color-mix(in srgb, var(--live) 20%, transparent);
  color: var(--live);
}
.chip.Followed {
  background: color-mix(in srgb, var(--rarity-epic) 20%, transparent);
  color: var(--rarity-epic);
}
.following {
  flex: none;
  font-family: var(--display);
  font-weight: 800;
  font-size: 15px;
  text-transform: uppercase;
  color: var(--muted);
}
.encounter {
  margin: -6px 0 0;
  color: var(--danger);
  font-size: 13px;
}
.ranks {
  display: grid;
  grid-template-columns: 1fr 1fr;
  gap: 6px;
}
.rank-tile {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 10px;
  padding: 8px 10px;
  display: flex;
  flex-direction: column;
  gap: 4px;
  min-width: 0;
}
.track,
.label {
  font-family: var(--display);
  font-weight: 700;
  font-size: 11px;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--muted);
}
.best,
.faint {
  font-size: 11px;
  color: var(--faint);
}
.past {
  margin-top: 8px;
  font-size: 13px;
  color: var(--muted);
}
.past summary {
  cursor: pointer;
}
.past ul {
  list-style: none;
  padding: 0;
  margin: 8px 0 0;
  display: flex;
  flex-direction: column;
  gap: 6px;
}
.past li {
  display: grid;
  grid-template-columns: 1fr auto;
  gap: 2px 8px;
  align-items: center;
}
.past li .faint {
  grid-column: 1 / -1;
}
.stats-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.stats-head .section-title {
  margin: 0;
}
.seg {
  display: inline-flex;
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 8px;
  padding: 2px;
}
.seg button,
.modes button {
  border: none;
  background: none;
  border-radius: 6px;
  padding: 3px 10px;
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
.modes {
  display: flex;
  flex-wrap: wrap;
  gap: 4px;
  margin: 10px 0 8px;
}
.modes button {
  border: 1px solid var(--border);
}
.modes button.on {
  border-color: var(--accent);
  color: var(--accent);
}
.grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
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
.value {
  font-family: var(--display);
  font-weight: 800;
  font-size: 22px;
  line-height: 1.05;
  color: var(--r);
  font-variant-numeric: tabular-nums;
}
.value.plain {
  color: var(--text);
}
</style>
