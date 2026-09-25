<script setup lang="ts">
import { computed } from 'vue'
import { isAnonymous, type MatchRecord, type SessionRecord } from '../bridge'

const props = defineProps<{ matches: MatchRecord[]; sessions: SessionRecord[] }>()
const emit = defineEmits<{ open: [accountId: string | null, name: string | null] }>()

// Same rule as SessionStore.Gap in C#: a longer break starts a new session.
const GAP_MS = 45 * 60_000

interface Session {
  start: number
  end: number
  matches: MatchRecord[]
  record: SessionRecord | null
}

const groups = computed<Session[]>(() => {
  const sorted = [...props.matches].sort((a, b) => Date.parse(a.startedUtc) - Date.parse(b.startedUtc))
  const out: Session[] = []
  for (const m of sorted) {
    const start = Date.parse(m.startedUtc)
    const end = m.endedUtc ? Date.parse(m.endedUtc) : start
    const last = out[out.length - 1]
    if (last && start - last.end < GAP_MS) {
      last.matches.push(m)
      last.end = Math.max(last.end, end)
    } else {
      out.push({ start, end, matches: [m], record: null })
    }
  }
  // Stats-based session totals (live play only) matched by time.
  for (const g of out)
    g.record =
      props.sessions.find((s) => {
        const t = Date.parse(s.startedUtc)
        return t >= g.start - 2 * 60_000 && t <= g.end
      }) ?? null
  return out.reverse()
})

function mostCommon(items: string[]) {
  const counts = new Map<string, number>()
  for (const i of items) counts.set(i, (counts.get(i) ?? 0) + 1)
  return [...counts.entries()].sort((a, b) => b[1] - a[1])[0] ?? null
}

function summary(g: Session) {
  const d = g.record?.delta && g.record.delta.matches > 0 ? g.record.delta : null
  const tracked = g.matches.filter((m) => m.kills !== null)
  const eliminators = g.matches.map((m) => m.eliminatedBy).filter((n): n is string => !!n && !isAnonymous(n))
  return {
    fromStats: !!d,
    kills: d ? d.kills : tracked.length ? tracked.reduce((s, m) => s + (m.kills ?? 0), 0) : null,
    wins: d ? d.wins : g.matches.filter((m) => m.won).length,
    kd: d ? d.kd : null,
    winRate: d ? d.winRate : null,
    minutes: Math.round((g.end - g.start) / 60_000),
    mode: mostCommon(g.matches.map((m) => m.mode)),
    nemesis: mostCommon(eliminators),
  }
}

const isLive = (g: Session) => Date.now() - g.end < GAP_MS
const day = (t: number) => new Date(t).toLocaleDateString('en-GB', { weekday: 'short', day: 'numeric', month: 'short' })
const time = (t: number) => new Date(t).toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' })
const hours = (min: number) => (min >= 60 ? `${Math.floor(min / 60)}h ${String(min % 60).padStart(2, '0')}` : `${min} min`)
</script>

<template>
  <div class="sessions">
    <p v-if="!groups.length" class="empty">Sessions appear here as you play.</p>
    <article v-for="g in groups" :key="g.start" class="session" :class="{ live: isLive(g) }">
      <template v-for="s in [summary(g)]" :key="0">
        <header>
          <span class="when">{{ day(g.start) }} · {{ time(g.start) }}–{{ time(g.end) }}</span>
          <span v-if="isLive(g)" class="live-tag">Now</span>
          <span class="dur">{{ hours(s.minutes) }}</span>
        </header>
        <div class="tiles">
          <div class="tile"><span class="label">Matches</span><span class="value">{{ g.matches.length }}</span></div>
          <div class="tile"><span class="label">Kills</span><span class="value">{{ s.kills ?? '–' }}</span></div>
          <div class="tile"><span class="label">Wins</span><span class="value" :class="{ gold: s.wins }">{{ s.wins }}</span></div>
          <div class="tile">
            <span class="label">{{ s.kd !== null ? 'K/D' : 'Win %' }}</span>
            <span class="value">{{ s.kd !== null ? s.kd.toFixed(2) : s.winRate !== null ? s.winRate.toFixed(0) : '–' }}</span>
          </div>
        </div>
        <p class="detail">
          <template v-if="s.mode">Mostly {{ s.mode[0] }}</template>
          <template v-if="s.nemesis && s.nemesis[1] >= 2">
            · eliminated {{ s.nemesis[1] }}× by
            <span class="player-link" role="button" tabindex="0" @click="emit('open', null, s.nemesis[0])" @keydown.enter="emit('open', null, s.nemesis[0])">{{ s.nemesis[0] }}</span>
          </template>
        </p>
        <p v-if="!s.fromStats" class="source">Kills and wins from tracked matches only; full session stats need 0.4.0 or later.</p>
      </template>
    </article>
  </div>
</template>

<style scoped>
.sessions {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(380px, 1fr));
  gap: var(--s4);
}
.session {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius);
  padding: var(--s4) var(--s5);
}
.session.live {
  border-color: color-mix(in srgb, var(--live) 45%, var(--border));
}
header {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 8px;
}
.when {
  font-family: var(--display);
  font-weight: 800;
  font-size: 20px;
}
.live-tag {
  font-family: var(--display);
  font-weight: 800;
  font-size: 11px;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--bg);
  background: var(--live);
  border-radius: 3px;
  padding: 0 6px;
  transform: skewX(-8deg);
}
.dur {
  margin-left: auto;
  color: var(--muted);
  font-size: 13px;
}
.tiles {
  display: grid;
  grid-template-columns: repeat(4, 1fr);
  gap: 6px;
}
.tile {
  background: var(--surface-2);
  border-radius: 10px;
  padding: var(--s2) var(--s3);
  display: flex;
  flex-direction: column;
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
  font-size: 28px;
  line-height: 1.1;
  font-variant-numeric: tabular-nums;
}
.value.gold {
  color: var(--rarity-legendary);
}
.detail {
  margin: var(--s3) 0 0;
  font-size: 14px;
  color: var(--muted);
}
.source {
  margin: 4px 0 0;
  font-size: 11px;
  color: var(--faint);
}
</style>
