<script setup lang="ts">
import { computed, onMounted, onUnmounted } from 'vue'
import type { MatchDetail, MatchRecord } from '../bridge'
import PlayerCard from './PlayerCard.vue'
import PlayerAvatar from './PlayerAvatar.vue'

// One match in detail: result, rank change, who eliminated you (with their stats) and your party.
const props = defineProps<{ match: MatchRecord; detail: MatchDetail | null }>()
const emit = defineEmits<{ close: []; open: [accountId: string | null, name: string | null] }>()

const minutes = computed(() =>
  props.match.endedUtc ? Math.round((Date.parse(props.match.endedUtc) - Date.parse(props.match.startedUtc)) / 60_000) : null,
)
const when = computed(() =>
  new Date(props.match.startedUtc).toLocaleString('en-GB', { weekday: 'long', day: 'numeric', month: 'long', hour: '2-digit', minute: '2-digit' }),
)
const result = computed(() => (props.match.won ? 'Victory' : !props.match.finished ? 'Left early' : 'Eliminated'))

function onKey(e: KeyboardEvent) {
  if (e.key === 'Escape') emit('close')
}
onMounted(() => window.addEventListener('keydown', onKey))
onUnmounted(() => window.removeEventListener('keydown', onKey))
</script>

<template>
  <div class="scrim" @click.self="emit('close')">
    <aside class="drawer" role="dialog" aria-modal="true" :aria-label="`${match.mode} match details`">
      <header>
        <div>
          <span class="kicker">{{ when }}</span>
          <h2>{{ match.mode }}</h2>
        </div>
        <button type="button" class="close" aria-label="Close" @click="emit('close')">×</button>
      </header>

      <div class="facts">
        <div class="fact" :class="{ win: match.won, left: !match.finished }"><span>Result</span><strong>{{ result }}</strong></div>
        <div class="fact"><span>Kills</span><strong>{{ match.kills ?? '–' }}</strong></div>
        <div class="fact"><span>Duration</span><strong>{{ minutes !== null ? `${minutes} min` : '–' }}</strong></div>
        <div class="fact"><span>Party</span><strong>{{ match.squadSize === 1 ? 'Solo' : match.squadSize }}</strong></div>
      </div>

      <div v-if="!detail" class="skeleton" style="height: 220px" />

      <template v-else>
        <section v-if="detail.rank" class="rank-move" :class="{ up: detail.rank.delta > 0, down: detail.rank.delta < 0 }">
          <span class="label">Rank · {{ detail.rank.trackName }}</span>
          <div class="move">
            <span>{{ detail.rank.beforeName }} <small>{{ Math.round(detail.rank.before.progress * 100) }}%</small></span>
            <span class="arrow" aria-hidden="true">→</span>
            <span>{{ detail.rank.afterName }} <small>{{ Math.round(detail.rank.after.progress * 100) }}%</small></span>
            <strong class="delta">{{ detail.rank.delta > 0 ? '+' : '' }}{{ detail.rank.delta }}%</strong>
          </div>
        </section>

        <section v-if="detail.eliminator">
          <h3 class="card-title">Eliminated by</h3>
          <PlayerCard :player="detail.eliminator" :bucket="null" mode-label="All modes" variant="eliminator" @open="(id, n) => emit('open', id, n)" />
        </section>

        <section v-if="detail.party.length">
          <h3 class="card-title">Your party</h3>
          <ul class="party">
            <li v-for="t in detail.party" :key="t.accountId">
              <button type="button" @click="emit('open', t.accountId, null)">
                <PlayerAvatar :name="t.name" :size="36" />
                <span class="mate-name">{{ t.name ?? 'Private profile' }}</span>
                <span v-if="t.stats.overall" class="stat-value" :class="`r-${t.stats.overall.kdRarity}`">{{ t.stats.overall.kd.toFixed(2) }} <small>K/D</small></span>
              </button>
            </li>
          </ul>
        </section>

        <p v-if="!detail.rank && !detail.eliminator && !detail.party.length" class="muted">
          No more details for this match: it was played solo, without a rank update or an eliminator in the log.
        </p>
      </template>
    </aside>
  </div>
</template>

<style scoped>
.scrim {
  position: fixed;
  inset: 52px 0 0 0;
  z-index: 5;
  background: color-mix(in srgb, #000 45%, transparent);
  display: flex;
  justify-content: flex-end;
}
.drawer {
  width: min(520px, 100%);
  height: 100%;
  overflow-y: auto;
  background: var(--bg);
  border-left: 1px solid var(--border);
  padding: var(--s5);
  display: flex;
  flex-direction: column;
  gap: var(--s5);
  animation: slide-in 0.25s ease;
}
@keyframes slide-in {
  from { transform: translateX(40px); opacity: 0; }
}
header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
}
.kicker {
  font-size: 13px;
  color: var(--muted);
}
h2 {
  margin: var(--s1) 0 0;
  font-family: var(--display);
  font-weight: 800;
  font-size: 34px;
  line-height: 1;
  text-transform: uppercase;
}
.close {
  background: none;
  border: none;
  font-size: 28px;
  line-height: 1;
  color: var(--faint);
}
.close:hover {
  color: var(--text);
}
.facts {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: var(--s2);
}
.fact {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius-sm);
  padding: var(--s2) var(--s3);
  display: flex;
  flex-direction: column;
}
.fact span,
.label {
  font-family: var(--display);
  font-weight: 700;
  font-size: 12px;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--muted);
}
.fact strong {
  font-family: var(--display);
  font-weight: 800;
  font-size: 22px;
}
.fact.win strong {
  color: var(--rarity-legendary);
}
.fact.left strong {
  color: var(--muted);
}
.rank-move {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: var(--s4);
  --d: var(--muted);
}
.rank-move.up {
  --d: var(--live);
  border-color: color-mix(in srgb, var(--live) 40%, var(--border));
}
.rank-move.down {
  --d: var(--danger);
  border-color: color-mix(in srgb, var(--danger) 40%, var(--border));
}
.move {
  display: flex;
  align-items: baseline;
  gap: var(--s3);
  margin-top: var(--s2);
  font-family: var(--display);
  font-weight: 800;
  font-size: 22px;
}
.move small {
  font-size: 14px;
  color: var(--muted);
}
.arrow {
  color: var(--faint);
}
.delta {
  margin-left: auto;
  color: var(--d);
  font-size: 26px;
}
.party {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: var(--s2);
}
.party button {
  width: 100%;
  display: flex;
  align-items: center;
  gap: var(--s3);
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius-sm);
  padding: var(--s2) var(--s3);
  text-align: left;
}
.party button:hover {
  border-color: var(--accent);
}
.mate-name {
  flex: 1;
  font-family: var(--display);
  font-weight: 800;
  font-size: 18px;
}
.stat-value small {
  font-size: 12px;
  color: var(--muted);
}
</style>
