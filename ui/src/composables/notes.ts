import { ref } from 'vue'
import { on, send, type PlayerNote } from '../bridge'

// Your notes on players, shared by every component (cards, profiles, match details).
export const notes = ref<PlayerNote[]>([])

export const noteTags = ['Teamer', 'Cheater?', 'Sweat', 'Good duo', 'Toxic', 'Friendly'] as const

export function findNote(accountId: string | null, name: string | null): PlayerNote | null {
  const lower = name?.toLowerCase()
  return (
    notes.value.find((n) => (accountId && n.accountId === accountId) || (lower && n.name.toLowerCase() === lower)) ?? null
  )
}

export function saveNote(accountId: string | null, name: string, tags: string[], text: string) {
  send({ type: 'setNote', accountId, name, tags, text })
}

export function initNotes() {
  on('notes', (n) => (notes.value = n))
}
