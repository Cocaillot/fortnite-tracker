<script setup lang="ts">
import { computed, useId } from 'vue'

// One emblem per rank tier, drawn in the tier colour (--tier-* in styles.css):
// shields for Bronze/Silver/Gold, gems for Platinum/Diamond, a star for Elite, a crown for
// Champion and a burst for Unreal. Divisions (I, II, III) show as pips under the shields.
const props = withDefaults(defineProps<{ tier: string; division?: number; size?: number }>(), { division: 0, size: 18 })

const id = useId()
const shape = computed(() => {
  switch (props.tier) {
    case 'Bronze':
    case 'Silver':
    case 'Gold':
      return 'shield'
    case 'Platinum':
      return 'crest'
    case 'Diamond':
      return 'gem'
    case 'Elite':
      return 'star'
    case 'Champion':
      return 'crown'
    case 'Unreal':
    case 'Beyond':
      return 'burst'
    default:
      return 'none'
  }
})
</script>

<template>
  <svg class="emblem" :class="`tier-${tier}`" :width="size" :height="size" viewBox="0 0 32 32" aria-hidden="true">
    <defs>
      <linearGradient :id="`${id}-g`" x1="0" y1="0" x2="0" y2="1">
        <stop offset="0" stop-color="#fff" stop-opacity="0.55" />
        <stop offset="0.45" stop-color="#fff" stop-opacity="0" />
      </linearGradient>
    </defs>
    <g v-if="shape === 'shield'">
      <path d="M16 2l12 4v9c0 7.5-5.2 12.6-12 15C9.2 27.6 4 22.5 4 15V6z" class="fill" />
      <path d="M16 2l12 4v9c0 7.5-5.2 12.6-12 15C9.2 27.6 4 22.5 4 15V6z" :fill="`url(#${id}-g)`" />
      <path d="M10 12l6 4 6-4M10 17l6 4 6-4" class="cut" />
    </g>
    <g v-else-if="shape === 'crest'">
      <path d="M16 2l11 6v10c0 6-4.8 10.5-11 12C9.8 28.5 5 24 5 18V8z" class="fill" />
      <path d="M16 2l11 6v10c0 6-4.8 10.5-11 12C9.8 28.5 5 24 5 18V8z" :fill="`url(#${id}-g)`" />
      <path d="M16 8l5 5-5 9-5-9z" class="cut solid" />
    </g>
    <g v-else-if="shape === 'gem'">
      <path d="M9 4h14l7 8-14 17L2 12z" class="fill" />
      <path d="M9 4h14l7 8-14 17L2 12z" :fill="`url(#${id}-g)`" />
      <path d="M2 12h28M9 4l3 8 4 17 4-17 3-8M12 12l4-8 4 8" class="cut" />
    </g>
    <g v-else-if="shape === 'star'">
      <path d="M16 2l4.2 9 9.8 1.2-7.2 6.8 1.9 9.8L16 24l-8.7 4.8 1.9-9.8L2 12.2 11.8 11z" class="fill" />
      <path d="M16 2l4.2 9 9.8 1.2-7.2 6.8 1.9 9.8L16 24l-8.7 4.8 1.9-9.8L2 12.2 11.8 11z" :fill="`url(#${id}-g)`" />
    </g>
    <g v-else-if="shape === 'crown'">
      <path d="M3 10l7 6 6-10 6 10 7-6-3 16H6z" class="fill" />
      <path d="M3 10l7 6 6-10 6 10 7-6-3 16H6z" :fill="`url(#${id}-g)`" />
      <path d="M7 22h18" class="cut" />
    </g>
    <g v-else-if="shape === 'burst'" class="glow">
      <path d="M16 1l3.5 11.5L31 16l-11.5 3.5L16 31l-3.5-11.5L1 16l11.5-3.5z" class="fill" />
      <path d="M16 1l3.5 11.5L31 16l-11.5 3.5L16 31l-3.5-11.5L1 16l11.5-3.5z" :fill="`url(#${id}-g)`" />
      <circle cx="16" cy="16" r="3.2" class="core" />
    </g>
    <path v-else d="M16 3l11 6v14l-11 6-11-6V9z" class="empty" />
    <g v-if="shape === 'shield' && division > 0" class="pips">
      <circle v-for="i in division" :key="i" :cx="16 + (i - (division + 1) / 2) * 5" cy="30.4" r="1.4" />
    </g>
  </svg>
</template>

<style scoped>
.emblem {
  flex: none;
  color: var(--t);
  overflow: visible;
  filter: drop-shadow(0 1px 3px color-mix(in srgb, var(--t) 35%, transparent));
}
.fill {
  fill: currentColor;
}
.cut {
  fill: none;
  stroke: color-mix(in srgb, #000 45%, currentColor);
  stroke-width: 2;
  stroke-linecap: round;
  stroke-linejoin: round;
}
.cut.solid {
  fill: color-mix(in srgb, #000 35%, currentColor);
  stroke: none;
}
.core {
  fill: #fff;
}
.glow {
  filter: drop-shadow(0 0 5px color-mix(in srgb, currentColor 80%, transparent));
}
.empty {
  fill: none;
  stroke: currentColor;
  stroke-width: 2;
  stroke-dasharray: 3 3;
}
.pips {
  fill: currentColor;
}
</style>
