<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { findNote, noteTags, notes, saveNote } from '../composables/notes'

// Tags and a private note on a player, with a reminder when you meet them again.
const props = defineProps<{ accountId: string | null; name: string }>()

const existing = computed(() => findNote(props.accountId, props.name))
const tags = ref<string[]>([])
const text = ref('')
const saved = ref(false)

watch(
  [existing, () => props.name],
  () => {
    tags.value = [...(existing.value?.tags ?? [])]
    text.value = existing.value?.text ?? ''
  },
  { immediate: true },
)
// Show "Saved" once the host echoes the change back.
watch(notes, () => {
  if (saved.value) window.setTimeout(() => (saved.value = false), 2000)
})

const dirty = computed(
  () => text.value.trim() !== (existing.value?.text ?? '') || tags.value.join('|') !== (existing.value?.tags ?? []).join('|'),
)

function toggle(tag: string) {
  tags.value = tags.value.includes(tag) ? tags.value.filter((t) => t !== tag) : [...tags.value, tag]
}

function save() {
  saveNote(props.accountId, props.name, tags.value, text.value)
  saved.value = true
}
</script>

<template>
  <section class="card notes">
    <h2 class="card-title">Your notes</h2>
    <div class="tags" role="group" aria-label="Tags">
      <button v-for="t in noteTags" :key="t" type="button" class="tag" :class="{ on: tags.includes(t) }" :aria-pressed="tags.includes(t)" @click="toggle(t)">
        {{ t }}
      </button>
    </div>
    <textarea v-model="text" rows="2" maxlength="300" :placeholder="`Anything to remember about ${name}?`" aria-label="Note" />
    <div class="row">
      <button type="button" class="btn-primary" :disabled="!dirty" @click="save">Save</button>
      <span class="hint-small">Private to this PC. You'll get a reminder when you meet {{ name }} again.</span>
      <span v-if="saved && !dirty" class="ok">Saved ✓</span>
    </div>
  </section>
</template>

<style scoped>
.tags {
  display: flex;
  flex-wrap: wrap;
  gap: var(--s2);
  margin-bottom: var(--s3);
}
.tag {
  border: 1px solid var(--border);
  background: none;
  color: var(--muted);
  border-radius: 999px;
  padding: 4px 14px;
  font-family: var(--display);
  font-weight: 700;
  font-size: 15px;
  letter-spacing: 0.03em;
}
.tag:hover {
  color: var(--text);
}
.tag.on {
  border-color: var(--accent);
  background: color-mix(in srgb, var(--accent) 16%, transparent);
  color: var(--text);
}
textarea {
  width: 100%;
  font: inherit;
  color: var(--text);
  background: var(--surface-2);
  border: 1px solid var(--border);
  border-radius: var(--radius-sm);
  padding: var(--s2) var(--s3);
  resize: vertical;
  user-select: text;
}
.row {
  display: flex;
  align-items: center;
  gap: var(--s3);
  margin-top: var(--s3);
}
.hint-small {
  font-size: 13px;
  color: var(--muted);
}
.ok {
  margin-left: auto;
  color: var(--live);
  font-weight: 600;
}
</style>
