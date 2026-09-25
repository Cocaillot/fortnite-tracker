<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { on, send, type LobbySnapshot, type Settings } from '../bridge'
import { keysLabel, langChoice, setLanguage, t, type LangChoice } from '../i18n'
import { theme } from '../composables/useTheme'

// First-run guide: language, Fortnite detection, stats key (checked live), preferences.
const props = defineProps<{ settings: Settings; snapshot: LobbySnapshot | null }>()

const steps = ['welcome', 'fortnite', 'key', 'prefs', 'done'] as const
const step = ref(0)
const current = computed(() => steps[step.value])

const languages: { id: LangChoice; label: string }[] = [
  { id: 'auto', label: 'Same as Windows' },
  { id: 'en', label: 'English' },
  { id: 'fr', label: 'Français' },
]

// ---- Stats key ----
const key = ref('')
const checking = ref(false)
const keyResult = ref<'ok' | 'invalid' | 'offline' | null>(null)
let off: (() => void) | undefined
onMounted(() => {
  off = on('apiKeyTest', (r) => {
    checking.value = false
    keyResult.value = r
  })
})
onUnmounted(() => off?.())

function checkKey() {
  if (!key.value.trim()) return
  checking.value = true
  keyResult.value = null
  send({ type: 'testApiKey', key: key.value.trim() })
}
function saveAnyway() {
  send({ type: 'setApiKey', key: key.value.trim() })
  next()
}

// ---- Preferences (applied when leaving that step) ----
const launch = ref(true)
const notify = ref(props.settings.notifyOnElimination)
const ranks = ref(props.settings.notifyRankChanges)
const overlay = ref(props.settings.overlay.enabled)
function savePrefs() {
  send({ type: 'setLaunchWithFortnite', enabled: launch.value })
  send({ type: 'setNotify', enabled: notify.value })
  send({ type: 'setNotifyRanks', enabled: ranks.value })
  send({ type: 'setOverlay', enabled: overlay.value })
}

function next() {
  if (current.value === 'prefs') savePrefs()
  if (step.value < steps.length - 1) step.value++
}
const finish = () => send({ type: 'onboardingDone' })
</script>

<template>
  <div class="scrim" role="dialog" aria-modal="true" :aria-label="t('Welcome to Fortnite Tracker')">
    <div class="guide card">
      <div class="dots" aria-hidden="true">
        <span v-for="(s, i) in steps" :key="s" :class="{ on: i <= step }" />
      </div>

      <Transition name="step" mode="out-in">
        <section v-if="current === 'welcome'" key="welcome">
          <img v-if="theme.logo" class="logo custom" :src="theme.logo" alt="" />
          <svg v-else class="logo" viewBox="0 0 24 24" aria-hidden="true">
            <rect x="3" y="12" width="4.5" height="8" rx="1" /><rect x="9.75" y="8" width="4.5" height="12" rx="1" /><rect x="16.5" y="4" width="4.5" height="16" rx="1" />
          </svg>
          <h1>{{ t('Welcome to Fortnite Tracker') }}</h1>
          <p class="lead">
            {{ t('Your squad\'s stats and ranks, who eliminated you, your history and progress. Everything comes from Fortnite\'s own log files and public stats: nothing touches the game.') }}
          </p>
          <span class="label">{{ t('Language') }}</span>
          <div class="seg" role="radiogroup" :aria-label="t('Language')">
            <button
              v-for="l in languages"
              :key="l.id"
              type="button"
              role="radio"
              :aria-checked="langChoice === l.id"
              :class="{ on: langChoice === l.id }"
              @click="setLanguage(l.id)"
            >
              {{ l.id === 'auto' ? t(l.label) : l.label }}
            </button>
          </div>
        </section>

        <section v-else-if="current === 'fortnite'" key="fortnite">
          <h1>{{ t('Finding Fortnite') }}</h1>
          <div class="check" :class="settings.fortniteFound ? 'ok' : 'wait'">
            <span class="mark" aria-hidden="true">{{ settings.fortniteFound ? '✓' : '…' }}</span>
            <div>
              <strong>{{ settings.fortniteFound ? t('Fortnite found on this PC') : t('Fortnite hasn\'t been played on this PC yet') }}</strong>
              <p>
                {{
                  snapshot?.localName
                    ? t('Signed in as {name}.', { name: snapshot.localName })
                    : settings.fortniteFound
                      ? t('Launch Fortnite once and your account is detected automatically.')
                      : t('Launch Fortnite: the app picks it up by itself, no setup needed.')
                }}
              </p>
            </div>
          </div>
          <p class="muted">{{ t("The app only reads Fortnite's log file. It never touches the game's memory or network traffic.") }}</p>
        </section>

        <section v-else-if="current === 'key'" key="key">
          <h1>{{ t('Stats key') }}</h1>
          <p class="lead">{{ t('Stats come from fortnite-api.com. Their key is free and takes a minute:') }}</p>
          <ol class="howto">
            <li>{{ t('Open') }} <a href="https://fortnite-api.com/dashboard" target="_blank" rel="noopener">fortnite-api.com/dashboard</a> {{ t('and sign in with Discord.') }}</li>
            <li>{{ t('Copy your API key and paste it below.') }}</li>
          </ol>
          <div v-if="settings.hasApiKey && keyResult !== 'ok'" class="check ok small">
            <span class="mark" aria-hidden="true">✓</span>
            <div><strong>{{ t('A key is already saved.') }}</strong> <span class="muted">{{ t('Paste another one to replace it.') }}</span></div>
          </div>
          <form class="row" @submit.prevent="checkKey">
            <input v-model="key" type="password" autocomplete="off" :placeholder="t('Paste key')" :aria-label="t('Stats API key')" />
            <button type="submit" class="btn-primary" :disabled="!key.trim() || checking">{{ checking ? t('Checking…') : t('Check') }}</button>
          </form>
          <p v-if="keyResult === 'ok'" class="result good">{{ t('The key works and is saved.') }}</p>
          <p v-else-if="keyResult === 'invalid'" class="result bad">{{ t('This key was refused. Check that you copied all of it.') }}</p>
          <p v-else-if="keyResult === 'offline'" class="result bad">
            {{ t("Couldn't reach fortnite-api.com to check it.") }}
            <button type="button" class="link" @click="saveAnyway">{{ t('Save it anyway') }}</button>
          </p>
        </section>

        <section v-else-if="current === 'prefs'" key="prefs">
          <h1>{{ t('Your preferences') }}</h1>
          <label class="switch pref">
            <input v-model="launch" type="checkbox" /><span class="track" aria-hidden="true" />
            <span><strong>{{ t('Open with Fortnite') }}</strong><small>{{ t('The app waits in the tray and opens when the game starts.') }}</small></span>
          </label>
          <label class="switch pref">
            <input v-model="notify" type="checkbox" /><span class="track" aria-hidden="true" />
            <span><strong>{{ t('Notify me who eliminated me') }}</strong><small>{{ t('With their stats, right after your team is out.') }}</small></span>
          </label>
          <label class="switch pref">
            <input v-model="ranks" type="checkbox" /><span class="track" aria-hidden="true" />
            <span><strong>{{ t('Rank change alerts') }}</strong><small>{{ t('When you or a friend rank up.') }}</small></span>
          </label>
          <label class="switch pref">
            <input v-model="overlay" type="checkbox" /><span class="track" aria-hidden="true" />
            <span><strong>{{ t('In-game overlay') }}</strong><small>{{ t('A small bar over the game with your squad\'s K/D.') }}</small></span>
          </label>
        </section>

        <section v-else key="done">
          <h1>{{ t('You\'re all set') }}</h1>
          <p class="lead">{{ t('Play a match and everything fills in. Two shortcuts work even in game:') }}</p>
          <dl class="keys">
            <dt>{{ keysLabel(settings.hotkeys.window) }}</dt><dd>{{ t('Show or hide this window, even in game') }}</dd>
            <dt>{{ keysLabel(settings.hotkeys.overlay) }}</dt><dd>{{ t('Turn the in-game overlay on or off') }}</dd>
          </dl>
          <p class="muted">{{ t('You can change all of this later in Settings.') }}</p>
        </section>
      </Transition>

      <footer>
        <button v-if="current !== 'done'" type="button" class="skip" @click="finish">{{ t('Skip setup') }}</button>
        <span class="spacer" />
        <button v-if="step > 0 && current !== 'done'" type="button" class="ghost" @click="step--">{{ t('Back') }}</button>
        <button v-if="current !== 'done'" type="button" class="btn-primary" @click="next">
          {{ current === 'key' && !settings.hasApiKey && keyResult !== 'ok' ? t('Later') : t('Next') }}
        </button>
        <button v-else type="button" class="btn-primary" @click="finish">{{ t('Start') }}</button>
      </footer>
    </div>
  </div>
</template>

<style scoped>
.scrim {
  position: fixed;
  inset: 0;
  z-index: 50;
  display: grid;
  place-items: center;
  padding: var(--s5);
  background: color-mix(in srgb, var(--bg) 70%, transparent);
  backdrop-filter: blur(6px);
}
.guide {
  width: min(620px, 100%);
  padding: var(--s6);
  display: flex;
  flex-direction: column;
  gap: var(--s4);
  box-shadow: 0 30px 80px rgb(0 0 0 / 0.45);
}
.dots {
  display: flex;
  gap: 6px;
}
.dots span {
  height: 4px;
  flex: 1;
  border-radius: 2px;
  background: var(--surface-2);
  transition: background 0.3s ease;
}
.dots .on {
  background: var(--accent);
}
section {
  min-height: 300px;
  display: flex;
  flex-direction: column;
  gap: var(--s3);
  align-items: flex-start;
}
h1 {
  margin: 0;
  font-family: var(--display);
  font-size: 34px;
  text-transform: uppercase;
  letter-spacing: 0.01em;
}
.lead {
  margin: 0;
  font-size: 16px;
  line-height: 1.5;
}
.muted {
  margin: 0;
  color: var(--muted);
  font-size: 14px;
  line-height: 1.5;
}
.logo {
  width: 52px;
  height: 52px;
  fill: var(--accent);
}
.logo.custom {
  object-fit: contain;
  border-radius: 10px;
}
.label {
  margin-top: var(--s3);
  font-family: var(--display);
  font-weight: 800;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  font-size: 13px;
  color: var(--muted);
}
.check {
  display: flex;
  gap: var(--s3);
  align-items: flex-start;
  width: 100%;
  padding: var(--s4);
  border-radius: var(--radius);
  background: var(--surface-2);
  border: 1px solid var(--border);
}
.check.small {
  padding: var(--s3);
  align-items: center;
}
.check p {
  margin: 4px 0 0;
  color: var(--muted);
}
.mark {
  flex: none;
  width: 30px;
  height: 30px;
  border-radius: 50%;
  display: grid;
  place-items: center;
  font-weight: 800;
  background: var(--surface);
  color: var(--muted);
}
.check.ok .mark {
  background: color-mix(in srgb, var(--live) 20%, transparent);
  color: var(--live);
}
.howto {
  margin: 0;
  padding-left: 20px;
  line-height: 1.8;
}
.howto a {
  color: var(--accent);
}
.row {
  display: flex;
  gap: var(--s2);
  width: 100%;
}
.row input {
  flex: 1;
}
.result {
  margin: 0;
  font-weight: 600;
}
.result.good {
  color: var(--live);
}
.result.bad {
  color: var(--danger);
}
.link {
  background: none;
  border: none;
  padding: 0;
  color: var(--accent);
  text-decoration: underline;
  font: inherit;
}
.pref {
  width: 100%;
  padding: var(--s3) 0;
  border-bottom: 1px solid var(--border);
}
.pref:last-child {
  border-bottom: none;
}
.pref strong {
  display: block;
  font-family: var(--display);
  font-size: 17px;
}
.pref small {
  color: var(--muted);
  font-size: 13px;
}
.keys {
  display: grid;
  grid-template-columns: auto 1fr;
  gap: var(--s2) var(--s4);
  margin: 0;
}
.keys dt {
  font-family: var(--display);
  font-weight: 800;
  letter-spacing: 0.04em;
}
.keys dd {
  margin: 0;
  color: var(--muted);
}
footer {
  display: flex;
  align-items: center;
  gap: var(--s2);
}
.spacer {
  flex: 1;
}
.skip {
  background: none;
  border: none;
  padding: 0;
  color: var(--muted);
  font-size: 14px;
}
.skip:hover {
  color: var(--text);
}
.ghost {
  background: none;
  border: 1px solid var(--border);
  color: var(--text);
  font-family: var(--display);
  font-weight: 700;
  font-size: 14px;
  letter-spacing: 0.04em;
  text-transform: uppercase;
}
.step-enter-active,
.step-leave-active {
  transition: opacity 0.18s ease, transform 0.18s ease;
}
.step-enter-from {
  opacity: 0;
  transform: translateX(16px);
}
.step-leave-to {
  opacity: 0;
  transform: translateX(-16px);
}
</style>
