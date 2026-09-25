<script setup lang="ts">
import { onUnmounted, ref } from 'vue'
import { on } from '../bridge'

// In-app pop-ups sent by the host (rank changes, meeting a noted player again…).
interface Toast {
  id: number
  title: string
  text: string
  good: boolean
}

const toasts = ref<Toast[]>([])
let next = 0

function dismiss(id: number) {
  toasts.value = toasts.value.filter((t) => t.id !== id)
}

const off = on('toast', (t) => {
  const id = next++
  toasts.value = [...toasts.value.slice(-3), { id, ...t }]
  window.setTimeout(() => dismiss(id), 7000)
})
onUnmounted(off)
</script>

<template>
  <div class="toasts" aria-live="polite">
    <TransitionGroup name="toast">
      <div v-for="t in toasts" :key="t.id" class="toast" :class="{ good: t.good }" role="status">
        <div class="body">
          <strong>{{ t.title }}</strong>
          <span>{{ t.text }}</span>
        </div>
        <button type="button" class="close" aria-label="Dismiss" @click="dismiss(t.id)">×</button>
      </div>
    </TransitionGroup>
  </div>
</template>

<style scoped>
.toasts {
  position: fixed;
  top: 64px;
  right: var(--s5);
  z-index: 10;
  display: flex;
  flex-direction: column;
  gap: var(--s2);
  width: 340px;
}
.toast {
  display: flex;
  gap: var(--s3);
  align-items: flex-start;
  background: var(--surface);
  border: 1px solid var(--border);
  border-left: 4px solid var(--danger);
  border-radius: var(--radius-sm);
  padding: var(--s3) var(--s4);
  box-shadow: 0 16px 40px -16px rgba(0, 0, 0, 0.6);
}
.toast.good {
  border-left-color: var(--live);
}
.body {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 2px;
  font-size: 14px;
}
.body strong {
  font-family: var(--display);
  font-weight: 800;
  font-size: 18px;
  letter-spacing: 0.02em;
}
.body span {
  color: var(--muted);
}
.close {
  background: none;
  border: none;
  padding: 0 4px;
  font-size: 20px;
  line-height: 1;
  color: var(--faint);
}
.close:hover {
  color: var(--text);
}
.toast-enter-active,
.toast-leave-active {
  transition: transform 0.3s ease, opacity 0.3s ease;
}
.toast-enter-from,
.toast-leave-to {
  transform: translateX(30px);
  opacity: 0;
}
</style>
