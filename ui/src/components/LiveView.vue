<script setup lang="ts">
import { t, tn } from '../i18n'
import { computed } from 'vue'
import type { LobbySnapshot, MatchRecord, RankProgress } from '../bridge'
import PlayerCard from './PlayerCard.vue'
import GoalsCard from './GoalsCard.vue'
import { showToast } from '../composables/toasts'

const props = defineProps<{
  snapshot: LobbySnapshot | null
  ranks: Record<string, RankProgress[]>
  history: MatchRecord[]
  /** Match timer from the app shell (ticks every second), e.g. "12:04". */
  timer: string | null
}>()
const emit = defineEmits<{ open: [accountId: string | null, name: string | null] }>()
const onOpen = (accountId: string | null, name: string | null) => emit('open', accountId, name)

const you = computed(() => (props.snapshot?.localName ? props.snapshot.squad[0] : null))
const myRanks = computed(() => (you.value?.accountId ? (props.ranks[you.value.accountId] ?? []) : []))
const bucket = computed(() => props.snapshot?.statsBucket ?? null)
const label = computed(() => props.snapshot?.statsLabel)

// Today at a glance, from match history.
const today = computed(() => {
  const day = new Date().toDateString()
  const ms = props.history.filter((m) => new Date(m.startedUtc).toDateString() === day)
  const tracked = ms.filter((m) => m.kills !== null)
  const minutes = ms.reduce((sum, m) => sum + (m.endedUtc ? (Date.parse(m.endedUtc) - Date.parse(m.startedUtc)) / 60_000 : 0), 0)
  return {
    matches: ms.length,
    wins: ms.filter((m) => m.won).length,
    kills: tracked.length ? tracked.reduce((s, m) => s + (m.kills ?? 0), 0) : null,
    time: minutes >= 60 ? `${Math.floor(minutes / 60)} h ${String(Math.round(minutes % 60)).padStart(2, '0')}` : `${Math.round(minutes)} min`,
  }
})

const heroState = computed(() => {
  const s = props.snapshot
  if (!s) return { cls: 'idle', title: t('Waiting for Fortnite'), line: t('Start the game and everything here fills in automatically.') }
  if (!s.gameRunning) return { cls: 'idle', title: t('Fortnite not running'), line: t('Launch Fortnite to start tracking.') }
  const party = s.squad.length > 1 ? t('Party of {n}', { n: s.squad.length }) : t('No party')
  if (!s.inMatch) return { cls: 'lobby', title: t('In the lobby'), line: `${s.mode !== 'Match' ? `${t('{mode} selected', { mode: tn(s.mode) })} · ` : ''}${party}` }
  return { cls: 'live', title: tn(s.mode), line: t('In a match · {party} · stats shown: {label}', { party, label: tn(s.statsLabel) }) }
})

const subtitle = computed(() => {
  const s = props.snapshot
  if (!s) return t('Waiting for Fortnite. Your squad appears here as soon as the game starts.')
  if (!s.gameRunning) return t('Fortnite is not running. Launch it and your squad shows up automatically.')
  return s.inMatch
    ? t('In a {mode} match. Stats shown are for {label}.', { mode: tn(s.mode), label: tn(s.statsLabel) })
    : t('In the lobby. After a match, the player who eliminated you appears on the right.')
})
</script>

<template>
  <div class="page">
    <section class="hero" :class="heroState.cls">
      <div class="hero-main">
        <span class="hero-kicker"><span class="dot" aria-hidden="true" />{{ t('Live') }}</span>
        <h1 class="hero-title">{{ heroState.title }}</h1>
        <p class="hero-line">{{ heroState.line }}</p>
      </div>
      <div v-if="heroState.cls === 'live' && timer" class="hero-timer" :aria-label="t('Match time')">{{ timer }}</div>
      <dl class="hero-stats" :aria-label="t('Today')">
        <div><dt>{{ t('Matches today') }}</dt><dd>{{ today.matches }}</dd></div>
        <div><dt>{{ t('Wins') }}</dt><dd :class="{ gold: today.wins }">{{ today.wins }}</dd></div>
        <div :title="today.kills === null ? t('Kills are measured for matches played while the app is open') : undefined"><dt>{{ t('Kills') }}</dt><dd>{{ today.kills ?? '–' }}</dd></div>
        <div><dt>{{ t('Time played') }}</dt><dd>{{ today.time }}</dd></div>
      </dl>
    </section>
    <p class="subtitle">{{ subtitle }}</p>

    <div class="columns">
      <section class="squad">
        <h2 class="card-title">{{ t('Your squad') }}</h2>
        <TransitionGroup v-if="snapshot?.squad.length" name="list" tag="div" class="list">
          <PlayerCard
            v-for="(p, i) in snapshot.squad"
            :key="p.accountId ?? i"
            :player="p"
            :bucket="bucket"
            :mode-label="label"
            :is-you="i === 0 && !!snapshot.localName"
            :fallback-name="i === 0 ? snapshot.localName : null"
            :ranks="p.accountId ? ranks[p.accountId] : []"
            @open="onOpen"
          />
        </TransitionGroup>
        <div v-else class="empty-state">
          <p>{{ t('No squad yet.') }}</p>
          <span>{{ t("Start Fortnite; you and your party members are detected from the game's log.") }}</span>
        </div>
      </section>

      <div class="side">
      <section class="elimination">
        <h2 class="card-title">{{ t('Last elimination') }}</h2>
        <Transition name="slide" mode="out-in">
          <div v-if="snapshot?.eliminatedBy" :key="snapshot.lastMatchStartedUtc ?? ''" class="list">
            <PlayerCard :player="snapshot.eliminatedBy" :bucket="bucket" :mode-label="label" :you="you" variant="eliminator" @open="onOpen" />
            <template v-if="snapshot.spectated.length">
              <h3 class="sub-title">{{ t('Also spectated') }}</h3>
              <PlayerCard
                v-for="p in snapshot.spectated"
                :key="p.epicName ?? ''"
                :player="p"
                :bucket="bucket"
                :mode-label="label"
                :you="you"
                variant="opponent"
                @open="onOpen"
              />
            </template>
          </div>
          <div v-else class="empty-state">
            <p>{{ t('Nobody has eliminated you yet.') }}</p>
            <span>{{ t('When your team is eliminated, their stats, threat level and how they compare to you show up here.') }}</span>
          </div>
        </Transition>
      </section>
      <GoalsCard :snapshot="snapshot" :ranks="myRanks" :history="history" @completed="(text) => showToast(t('Goal completed!'), text, true)" />
      </div>
    </div>
  </div>
</template>

<style scoped>
.hero {
  position: relative;
  display: flex;
  align-items: center;
  gap: var(--s6);
  padding: var(--s5) var(--s6);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  overflow: hidden;
  background:
    radial-gradient(600px 220px at 0% 0%, color-mix(in srgb, var(--state) 22%, transparent), transparent 70%),
    linear-gradient(120deg, var(--surface), color-mix(in srgb, var(--surface) 85%, var(--bg)));
  --state: var(--faint);
}
.hero.lobby {
  --state: var(--accent);
}
.hero.live {
  --state: var(--live);
  border-color: color-mix(in srgb, var(--live) 40%, var(--border));
}
.hero-main {
  flex: 1;
  min-width: 0;
}
.hero-kicker {
  display: inline-flex;
  align-items: center;
  gap: var(--s2);
  font-family: var(--display);
  font-weight: 800;
  font-size: 14px;
  letter-spacing: 0.12em;
  text-transform: uppercase;
  color: var(--state);
}
.dot {
  width: 9px;
  height: 9px;
  border-radius: 50%;
  background: var(--state);
}
.hero.live .dot {
  animation: pulse 1.6s ease-out infinite;
}
@keyframes pulse {
  0% { box-shadow: 0 0 0 0 color-mix(in srgb, var(--live) 60%, transparent); }
  100% { box-shadow: 0 0 0 9px transparent; }
}
.hero-title {
  margin: var(--s1) 0 0;
  font-family: var(--display);
  font-weight: 800;
  font-size: 46px;
  line-height: 1;
  text-transform: uppercase;
  letter-spacing: 0.01em;
}
.hero-line {
  margin: var(--s2) 0 0;
  color: var(--muted);
}
.hero-timer {
  font-family: var(--display);
  font-weight: 800;
  font-size: 58px;
  line-height: 1;
  font-variant-numeric: tabular-nums;
  color: var(--live);
  text-shadow: 0 0 24px color-mix(in srgb, var(--live) 45%, transparent);
}
.hero-stats {
  display: grid;
  grid-template-columns: repeat(4, auto);
  gap: var(--s2) var(--s5);
  margin: 0;
  padding-left: var(--s6);
  border-left: 1px solid var(--border);
}
.hero-stats dt {
  font-family: var(--display);
  font-weight: 700;
  font-size: 12px;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--muted);
  white-space: nowrap;
}
.hero-stats dd {
  margin: 0;
  font-family: var(--display);
  font-weight: 800;
  font-size: 30px;
  line-height: 1.1;
  font-variant-numeric: tabular-nums;
}
.hero-stats dd.gold {
  color: var(--rarity-legendary);
}
@media (max-width: 1250px) {
  .hero {
    flex-wrap: wrap;
  }
  .hero-stats {
    padding-left: 0;
    border-left: none;
  }
}
.subtitle {
  margin: calc(-1 * var(--s2)) 0 0;
  color: var(--muted);
}
.columns {
  display: grid;
  grid-template-columns: minmax(0, 1.2fr) minmax(0, 1fr);
  gap: var(--s6);
  align-items: start;
}
@media (max-width: 1180px) {
  .columns {
    grid-template-columns: minmax(0, 1fr);
  }
}
.side {
  display: flex;
  flex-direction: column;
  gap: var(--s6);
}
.list {
  display: flex;
  flex-direction: column;
  gap: var(--s4);
}
.sub-title {
  margin: var(--s2) 0 0;
  font-family: var(--display);
  font-weight: 800;
  font-size: 15px;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--faint);
}
.empty-state {
  border: 1px dashed var(--border);
  border-radius: var(--radius);
  padding: var(--s6) var(--s5);
  text-align: center;
  color: var(--muted);
}
.empty-state p {
  margin: 0 0 var(--s1);
  font-family: var(--display);
  font-weight: 800;
  font-size: 20px;
  color: var(--text);
}
.empty-state span {
  font-size: 14px;
}

/* The eliminator card slides in from the right when you die. */
.slide-enter-active {
  transition: transform 0.45s cubic-bezier(0.2, 0.9, 0.3, 1.2), opacity 0.3s ease;
}
.slide-leave-active {
  transition: opacity 0.15s ease;
}
.slide-enter-from {
  transform: translateX(40px) skewX(-4deg);
  opacity: 0;
}
.slide-leave-to {
  opacity: 0;
}
.list-enter-active {
  transition: transform 0.3s ease, opacity 0.3s ease;
}
.list-enter-from {
  transform: translateY(8px);
  opacity: 0;
}
</style>
