import { ref } from 'vue'
import { on } from '../bridge'

// In-app pop-ups: from the host (rank changes, meeting a noted player) or the UI (goal completed).
export interface Toast {
  id: number
  title: string
  text: string
  good: boolean
}

export const toasts = ref<Toast[]>([])
let next = 0

export function dismissToast(id: number) {
  toasts.value = toasts.value.filter((t) => t.id !== id)
}

export function showToast(title: string, text: string, good: boolean) {
  const id = next++
  toasts.value = [...toasts.value.slice(-3), { id, title, text, good }]
  window.setTimeout(() => dismissToast(id), 7000)
}

export function initToasts() {
  on('toast', (t) => showToast(t.title, t.text, t.good))
}
