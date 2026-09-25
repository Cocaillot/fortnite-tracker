<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { relationLabel, type ModeStats, type PlayerProfile } from '../bridge'
import RankBadge from './RankBadge.vue'
import PlayerAvatar from './PlayerAvatar.vue'

const props = defineProps<{ profile: PlayerProfile | null; loadingName: string | null; canGoBack: boolean }>()
const emit = defineEmits<{ close: []; follow: [accountId: string | null, name: string, enabled: boolean] }>()

type Window = 'season' | 'lifetime'
const window_ = ref<Window>('season')
const mode = ref<string>('overall')

// A different player resets the pickers.
watch(
  () => props.profile?.accountId ?? props.profile?.name,
  () => {
    window_.value = 'season'
    mode.value = 'overall'
  },
)

const modeNames: Record<string, string> = { overall: 'All modes', solo: 'Solo', duo: 'Duos', squad: 'Squads', ltm: 'Ranked & LTMs' }

const stats = computed(() => (props.profile ? (window_.value === 'season' ? props.profile.season : props.profile.lifetime) : null))
const modes = computed(() => ['overall', ...Object.keys(stats.value?.byMode ?? {}).filter((m) => m in modeNames)])
const selected = computed<ModeStats | null>(() =>
  !stats.value ? null : mode.value === 'overall' ? stats.value.overall : (stats.value.byMode?.[mode.value] ?? null),
)

// Every ranked mode: played this season first (best rank first), then earlier seasons (most recent
// first); modes never played are listed separately.
const playedRanks = computed(() =>
  (props.profile?.ranks ?? [])
    .filter((r) => r.lastUpdatedUtc)
    .sort((a, b) =>
      a.isCurrentSeason !== b.isCurrentSeason
        ? Number(b.isCurrentSeason) - Number(a.isCurrentSeason)
        : a.isCurrentSeason
          ? b.current + b.progress - (a.current + a.progress)
          : Date.parse(b.lastUpdatedUtc!) - Date.parse(a.lastUpdatedUtc!),
    ),
)
const neverPlayed = computed(() => (props.profile?.ranks ?? []).filter((r) => !r.lastUpdatedUtc))

const statusText: Record<string, string> = {
  Private: 'This player keeps their stats private. They can make them public in Fortnite settings.',
  NotFound: 'No stats found: likely a bot, or a name that changed.',
  NoApiKey: 'Add your API key in Settings to load stats.',
  Error: 'Stats unavailable right now.',
}


// fortnite-api doesn't count placements for Ranked/LTMs, so all-zero tops mean "not tracked", not "never".
const placementsTracked = computed(() => !!selected.value && (selected.value.top25 > 0 || selected.value.matches < 10))
const pct = (part: number, total: number) => (total ? `${((100 * part) / total).toFixed(1)}%` : '–')
const hours = (min: number) => (min >= 60 ? `${Math.round(min / 60)} h` : `${min} min`)
const when = (iso: string) => new Date(iso).toLocaleString('en-GB', { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })
const monthYear = (iso: string) => new Date(iso).toLocaleDateString('en-GB', { month: 'short', year: 'numeric' })
</script>

<template>
  <div class="page">
    <button v-if="canGoBack" type="button" class="back" @click="emit('close')">‹ Back</button>

    <header class="hero card">
      <PlayerAvatar :name="profile?.name ?? loadingName" :size="92" :you="profile?.relation === 'You'" />
      <div class="identity">
        <h1 class="name">{{ profile?.name ?? loadingName }}</h1>
        <div v-if="profile" class="chips">
          <span class="chip" :class="profile.relation">{{ relationLabel[profile.relation] }}</span>
          <span v-if="profile.season.battlePassLevel" class="chip muted">Battle Pass level {{ profile.season.battlePassLevel }}</span>
          <span v-if="profile.encounters.eliminatedYou" class="encounter">
            Eliminated you {{ profile.encounters.eliminatedYou }}×<template v-if="profile.encounters.lastEliminatedYouUtc">, last on {{ when(profile.encounters.lastEliminatedYouUtc) }}</template>
          </span>
        </div>
        <div v-else class="skeleton" style="width: 220px; height: 22px" />
      </div>
      <button
        v-if="profile && profile.relation !== 'You'"
        type="button"
        :class="profile.followed ? 'following' : 'btn-primary'"
        @click="emit('follow', profile.accountId, profile.name, !profile.followed)"
      >
        {{ profile.followed ? 'Following ✓' : 'Follow' }}
      </button>
    </header>

    <div v-if="!profile" class="columns">
      <div class="skeleton" style="height: 360px" />
      <div class="skeleton" style="height: 360px" />
    </div>

    <div v-else class="columns">
      <section class="card">
        <h2 class="card-title">Ranked modes</h2>
        <div v-if="playedRanks.length" class="table-wrap flat">
          <table class="data">
            <thead>
              <tr>
                <th>Mode</th>
                <th>This season</th>
                <th>Best</th>
                <th>Last played</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="r in playedRanks" :key="r.track" :class="{ past: !r.isCurrentSeason }">
                <td class="mode-name">
                  {{ r.trackName }}
                  <span v-if="!r.trackNameConfirmed" class="codename" :title="`Fortnite codename: ${r.track}`">codename</span>
                </td>
                <td>
                  <RankBadge v-if="r.isCurrentSeason" :rank="r" size="md" />
                  <span v-else class="faint">Not played</span>
                </td>
                <td class="muted">{{ r.highestName }}</td>
                <td class="muted">{{ monthYear(r.lastUpdatedUtc!) }}</td>
              </tr>
            </tbody>
          </table>
        </div>
        <p v-else class="muted">
          {{
            profile.relation === 'Opponent' || profile.relation === 'Followed'
              ? 'Fortnite only shares ranks for you, your party and your friends.'
              : 'No ranked matches found.'
          }}
        </p>
        <p v-if="neverPlayed.length" class="never">Never played: {{ neverPlayed.map((r) => r.trackName).join(', ') }}</p>
      </section>

      <section class="card">
        <div class="stats-head">
          <h2 class="card-title">Stats</h2>
          <div class="seg" role="tablist" aria-label="Period">
            <button type="button" role="tab" :aria-selected="window_ === 'season'" :class="{ on: window_ === 'season' }" @click="window_ = 'season'">Season</button>
            <button type="button" role="tab" :aria-selected="window_ === 'lifetime'" :class="{ on: window_ === 'lifetime' }" @click="window_ = 'lifetime'">Lifetime</button>
          </div>
        </div>

        <p v-if="stats && stats.status !== 'Ok'" class="muted">{{ statusText[stats.status] ?? 'Stats unavailable.' }}</p>
        <template v-else-if="stats">
          <div class="modes" role="tablist" aria-label="Mode">
            <button v-for="m in modes" :key="m" type="button" role="tab" :aria-selected="mode === m" :class="{ on: mode === m }" @click="mode = m">
              {{ modeNames[m] }}
            </button>
          </div>
          <div v-if="selected" class="grid">
            <div class="tile rarity" :class="`r-${selected.kdRarity}`" title="Kills per death">
              <span class="label">K/D</span><span class="value">{{ selected.kd.toFixed(2) }}</span>
            </div>
            <div class="tile rarity" :class="`r-${selected.winRateRarity}`" title="Share of matches won">
              <span class="label">Win rate</span><span class="value">{{ selected.winRate.toFixed(1) }}%</span>
            </div>
            <div class="tile"><span class="label">Wins</span><span class="value plain">{{ selected.wins }}</span></div>
            <div class="tile"><span class="label">Matches</span><span class="value plain">{{ selected.matches }}</span></div>
            <div class="tile"><span class="label">Kills</span><span class="value plain">{{ selected.kills }}</span></div>
            <div class="tile"><span class="label">Kills / match</span><span class="value plain">{{ selected.killsPerMatch.toFixed(2) }}</span></div>
            <div class="tile" title="Share of matches finished in the top 10">
              <span class="label">Top 10</span><span class="value plain">{{ placementsTracked ? pct(selected.top10, selected.matches) : '–' }}</span>
            </div>
            <div class="tile" title="Share of matches finished in the top 25">
              <span class="label">Top 25</span><span class="value plain">{{ placementsTracked ? pct(selected.top25, selected.matches) : '–' }}</span>
            </div>
            <div class="tile"><span class="label">Time played</span><span class="value plain">{{ hours(selected.minutesPlayed) }}</span></div>
          </div>
          <p v-else class="muted">No matches in this mode.</p>
          <p v-if="selected && !placementsTracked" class="note">Top 10 / Top 25 aren't tracked for Ranked and limited-time modes.</p>
        </template>
      </section>
    </div>
  </div>
</template>

<style scoped>
.back {
  align-self: flex-start;
  background: none;
  border: none;
  padding: 0;
  color: var(--accent);
  font-family: var(--display);
  font-weight: 800;
  font-size: 17px;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  margin-bottom: calc(-1 * var(--s3));
}
.hero {
  display: flex;
  align-items: center;
  gap: var(--s5);
}
.identity {
  flex: 1;
  min-width: 0;
}
.name {
  margin: 0;
  font-family: var(--display);
  font-weight: 800;
  font-size: 44px;
  line-height: 1;
  overflow-wrap: anywhere;
}
.chips {
  display: flex;
  align-items: center;
  gap: var(--s2);
  margin-top: var(--s3);
  flex-wrap: wrap;
}
.chip {
  font-family: var(--display);
  font-weight: 800;
  font-size: 14px;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  border-radius: 4px;
  padding: 2px 9px;
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
.encounter {
  color: var(--danger);
  font-size: 14px;
}
.following {
  flex: none;
  font-family: var(--display);
  font-weight: 800;
  font-size: 16px;
  text-transform: uppercase;
  color: var(--muted);
  padding: 8px 16px;
}
.hero .btn-primary {
  flex: none;
  font-size: 17px;
  padding: 9px 22px;
}
.columns {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
  gap: var(--s5);
  align-items: start;
}
@media (max-width: 1180px) {
  .columns {
    grid-template-columns: minmax(0, 1fr);
  }
}
.table-wrap.flat {
  border: none;
  background: none;
  margin: 0 calc(-1 * var(--s4));
}
.table-wrap.flat th {
  background: var(--surface);
}
.mode-name {
  font-family: var(--display);
  font-weight: 800;
  font-size: 17px;
  white-space: nowrap;
}
tr.past .mode-name {
  color: var(--muted);
}
.codename {
  font-family: var(--body);
  font-weight: 400;
  font-size: 11px;
  color: var(--faint);
  border: 1px solid var(--border);
  border-radius: 3px;
  padding: 0 5px;
  margin-left: 4px;
}
.never,
.note {
  margin: var(--s3) 0 0;
  font-size: 13px;
  color: var(--faint);
}
.stats-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: var(--s4);
}
.stats-head .card-title {
  margin: 0;
}
.modes {
  display: flex;
  flex-wrap: wrap;
  gap: var(--s2);
  margin-bottom: var(--s4);
}
.modes button {
  border: 1px solid var(--border);
  background: none;
  border-radius: var(--radius-sm);
  padding: 5px 14px;
  color: var(--muted);
  font-family: var(--display);
  font-weight: 700;
  font-size: 15px;
  letter-spacing: 0.04em;
  text-transform: uppercase;
}
.modes button:hover {
  color: var(--text);
}
.modes button.on {
  border-color: var(--accent);
  color: var(--accent);
}
.grid {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: var(--s3);
}
.tile {
  display: flex;
  flex-direction: column;
  gap: 2px;
  background: var(--surface-2);
  border-radius: var(--radius-sm);
  padding: var(--s3) var(--s4);
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
  font-size: 32px;
  line-height: 1.05;
  color: var(--r);
  font-variant-numeric: tabular-nums;
}
.value.plain {
  color: var(--text);
}
</style>
