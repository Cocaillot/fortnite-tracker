<script setup lang="ts">
export type Page = 'live' | 'leaderboard' | 'history' | 'me' | 'settings'

defineProps<{
  page: Page | 'profile'
  status: { state: 'idle' | 'lobby' | 'live'; label: string; timer: string | null; detail: string | null }
  version: string | null
  updateVersion: string | null
}>()
const emit = defineEmits<{ navigate: [page: Page]; applyUpdate: [] }>()

const items: { id: Page; label: string; icon: string }[] = [
  { id: 'live', label: 'Live', icon: 'M12 12m-2 0a2 2 0 1 0 4 0a2 2 0 1 0-4 0M6.3 17.7a8 8 0 0 1 0-11.4M17.7 6.3a8 8 0 0 1 0 11.4M9.2 14.8a4 4 0 0 1 0-5.6M14.8 9.2a4 4 0 0 1 0 5.6' },
  { id: 'leaderboard', label: 'Leaderboard', icon: 'M8 21h8M12 17v4M7 4h10v5a5 5 0 0 1-10 0zM7 6H4v2a3 3 0 0 0 3 3M17 6h3v2a3 3 0 0 1-3 3' },
  { id: 'history', label: 'History', icon: 'M12 7v5l3 2M3.5 12a8.5 8.5 0 1 0 2.5-6M3 4v4h4' },
  { id: 'me', label: 'My profile', icon: 'M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8zM4.5 20a7.5 7.5 0 0 1 15 0' },
]
</script>

<template>
  <nav class="sidebar" aria-label="Main">
    <ul class="nav">
      <li v-for="item in items" :key="item.id">
        <button type="button" :class="{ active: page === item.id }" :aria-current="page === item.id ? 'page' : undefined" @click="emit('navigate', item.id)">
          <svg viewBox="0 0 24 24" aria-hidden="true"><path :d="item.icon" /></svg>
          <span>{{ item.label }}</span>
        </button>
      </li>
    </ul>

    <div class="bottom">
      <button v-if="updateVersion" type="button" class="update" @click="emit('applyUpdate')">
        <span class="update-title">Update ready</span>
        <span>Restart to install {{ updateVersion }}</span>
      </button>

      <div class="status" :class="status.state">
        <div class="status-row">
          <span class="dot" aria-hidden="true" />
          <span class="status-label">{{ status.label }}</span>
          <span v-if="status.timer" class="timer">{{ status.timer }}</span>
        </div>
        <span v-if="status.detail" class="status-detail">{{ status.detail }}</span>
      </div>

      <ul class="nav">
        <li>
          <button type="button" :class="{ active: page === 'settings' }" @click="emit('navigate', 'settings')">
            <svg viewBox="0 0 24 24" aria-hidden="true">
              <path d="M12 9a3 3 0 1 0 0 6 3 3 0 0 0 0-6zm7.4 4.1l1.6 1.2-1.7 3-1.9-.7a7 7 0 0 1-1.8 1l-.3 2H10.7l-.3-2a7 7 0 0 1-1.8-1l-1.9.7-1.7-3 1.6-1.2a7 7 0 0 1 0-2.1L5 9.8l1.7-3 1.9.7a7 7 0 0 1 1.8-1l.3-2h3.4l.3 2c.7.3 1.2.6 1.8 1l1.9-.7 1.7 3-1.6 1.2a7 7 0 0 1 0 2.1z" />
            </svg>
            <span>Settings</span>
          </button>
        </li>
      </ul>
      <p v-if="version" class="version">Version {{ version }}</p>
    </div>
  </nav>
</template>

<style scoped>
.sidebar {
  width: 240px;
  flex: none;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  gap: var(--s4);
  padding: var(--s4) var(--s3);
  border-right: 1px solid var(--border);
  background: #0d1016;
}
.nav {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.nav button {
  width: 100%;
  display: flex;
  align-items: center;
  gap: var(--s3);
  padding: 10px var(--s3);
  background: none;
  border: none;
  border-radius: 10px;
  color: var(--muted);
  font-family: var(--display);
  font-weight: 700;
  font-size: 18px;
  letter-spacing: 0.04em;
  text-transform: uppercase;
  text-align: left;
  position: relative;
}
.nav button:hover {
  background: var(--surface);
  color: var(--text);
}
.nav button.active {
  background: var(--surface-2);
  color: var(--text);
}
.nav button.active::before {
  content: '';
  position: absolute;
  left: 0;
  top: 8px;
  bottom: 8px;
  width: 3px;
  border-radius: 2px;
  background: var(--accent);
}
.nav svg {
  width: 22px;
  height: 22px;
  flex: none;
  fill: none;
  stroke: currentColor;
  stroke-width: 1.7;
  stroke-linecap: round;
  stroke-linejoin: round;
}
.bottom {
  display: flex;
  flex-direction: column;
  gap: var(--s3);
}
.status {
  background: var(--surface);
  border: 1px solid var(--border);
  border-radius: 10px;
  padding: var(--s3);
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.status-row {
  display: flex;
  align-items: center;
  gap: var(--s2);
}
.dot {
  width: 9px;
  height: 9px;
  border-radius: 50%;
  background: var(--faint);
  flex: none;
}
.status-label {
  font-family: var(--display);
  font-weight: 800;
  font-size: 16px;
  letter-spacing: 0.03em;
  text-transform: uppercase;
  color: var(--muted);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.timer {
  margin-left: auto;
  font-family: var(--display);
  font-weight: 800;
  font-size: 16px;
  font-variant-numeric: tabular-nums;
}
.status-detail {
  font-size: 12px;
  color: var(--faint);
  padding-left: 17px;
}
.status.lobby .dot {
  background: var(--accent);
}
.status.lobby .status-label {
  color: var(--accent);
}
.status.live {
  border-color: color-mix(in srgb, var(--live) 40%, var(--border));
}
.status.live .status-label {
  color: var(--live);
}
.status.live .dot {
  background: var(--live);
  animation: pulse 1.6s ease-out infinite;
}
@keyframes pulse {
  0% { box-shadow: 0 0 0 0 color-mix(in srgb, var(--live) 60%, transparent); }
  100% { box-shadow: 0 0 0 8px transparent; }
}
.update {
  display: flex;
  flex-direction: column;
  align-items: flex-start;
  gap: 2px;
  background: color-mix(in srgb, var(--accent) 14%, var(--surface));
  border: 1px solid color-mix(in srgb, var(--accent) 40%, transparent);
  border-radius: 10px;
  padding: var(--s3);
  text-align: left;
  font-size: 13px;
  color: var(--text);
}
.update-title {
  font-family: var(--display);
  font-weight: 800;
  font-size: 15px;
  text-transform: uppercase;
  color: var(--accent);
}
.version {
  margin: 0;
  padding: 0 var(--s3);
  font-size: 12px;
  color: var(--faint);
}
</style>
