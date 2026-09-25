<script setup lang="ts">
import { computed } from 'vue'

// A monogram badge whose colours come from the name, so each player keeps the same look everywhere.
const props = withDefaults(defineProps<{ name: string | null; size?: number; you?: boolean }>(), { size: 40, you: false })

const initials = computed(
  () =>
    (props.name ?? '?')
      .replace(/[^\p{L}\p{N} ]/gu, ' ')
      .split(' ')
      .filter(Boolean)
      .slice(0, 2)
      .map((w) => w[0]!.toUpperCase())
      .join('') || '?',
)

const hue = computed(() => {
  let h = 0
  for (const ch of props.name ?? '?') h = (h * 31 + ch.codePointAt(0)!) % 360
  return h
})
</script>

<template>
  <span
    class="avatar"
    :class="{ you }"
    :style="{
      width: `${size}px`,
      height: `${size}px`,
      fontSize: `${Math.round(size * 0.42)}px`,
      background: you ? undefined : `linear-gradient(135deg, hsl(${hue} 70% 55%), hsl(${(hue + 40) % 360} 65% 40%))`,
    }"
    aria-hidden="true"
  >
    {{ initials }}
  </span>
</template>

<style scoped>
.avatar {
  flex: none;
  display: inline-grid;
  place-items: center;
  border-radius: calc(var(--radius) * 0.9);
  font-family: var(--display);
  font-weight: 800;
  letter-spacing: 0.02em;
  color: #fff;
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.35);
  transform: skewX(-4deg);
  box-shadow: inset 0 0 0 1px rgba(255, 255, 255, 0.12);
}
.avatar.you {
  background: linear-gradient(135deg, var(--accent), color-mix(in srgb, var(--accent) 45%, var(--rarity-epic)));
  color: var(--accent-ink);
  text-shadow: none;
}
</style>
