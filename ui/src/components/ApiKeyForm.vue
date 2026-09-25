<script setup lang="ts">
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
  <form class="panel" @submit.prevent="submit">
    <label for="api-key">fortnite-api.com API key</label>
    <p class="hint">
      {{ props.hasKey ? 'A key is saved. Paste a new one to replace it.' : 'Get a free key at fortnite-api.com/dashboard.' }}
      It's stored only on this PC.
    </p>
    <div class="row">
      <input id="api-key" v-model="key" type="password" autocomplete="off" placeholder="Paste key" />
      <button type="submit">Save</button>
    </div>
  </form>
</template>
