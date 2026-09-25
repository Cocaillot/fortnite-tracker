import { createApp } from 'vue'
import './styles.css'
import App from './App.vue'
import { initTheme } from './composables/useTheme'
import { initNotes } from './composables/notes'
import { initGoals } from './composables/goals'
import { initToasts } from './composables/toasts'

// Before mounting, so the saved look is there from the first frame.
initTheme()
initNotes()
initGoals()
initToasts()
createApp(App).mount('#app')
