<script setup lang="ts">
import { computed } from 'vue'
import type { LobbySnapshot, RankProgress } from '../bridge'
import PlayerCard from './PlayerCard.vue'

const props = defineProps<{
  snapshot: LobbySnapshot | null
  ranks: Record<string, RankProgress[]>
}>()
const emit = defineEmits<{ open: [accountId: string | null, name: string | null] }>()
const onOpen = (accountId: string | null, name: string | null) => emit('open', accountId, name)

const you = computed(() => (props.snapshot?.localName ? props.snapshot.squad[0] : null))
const bucket = computed(() => props.snapshot?.statsBucket ?? null)
const label = computed(() => props.snapshot?.statsLabel)

const subtitle = computed(() => {
  const s = props.snapshot
  if (!s) return 'Waiting for Fortnite. Your squad appears here as soon as the game starts.'
  if (!s.gameRunning) return 'Fortnite is not running. Launch it and your squad shows up automatically.'
  return s.inMatch
    ? `In a ${s.mode} match. Stats shown are for ${s.statsLabel}.`
    : `In the lobby. After a match, the player who eliminated you appears on the right.`
})
</script>

<template>
  <div class="page">
    <div class="page-header">
      <div>
        <h1>Live</h1>
        <p>{{ subtitle }}</p>
      </div>
    </div>

    <div class="columns">
      <section class="squad">
        <h2 class="card-title">Your squad</h2>
        <TransitionGroup v-if="snapshot?.squad.length" name="list" tag="div" class="list">
          <PlayerCard
            v-for="(p, i) in snapshot.squad"
            :key="p.accountId ?? i"
            :player="p"
            :bucket="bucket"
            :mode-label="label"
            :is-you="i === 0 && !!snapshot.localName"
            :fallback-name="i === 0 ? snapshot.localName : null"
            :ranks="p.accountId ? ranks[p.accountId] : []"
            @open="onOpen"
          />
        </TransitionGroup>
        <div v-else class="empty-state">
          <p>No squad yet.</p>
          <span>Start Fortnite; you and your party members are detected from the game's log.</span>
        </div>
      </section>

      <section class="elimination">
        <h2 class="card-title">Last elimination</h2>
        <Transition name="slide" mode="out-in">
          <div v-if="snapshot?.eliminatedBy" :key="snapshot.lastMatchStartedUtc ?? ''" class="list">
            <PlayerCard :player="snapshot.eliminatedBy" :bucket="bucket" :mode-label="label" :you="you" variant="eliminator" @open="onOpen" />
            <template v-if="snapshot.spectated.length">
              <h3 class="sub-title">Also spectated</h3>
              <PlayerCard
                v-for="p in snapshot.spectated"
                :key="p.epicName ?? ''"
                :player="p"
                :bucket="bucket"
                :mode-label="label"
                :you="you"
                variant="opponent"
                @open="onOpen"
              />
            </template>
          </div>
          <div v-else class="empty-state">
            <p>Nobody has eliminated you yet.</p>
            <span>When your team is eliminated, their stats, threat level and how they compare to you show up here.</span>
          </div>
        </Transition>
      </section>
    </div>
  </div>
</template>

<style scoped>
.columns {
  display: grid;
  grid-template-columns: minmax(0, 1.2fr) minmax(0, 1fr);
  gap: var(--s6);
  align-items: start;
}
@media (max-width: 1180px) {
  .columns {
    grid-template-columns: minmax(0, 1fr);
  }
}
.list {
  display: flex;
  flex-direction: column;
  gap: var(--s4);
}
.sub-title {
  margin: var(--s2) 0 0;
  font-family: var(--display);
  font-weight: 800;
  font-size: 15px;
  letter-spacing: 0.06em;
  text-transform: uppercase;
  color: var(--faint);
}
.empty-state {
  border: 1px dashed var(--border);
  border-radius: var(--radius);
  padding: var(--s6) var(--s5);
  text-align: center;
  color: var(--muted);
}
.empty-state p {
  margin: 0 0 var(--s1);
  font-family: var(--display);
  font-weight: 800;
  font-size: 20px;
  color: var(--text);
}
.empty-state span {
  font-size: 14px;
}

/* The eliminator card slides in from the right when you die. */
.slide-enter-active {
  transition: transform 0.45s cubic-bezier(0.2, 0.9, 0.3, 1.2), opacity 0.3s ease;
}
.slide-leave-active {
  transition: opacity 0.15s ease;
}
.slide-enter-from {
  transform: translateX(40px) skewX(-4deg);
  opacity: 0;
}
.slide-leave-to {
  opacity: 0;
}
.list-enter-active {
  transition: transform 0.3s ease, opacity 0.3s ease;
}
.list-enter-from {
  transform: translateY(8px);
  opacity: 0;
}
</style>
