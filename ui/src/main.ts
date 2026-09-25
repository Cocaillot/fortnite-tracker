import { createApp } from 'vue'
import './styles.css'
import App from './App.vue'
import { initTheme } from './composables/useTheme'
import { initNotes } from './composables/notes'
import { initGoals } from './composables/goals'
import { initToasts } from './composables/toasts'
import { initI18n } from './i18n'

// Before mounting, so the saved look is there from the first frame.
initI18n()
initTheme()
initNotes()
initGoals()
initToasts()
createApp(App).mount('#app')
