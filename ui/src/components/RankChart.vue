<script setup lang="ts">
import { t, tn, locale } from '../i18n'
import { computed } from 'vue'
import type { RankPoint } from '../bridge'

// Rank over time for one season: a line through (time, rank + progress) over bands for each tier.
const props = defineProps<{ points: RankPoint[] }>()

const W = 640
const H = 220
const PAD = { left: 76, right: 16, top: 12, bottom: 26 }

const tiers = [
  { name: 'Bronze', from: 0, to: 3 },
  { name: 'Silver', from: 3, to: 6 },
  { name: 'Gold', from: 6, to: 9 },
  { name: 'Platinum', from: 9, to: 12 },
  { name: 'Diamond', from: 12, to: 15 },
  { name: 'Elite', from: 15, to: 16 },
  { name: 'Champion', from: 16, to: 17 },
  { name: 'Unreal', from: 17, to: 18 },
]

const values = computed(() => props.points.map((p) => ({ t: Date.parse(p.at), v: Math.min(18, p.current + p.progress) })))

// Show the tiers the line passes through, plus one on each side for context.
const range = computed(() => {
  const vs = values.value.map((p) => p.v)
  const lo = Math.max(0, Math.floor(Math.min(...vs)) - 1)
  const hi = Math.min(18, Math.ceil(Math.max(...vs)) + 1)
  return { lo, hi: Math.max(hi, lo + 2) }
})

const x = (t: number) => {
  const ts = values.value.map((p) => p.t)
  const min = Math.min(...ts)
  const max = Math.max(...ts)
  return PAD.left + (max === min ? 0.5 : (t - min) / (max - min)) * (W - PAD.left - PAD.right)
}
const y = (v: number) => PAD.top + (1 - (v - range.value.lo) / (range.value.hi - range.value.lo)) * (H - PAD.top - PAD.bottom)

const bands = computed(() =>
  tiers
    .filter((t) => t.to > range.value.lo && t.from < range.value.hi)
    .map((t) => {
      const top = y(Math.min(t.to, range.value.hi))
      const bottom = y(Math.max(t.from, range.value.lo))
      return { ...t, top, height: bottom - top }
    }),
)

const path = computed(() => values.value.map((p, i) => `${i ? 'L' : 'M'}${x(p.t).toFixed(1)},${y(p.v).toFixed(1)}`).join(' '))
const area = computed(() => {
  const v = values.value
  if (!v.length) return ''
  return `${path.value} L${x(v[v.length - 1]!.t).toFixed(1)},${H - PAD.bottom} L${x(v[0]!.t).toFixed(1)},${H - PAD.bottom} Z`
})

const fmt = (t: number) => new Date(t).toLocaleString(locale(), { day: 'numeric', month: 'short', hour: '2-digit', minute: '2-digit' })
const last = computed(() => values.value[values.value.length - 1])
</script>

<template>
  <svg class="chart" :viewBox="`0 0 ${W} ${H}`" role="img" :aria-label="t('Rank over time, {n} updates', { n: points.length })">
    <g v-for="b in bands" :key="b.name" :class="`tier-${b.name}`">
      <rect :x="PAD.left" :y="b.top" :width="W - PAD.left - PAD.right" :height="b.height" class="band" />
      <text :x="PAD.left - 8" :y="b.top + b.height / 2" class="band-label">{{ tn(b.name) }}</text>
    </g>
    <path :d="area" class="area" />
    <path :d="path" class="line" />
    <circle v-for="(p, i) in values" :key="i" :cx="x(p.t)" :cy="y(p.v)" r="3" class="dot">
      <title>{{ fmt(p.t) }}</title>
    </circle>
    <circle v-if="last" :cx="x(last.t)" :cy="y(last.v)" r="6" class="dot now" />
    <text :x="PAD.left" :y="H - 6" class="axis">{{ values.length ? fmt(values[0]!.t) : '' }}</text>
    <text :x="W - PAD.right" :y="H - 6" class="axis end">{{ last ? fmt(last.t) : '' }}</text>
  </svg>
</template>

<style scoped>
.chart {
  width: 100%;
  height: auto;
  display: block;
}
.band {
  fill: color-mix(in srgb, var(--t) 9%, transparent);
  stroke: color-mix(in srgb, var(--t) 22%, transparent);
  stroke-width: 1;
}
.band-label {
  fill: var(--t);
  font-family: var(--display);
  font-weight: 800;
  font-size: 13px;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  text-anchor: end;
  dominant-baseline: middle;
}
.area {
  fill: color-mix(in srgb, var(--accent) 14%, transparent);
}
.line {
  fill: none;
  stroke: var(--accent);
  stroke-width: 2.5;
  stroke-linejoin: round;
  stroke-linecap: round;
}
.dot {
  fill: var(--surface);
  stroke: var(--accent);
  stroke-width: 2;
}
.dot.now {
  fill: var(--accent);
  stroke: color-mix(in srgb, var(--accent) 40%, transparent);
  stroke-width: 5;
}
.axis {
  fill: var(--faint);
  font-size: 11px;
}
.axis.end {
  text-anchor: end;
}
</style>
