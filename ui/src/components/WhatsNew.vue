<script setup lang="ts">
import { send } from '../bridge'
import type { Release } from '../changelog'
import { t } from '../i18n'

// Shown once after an update.
defineProps<{ releases: Release[] }>()
const close = () => send({ type: 'seenVersion' })
</script>

<template>
  <div class="scrim" role="dialog" aria-modal="true" :aria-label="t('What\'s new')" @click.self="close">
    <div class="box card">
      <span class="kicker">{{ t('What\'s new') }}</span>
      <template v-for="r in releases" :key="r.version">
        <h2>{{ t('Version {v}', { v: r.version }) }}</h2>
        <ul>
          <li v-for="item in r.items" :key="item.title">
            <strong>{{ t(item.title) }}</strong>
            <span>{{ t(item.text) }}</span>
          </li>
        </ul>
      </template>
      <button type="button" class="btn-primary" @click="close">{{ t('Got it') }}</button>
    </div>
  </div>
</template>

<style scoped>
.scrim {
  position: fixed;
  inset: 0;
  z-index: 50;
  display: grid;
  place-items: center;
  padding: var(--s5);
  background: color-mix(in srgb, var(--bg) 70%, transparent);
  backdrop-filter: blur(6px);
}
.box {
  width: min(560px, 100%);
  max-height: 85vh;
  overflow: auto;
  padding: var(--s6);
  display: flex;
  flex-direction: column;
  gap: var(--s3);
  box-shadow: 0 30px 80px rgb(0 0 0 / 0.45);
}
.kicker {
  color: var(--accent);
  font-family: var(--display);
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  font-size: 13px;
}
h2 {
  margin: 0;
  font-family: var(--display);
  font-size: 30px;
  text-transform: uppercase;
}
ul {
  margin: 0 0 var(--s3);
  padding: 0;
  list-style: none;
  display: flex;
  flex-direction: column;
  gap: var(--s3);
}
li {
  display: flex;
  flex-direction: column;
  gap: 2px;
  padding-left: var(--s3);
  border-left: 3px solid var(--accent);
}
li strong {
  font-family: var(--display);
  font-size: 18px;
}
li span {
  color: var(--muted);
  line-height: 1.45;
}
.btn-primary {
  align-self: flex-end;
}
</style>
