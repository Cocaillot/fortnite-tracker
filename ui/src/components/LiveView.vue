<script setup lang="ts">
import { computed, ref } from 'vue'
import type { LobbySnapshot, Platform, PlayerStats } from '../bridge'
import PlayerCard from './PlayerCard.vue'

const props = defineProps<{
  snapshot: LobbySnapshot | null
  lookupResult: PlayerStats | null
  lookingUp: boolean
}>()
const emit = defineEmits<{ lookup: [name: string, platform: Platform] }>()

const searchName = ref('')
const platform = ref<Platform>('epic')

const you = computed(() => (props.snapshot?.localName ? props.snapshot.squad[0] : null))
const bucket = computed(() => props.snapshot?.statsBucket ?? null)
const label = computed(() => props.snapshot?.statsLabel)

function submit() {
  if (searchName.value.trim()) emit('lookup', searchName.value.trim(), platform.value)
}
</script>

<template>
  <div class="live">
    <Transition name="slide">
      <section v-if="snapshot?.eliminatedBy" :key="snapshot.lastMatchStartedUtc ?? ''">
        <h2 class="section-title eliminated">Eliminated by</h2>
        <PlayerCard
          :player="snapshot.eliminatedBy"
          :bucket="bucket"
          :mode-label="label"
          :you="you"
          variant="eliminator"
        />
      </section>
    </Transition>

    <section>
      <h2 class="section-title">Your squad</h2>
      <TransitionGroup v-if="snapshot?.squad.length" name="list" tag="div" class="list">
        <PlayerCard
          v-for="(p, i) in snapshot.squad"
          :key="p.accountId ?? i"
          :player="p"
          :bucket="bucket"
          :mode-label="label"
          :is-you="i === 0 && !!snapshot.localName"
          :fallback-name="i === 0 ? snapshot.localName : null"
        />
      </TransitionGroup>
      <p v-else class="empty">Launch Fortnite. Your squad appears here automatically.</p>
    </section>

    <section v-if="snapshot?.spectated.length">
      <h2 class="section-title">Also spectated</h2>
      <TransitionGroup name="list" tag="div" class="list">
        <PlayerCard
          v-for="p in snapshot.spectated"
          :key="p.epicName ?? ''"
          :player="p"
          :bucket="bucket"
          :mode-label="label"
          :you="you"
          variant="opponent"
        />
      </TransitionGroup>
    </section>

    <section>
      <h2 class="section-title">Look up a player</h2>
      <form class="row" @submit.prevent="submit">
        <input v-model="searchName" placeholder="Epic, PSN or Xbox name" aria-label="Player name" />
        <select v-model="platform" aria-label="Platform">
          <option value="epic">Epic</option>
          <option value="psn">PSN</option>
          <option value="xbl">Xbox</option>
        </select>
        <button type="submit" class="btn-primary" :disabled="lookingUp">{{ lookingUp ? '…' : 'Search' }}</button>
      </form>
      <Transition name="fade">
        <PlayerCard
          v-if="lookupResult"
          :player="lookupResult"
          :bucket="null"
          mode-label="All modes"
          :you="you"
          variant="opponent"
          class="result"
        />
      </Transition>
    </section>
  </div>
</template>

<style scoped>
.live {
  display: flex;
  flex-direction: column;
  gap: 18px;
}
.eliminated {
  color: var(--danger);
}
.eliminated::before {
  content: '';
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--danger);
  box-shadow: 0 0 0 3px color-mix(in srgb, var(--danger) 25%, transparent);
}
.result {
  margin-top: 8px;
}

/* The eliminator card slides in from the right when you die. */
.slide-enter-active {
  transition: transform 0.45s cubic-bezier(0.2, 0.9, 0.3, 1.2), opacity 0.3s ease;
}
.slide-leave-active {
  transition: opacity 0.2s ease;
}
.slide-enter-from {
  transform: translateX(40px) skewX(-6deg);
  opacity: 0;
}
.slide-leave-to {
  opacity: 0;
}

.list-enter-active,
.fade-enter-active {
  transition: transform 0.3s ease, opacity 0.3s ease;
}
.list-enter-from,
.fade-enter-from {
  transform: translateY(8px);
  opacity: 0;
}
</style>
