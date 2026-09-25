<script setup lang="ts">
import type { Settings } from '../bridge'
import ApiKeyForm from './ApiKeyForm.vue'

defineProps<{ settings: Settings }>()
const emit = defineEmits<{ saveKey: [key: string]; richPresence: [enabled: boolean] }>()
</script>

<template>
  <section class="settings">
    <ApiKeyForm :has-key="settings.hasApiKey" @save="emit('saveKey', $event)" />

    <div class="panel">
      <label class="toggle">
        <input
          type="checkbox"
          :checked="settings.richPresence.available && settings.richPresence.enabled"
          :disabled="!settings.richPresence.available"
          @change="emit('richPresence', ($event.target as HTMLInputElement).checked)"
        />
        <span>Show my mode and stats on my Discord profile</span>
      </label>
      <p class="hint">
        <template v-if="settings.richPresence.available">
          Shows mode, squad size and your K/D while Fortnite is running. Discord must be open on this PC.
        </template>
        <template v-else>Not set up in this build: add a Discord application ID as Discord:ClientId in appsettings.json.</template>
      </p>
    </div>

    <p class="about">Fortnite Tracker {{ settings.version }}. Not affiliated with Epic Games.</p>
  </section>
</template>

<style scoped>
.settings {
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.toggle {
  display: flex;
  gap: 10px;
  align-items: center;
  font-weight: 600;
  cursor: pointer;
}
.toggle input {
  flex: none;
  width: 16px;
  height: 16px;
  accent-color: var(--accent);
}
.hint {
  margin: 6px 0 0 26px;
}
.about {
  color: var(--muted);
  font-size: 12px;
  margin: 0;
}
</style>
