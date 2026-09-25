<script setup lang="ts">
import { ref } from 'vue'
import { keysLabel, t } from '../i18n'

// A global shortcut picker: click, press the keys, done. Letters and digits need a modifier.
const props = defineProps<{ value: string; ok: boolean; label: string; fallback: string }>()
const emit = defineEmits<{ change: [value: string] }>()

const listening = ref(false)
const error = ref<string | null>(null)

function keyName(e: KeyboardEvent): string | null {
  if (/^Key[A-Z]$/.test(e.code)) return e.code.slice(3)
  if (/^Digit[0-9]$/.test(e.code)) return e.code.slice(5)
  if (/^F([1-9]|1[0-2])$/.test(e.code)) return e.code
  return null
}

function onKey(e: KeyboardEvent) {
  if (!listening.value) return
  e.preventDefault()
  e.stopPropagation() // keep F11 from toggling full screen while choosing
  if (e.key === 'Escape') {
    listening.value = false
    return
  }
  if (['Control', 'Shift', 'Alt', 'Meta'].includes(e.key)) return
  const key = keyName(e)
  const mods = [e.ctrlKey && 'Ctrl', e.altKey && 'Alt', e.shiftKey && 'Shift', e.metaKey && 'Win'].filter(Boolean) as string[]
  if (!key) {
    error.value = t('Use a letter, a number or F1–F12.')
    return
  }
  if (!mods.length && !key.startsWith('F')) {
    error.value = t("Add Ctrl, Alt or Shift so it doesn't trigger while you type.")
    return
  }
  error.value = null
  listening.value = false
  const next = [...mods, key].join('+')
  if (next !== props.value) emit('change', next)
}

function toggle() {
  listening.value = !listening.value
  error.value = null
}
</script>

<template>
  <div class="hotkey">
    <button
      type="button"
      class="keys"
      :class="{ listening, bad: !ok && !listening }"
      :aria-label="`${label}: ${keysLabel(value)}`"
      @click="toggle"
      @keydown="onKey"
      @blur="listening = false"
    >
      {{ listening ? t('Press the keys…') : keysLabel(value) }}
    </button>
    <span class="desc">{{ label }}</span>
    <button v-if="value !== fallback" type="button" class="reset" @click="emit('change', fallback)">{{ t('Reset') }}</button>
    <span v-if="error" class="err">{{ error }}</span>
    <span v-else-if="!ok" class="err">{{ t('Another app already uses this shortcut. Pick another one.') }}</span>
  </div>
</template>

<style scoped>
.hotkey {
  display: grid;
  grid-template-columns: 150px 1fr auto;
  align-items: center;
  gap: 6px var(--s3);
}
.keys {
  font-family: var(--display);
  font-weight: 800;
  font-size: 14px;
  letter-spacing: 0.04em;
  padding: 7px 10px;
  border: 1px solid var(--border);
  border-radius: var(--radius-sm, 6px);
  background: var(--surface-2);
  color: var(--text);
  text-align: center;
}
.keys:hover {
  border-color: var(--muted);
}
.keys.listening {
  border-color: var(--accent);
  color: var(--accent);
  animation: blink 1s ease-in-out infinite;
}
.keys.bad {
  border-color: var(--danger);
}
@keyframes blink {
  50% { opacity: 0.6; }
}
.desc {
  color: var(--muted);
  font-size: 14px;
}
.reset {
  background: none;
  border: none;
  padding: 0;
  color: var(--muted);
  font-size: 13px;
  text-decoration: underline;
}
.reset:hover {
  color: var(--accent);
}
.err {
  grid-column: 1 / -1;
  color: var(--danger);
  font-size: 13px;
}
</style>
