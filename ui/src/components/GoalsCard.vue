<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import type { LobbySnapshot, MatchRecord, RankProgress } from '../bridge'
import { addGoal, describe, markDone, removeGoal, useGoalProgress, type Goal, type Period } from '../composables/goals'
import { RANK_NAMES } from '../ranks'

const props = defineProps<{ snapshot: LobbySnapshot | null; ranks: RankProgress[]; history: MatchRecord[] }>()
const emit = defineEmits<{ completed: [text: string] }>()

const progress = useGoalProgress(() => props.snapshot, () => props.ranks, () => props.history)

// Celebrate each goal once when it reaches 100%.
watch(progress, (list) => {
  for (const p of list)
    if (p.value >= 1 && !p.goal.done) {
      markDone(p.goal.id)
      emit('completed', describe(p.goal))
    }
})

// ---- Add form ----
const adding = ref(false)
const kind = ref<Goal['kind']>('rank')
const period = ref<Period>('today')
const amount = ref(5)
const kd = ref(1.5)
const rankTracks = computed(() => props.ranks.filter((r) => r.isCurrentSeason))
const track = ref<string>('')
const rankTarget = ref(6)

function openForm() {
  adding.value = true
  track.value = rankTracks.value[0]?.track ?? ''
  const r = rankTracks.value[0]
  rankTarget.value = r ? Math.min(17, r.current + 1) : 6
}

function create() {
  const id = crypto.randomUUID()
  if (kind.value === 'rank') {
    const r = rankTracks.value.find((x) => x.track === track.value)
    if (!r) return
    addGoal({ id, kind: 'rank', track: r.track, trackName: r.trackName, target: rankTarget.value })
  } else if (kind.value === 'kd') addGoal({ id, kind: 'kd', target: kd.value })
  else addGoal({ id, kind: kind.value, period: period.value, target: amount.value })
  adding.value = false
}
</script>

<template>
  <section class="goals">
    <div class="head">
      <h2 class="card-title">Goals</h2>
      <button v-if="!adding" type="button" class="add" @click="openForm">+ Add goal</button>
    </div>

    <form v-if="adding" class="form card" @submit.prevent="create">
      <select v-model="kind" aria-label="Goal type">
        <option value="rank" :disabled="!rankTracks.length">Reach a rank</option>
        <option value="wins">Win matches</option>
        <option value="matches">Play matches</option>
        <option value="kills">Get kills</option>
        <option value="kd">Season K/D</option>
      </select>
      <template v-if="kind === 'rank'">
        <select v-model.number="rankTarget" aria-label="Target rank">
          <option v-for="(n, i) in RANK_NAMES" :key="n" :value="i">{{ n }}</option>
        </select>
        <span class="muted">in</span>
        <select v-model="track" aria-label="Mode">
          <option v-for="r in rankTracks" :key="r.track" :value="r.track">{{ r.trackName }}</option>
        </select>
      </template>
      <template v-else-if="kind === 'kd'">
        <input v-model.number="kd" type="number" min="0.1" max="20" step="0.1" aria-label="Target K/D" />
      </template>
      <template v-else>
        <input v-model.number="amount" type="number" min="1" max="500" aria-label="Target" />
        <select v-model="period" aria-label="Period">
          <option value="today">today</option>
          <option value="week">this week</option>
        </select>
      </template>
      <button type="submit" class="btn-primary">Add</button>
      <button type="button" class="cancel" @click="adding = false">Cancel</button>
    </form>

    <p v-if="!progress.length && !adding" class="empty">
      Set a target, like reaching Gold in Reload or winning 3 matches this week, and track it here.
    </p>

    <ul v-else class="list">
      <li v-for="p in progress" :key="p.goal.id" class="goal" :class="{ done: p.value >= 1 }">
        <div class="row">
          <span class="title">{{ describe(p.goal) }}</span>
          <span class="text">{{ p.value >= 1 ? 'Done ✓' : p.text }}</span>
          <button type="button" class="remove" :aria-label="`Remove goal: ${describe(p.goal)}`" @click="removeGoal(p.goal.id)">×</button>
        </div>
        <div class="bar"><span :style="{ width: `${Math.round(p.value * 100)}%` }" /></div>
      </li>
    </ul>
  </section>
</template>

<style scoped>
.goals {
  display: flex;
  flex-direction: column;
  gap: var(--s3);
}
.head {
  display: flex;
  align-items: center;
  justify-content: space-between;
}
.head .card-title {
  margin: 0;
}
.add,
.cancel {
  background: none;
  border: none;
  color: var(--accent);
  font-family: var(--display);
  font-weight: 800;
  font-size: 15px;
  text-transform: uppercase;
  letter-spacing: 0.04em;
}
.cancel {
  color: var(--muted);
}
.form {
  display: flex;
  flex-wrap: wrap;
  gap: var(--s2);
  align-items: center;
  padding: var(--s3);
}
.form input {
  flex: 0 0 90px;
}
.empty {
  margin: 0;
  color: var(--muted);
  border: 1px dashed var(--border);
  border-radius: var(--radius);
  padding: var(--s4);
  font-size: 14px;
}
.list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: var(--s2);
}
.goal {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: var(--radius-sm);
  padding: var(--s3) var(--s4);
}
.goal.done {
  border-color: color-mix(in srgb, var(--live) 45%, var(--border));
}
.row {
  display: flex;
  align-items: center;
  gap: var(--s3);
}
.title {
  flex: 1;
  font-weight: 600;
}
.text {
  font-family: var(--display);
  font-weight: 800;
  font-size: 16px;
  color: var(--muted);
}
.goal.done .text {
  color: var(--live);
}
.remove {
  background: none;
  border: none;
  font-size: 18px;
  color: var(--faint);
  padding: 0 4px;
}
.remove:hover {
  color: var(--danger);
}
.bar {
  margin-top: var(--s2);
  height: 6px;
  border-radius: 3px;
  background: var(--surface-2);
  overflow: hidden;
}
.bar span {
  display: block;
  height: 100%;
  background: linear-gradient(90deg, var(--accent), color-mix(in srgb, var(--accent) 60%, var(--live)));
  transition: width 0.4s ease;
}
.goal.done .bar span {
  background: var(--live);
}
</style>
