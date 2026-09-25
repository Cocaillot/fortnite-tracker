<script setup lang="ts">
import { t } from '../i18n'
import { dismissToast as dismiss, toasts } from '../composables/toasts'
</script>

<template>
  <div class="toasts" aria-live="polite">
    <TransitionGroup name="toast">
      <div v-for="toast in toasts" :key="toast.id" class="toast" :class="{ good: toast.good }" role="status">
        <div class="body">
          <strong>{{ toast.title }}</strong>
          <span>{{ toast.text }}</span>
        </div>
        <button type="button" class="close" :aria-label="t('Dismiss')" @click="dismiss(toast.id)">×</button>
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
