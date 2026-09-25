<script setup lang="ts">
import { computed } from 'vue'

// A small bar or line chart for one value per day.
const props = withDefaults(
  defineProps<{
    points: { label: string; value: number | null }[]
    kind?: 'bar' | 'line'
    format?: (v: number) => string
    color?: string
  }>(),
  { kind: 'bar', format: (v: number) => String(Math.round(v * 100) / 100), color: 'var(--accent)' },
)

const W = 560
const H = 180
const PAD = { left: 8, right: 8, top: 22, bottom: 24 }

const known = computed(() => props.points.filter((p) => p.value !== null) as { label: string; value: number }[])
const max = computed(() => Math.max(...known.value.map((p) => p.value), 0) || 1)
const min = computed(() => (props.kind === 'line' ? Math.min(...known.value.map((p) => p.value)) : 0))
const step = computed(() => (W - PAD.left - PAD.right) / Math.max(1, props.points.length))

const x = (i: number) => PAD.left + step.value * (i + 0.5)
const y = (v: number) => {
  const span = max.value - min.value || 1
  const lo = props.kind === 'line' ? min.value - span * 0.15 : 0
  const hi = props.kind === 'line' ? max.value + span * 0.15 : max.value
  return PAD.top + (1 - (v - lo) / (hi - lo)) * (H - PAD.top - PAD.bottom)
}

const line = computed(() =>
  props.points
    .map((p, i) => (p.value === null ? null : `${x(i).toFixed(1)},${y(p.value).toFixed(1)}`))
    .filter(Boolean)
    .map((pt, i) => `${i ? 'L' : 'M'}${pt}`)
    .join(' '),
)
// Only label every few days when there are many, counting back from the latest day so it's always labelled.
const labelEvery = computed(() => Math.ceil(props.points.length / 10))
const labelled = (i: number) => (props.points.length - 1 - i) % labelEvery.value === 0
</script>

<template>
  <svg class="trend" :viewBox="`0 0 ${W} ${H}`" role="img" :style="{ '--c': color }">
    <line :x1="PAD.left" :x2="W - PAD.right" :y1="H - PAD.bottom" :y2="H - PAD.bottom" class="base" />
    <template v-if="kind === 'bar'">
      <g v-for="(p, i) in points" :key="i">
        <rect
          v-if="p.value"
          :x="x(i) - step * 0.32"
          :width="step * 0.64"
          :y="y(p.value)"
          :height="H - PAD.bottom - y(p.value)"
          rx="3"
          class="bar"
        >
          <title>{{ p.label }}: {{ format(p.value) }}</title>
        </rect>
        <text v-if="p.value" :x="x(i)" :y="y(p.value) - 5" class="value">{{ format(p.value) }}</text>
      </g>
    </template>
    <template v-else>
      <path :d="line" class="line" />
      <g v-for="(p, i) in points" :key="i">
        <circle v-if="p.value !== null" :cx="x(i)" :cy="y(p.value)" r="4" class="dot">
          <title>{{ p.label }}: {{ format(p.value) }}</title>
        </circle>
        <text v-if="p.value !== null && labelled(i)" :x="x(i)" :y="y(p.value) - 9" class="value">{{ format(p.value) }}</text>
      </g>
    </template>
    <template v-for="(p, i) in points" :key="`l${i}`">
      <text v-if="labelled(i)" :x="x(i)" :y="H - 6" class="label">{{ p.label }}</text>
    </template>
  </svg>
</template>

<style scoped>
.trend {
  width: 100%;
  height: auto;
  display: block;
}
.base {
  stroke: var(--border);
}
.bar {
  fill: color-mix(in srgb, var(--c) 75%, transparent);
}
.bar:hover {
  fill: var(--c);
}
.line {
  fill: none;
  stroke: var(--c);
  stroke-width: 2.5;
  stroke-linejoin: round;
}
.dot {
  fill: var(--surface);
  stroke: var(--c);
  stroke-width: 2;
}
.value {
  fill: var(--text);
  font-family: var(--display);
  font-weight: 800;
  font-size: 12px;
  text-anchor: middle;
}
.label {
  fill: var(--faint);
  font-size: 11px;
  text-anchor: middle;
}
</style>
