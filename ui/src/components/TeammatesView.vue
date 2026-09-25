<script setup lang="ts">
import { onMounted } from 'vue'
import type { TeammateSummary } from '../bridge'
import PlayerAvatar from './PlayerAvatar.vue'

// Your record with everyone you've partied with, from match history.
defineProps<{ teammates: TeammateSummary[] | null }>()
const emit = defineEmits<{ refresh: []; open: [accountId: string | null, name: string | null] }>()
onMounted(() => emit('refresh'))

const hours = (min: number) => (min >= 60 ? `${Math.floor(min / 60)}h ${String(Math.round(min % 60)).padStart(2, '0')}` : `${Math.round(min)} min`)
const day = (iso: string) => new Date(iso).toLocaleDateString('en-GB', { day: 'numeric', month: 'short' })
</script>

<template>
  <div v-if="!teammates" class="skeleton" style="height: 200px" />
  <div v-else-if="!teammates.length" class="card empty">
    No teammates yet. Matches played in a party show up here with your record together.
  </div>
  <div v-else class="table-wrap">
    <table class="data">
      <thead>
        <tr>
          <th>Teammate</th>
          <th class="num">Matches together</th>
          <th class="num" title="Wins confirmed from your stats">Wins</th>
          <th class="num" title="Kills are known for matches played with the app running">Your kills</th>
          <th class="num">Time together</th>
          <th class="num">Last played</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="t in teammates" :key="t.accountId" class="clickable" tabindex="0" @click="emit('open', t.accountId, null)" @keydown.enter="emit('open', t.accountId, null)">
          <td>
            <div class="who">
              <PlayerAvatar :name="t.name" :size="38" />
              <span class="name" :class="{ faint: !t.name }">{{ t.name ?? 'Private profile' }}</span>
            </div>
          </td>
          <td class="num"><span class="stat-value">{{ t.matches }}</span></td>
          <td class="num"><span class="stat-value" :class="{ gold: t.wins }">{{ t.wins }}</span></td>
          <td class="num">
            <span class="stat-value">{{ t.kills ?? '–' }}</span>
            <span v-if="t.kills !== null && t.tracked < t.matches" class="faint small"> in {{ t.tracked }}</span>
          </td>
          <td class="num muted">{{ hours(t.minutes) }}</td>
          <td class="num muted">{{ day(t.lastPlayedUtc) }}</td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<style scoped>
.empty {
  color: var(--muted);
  text-align: center;
}
.who {
  display: flex;
  align-items: center;
  gap: var(--s3);
}
.name {
  font-family: var(--display);
  font-weight: 800;
  font-size: 19px;
}
.gold {
  color: var(--rarity-legendary);
}
.small {
  font-size: 12px;
}
</style>
