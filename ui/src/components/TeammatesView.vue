<script setup lang="ts">
import { t, locale } from '../i18n'
import { onMounted } from 'vue'
import type { TeammateSummary } from '../bridge'
import PlayerAvatar from './PlayerAvatar.vue'

// Your record with everyone you've partied with, from match history.
defineProps<{ teammates: TeammateSummary[] | null }>()
const emit = defineEmits<{ refresh: []; open: [accountId: string | null, name: string | null] }>()
onMounted(() => emit('refresh'))

const hours = (min: number) => (min >= 60 ? `${Math.floor(min / 60)} h ${String(Math.round(min % 60)).padStart(2, '0')}` : `${Math.round(min)} min`)
const day = (iso: string) => new Date(iso).toLocaleDateString(locale(), { day: 'numeric', month: 'short' })
</script>

<template>
  <div v-if="!teammates" class="skeleton" style="height: 200px" />
  <div v-else-if="!teammates.length" class="card empty">
    {{ t('No teammates yet. Matches played in a party show up here with your record together.') }}
  </div>
  <div v-else class="table-wrap">
    <table class="data">
      <thead>
        <tr>
          <th>{{ t('Teammate') }}</th>
          <th class="num">{{ t('Matches together') }}</th>
          <th class="num" :title="t('Wins confirmed from your stats')">{{ t('Wins') }}</th>
          <th class="num" :title="t('Kills are known for matches played with the app running')">{{ t('Your kills') }}</th>
          <th class="num">{{ t('Time together') }}</th>
          <th class="num">{{ t('Last played') }}</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="mate in teammates" :key="mate.accountId" class="clickable" tabindex="0" @click="emit('open', mate.accountId, null)" @keydown.enter="emit('open', mate.accountId, null)">
          <td>
            <div class="who">
              <PlayerAvatar :name="mate.name" :size="38" />
              <span class="name" :class="{ faint: !mate.name }">{{ mate.name ?? t('Private profile') }}</span>
            </div>
          </td>
          <td class="num"><span class="stat-value">{{ mate.matches }}</span></td>
          <td class="num"><span class="stat-value" :class="{ gold: mate.wins }">{{ mate.wins }}</span></td>
          <td class="num">
            <span class="stat-value">{{ mate.kills ?? '–' }}</span>
            <span v-if="mate.kills !== null && mate.tracked < mate.matches" class="faint small"> {{ t('in {n}', { n: mate.tracked }) }}</span>
          </td>
          <td class="num muted">{{ hours(mate.minutes) }}</td>
          <td class="num muted">{{ day(mate.lastPlayedUtc) }}</td>
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
