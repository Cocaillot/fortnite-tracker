<script setup lang="ts">
import { t } from '../i18n'
import { ref } from 'vue'

const props = defineProps<{ hasKey: boolean }>()
const emit = defineEmits<{ save: [key: string] }>()

const key = ref('')

function submit() {
  if (!key.value.trim()) return
  emit('save', key.value.trim())
  key.value = ''
}
</script>

<template>
  <form class="panel" :class="{ attention: !props.hasKey }" @submit.prevent="submit">
    <label for="api-key" class="title">{{ t('Stats API key') }}</label>
    <p class="hint">
      {{ props.hasKey ? t('A key is saved. Paste a new one to replace it.') : t('Get a free key at fortnite-api.com/dashboard to load stats.') }}
      {{ t("It's stored only on this PC.") }}
    </p>
    <div class="row">
      <input id="api-key" v-model="key" type="password" autocomplete="off" :placeholder="t('Paste key')" />
      <button type="submit" class="btn-primary">{{ t('Save') }}</button>
    </div>
  </form>
</template>

<style scoped>
.attention {
  border-color: color-mix(in srgb, var(--accent) 50%, var(--border));
}
.title {
  font-family: var(--display);
  font-weight: 800;
  font-size: 17px;
  letter-spacing: 0.02em;
}
</style>
