<script setup lang="ts">
import { computed, ref } from 'vue'
import { send, type Settings } from '../bridge'
import { t } from '../i18n'

// Shown as soon as a newer version is found: "Update now" installs it (after the download if
// needed) and restarts the app. "Later" hides it until the next start; it installs then anyway.
const props = defineProps<{ update: Settings['update'] }>()

const dismissed = ref<string | null>(null)
const shown = computed(() => !!props.update.available && dismissed.value !== props.update.available)

const status = computed(() => {
  const u = props.update
  if (u.installing) return u.ready ? t('Installing… the app restarts in a moment.') : t('Downloading… {p}%, then the app restarts.', { p: u.progress })
  if (u.ready) return t('Downloaded and ready to install. It takes a few seconds.')
  return t('Downloading in the background… {p}%', { p: u.progress })
})
</script>

<template>
  <Transition name="drop">
    <div v-if="shown" class="wrap">
    <section class="banner" role="status">
      <svg class="icon" viewBox="0 0 24 24" aria-hidden="true">
        <path d="M12 3v12M7 10l5 5 5-5M5 20h14" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" />
      </svg>
      <div class="text">
        <strong>{{ t('Version {v} is available', { v: update.available! }) }}</strong>
        <span>{{ status }}</span>
        <span v-if="!update.ready" class="bar" aria-hidden="true"><span :style="{ width: `${update.progress}%` }" /></span>
      </div>
      <button type="button" class="btn-primary" :disabled="update.installing" @click="send({ type: 'applyUpdate' })">
        {{ update.installing ? t('Updating…') : t('Update now') }}
      </button>
      <button v-if="!update.installing" type="button" class="later" @click="dismissed = update.available">{{ t('Later') }}</button>
    </section>
    </div>
  </Transition>
</template>

<style scoped>
/* Same width and spacing as the pages below it. */
.wrap {
  max-width: 1440px;
  margin: 0 auto;
  padding: var(--s5) var(--s6) 0;
}
.banner {
  display: flex;
  align-items: center;
  gap: var(--s4);
  padding: var(--s4) var(--s5);
  border-radius: var(--radius);
  background: color-mix(in srgb, var(--accent) 14%, var(--surface));
  border: 1px solid color-mix(in srgb, var(--accent) 45%, transparent);
}
.icon {
  flex: none;
  width: 28px;
  height: 28px;
  color: var(--accent);
}
.text {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 3px;
  min-width: 0;
}
.text strong {
  font-family: var(--display);
  font-size: 19px;
  text-transform: uppercase;
  letter-spacing: 0.02em;
}
.text span {
  color: var(--muted);
  font-size: 14px;
}
.bar {
  display: block;
  height: 4px;
  max-width: 320px;
  border-radius: 2px;
  background: var(--surface-2);
  overflow: hidden;
}
.bar span {
  display: block;
  height: 100%;
  background: var(--accent);
  transition: width 0.3s ease;
}
.later {
  background: none;
  border: none;
  padding: 0;
  color: var(--muted);
  font-size: 14px;
}
.later:hover {
  color: var(--text);
}
.drop-enter-active,
.drop-leave-active {
  transition: opacity 0.2s ease, transform 0.2s ease;
}
.drop-enter-from,
.drop-leave-to {
  opacity: 0;
  transform: translateY(-8px);
}
</style>
