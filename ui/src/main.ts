import { createApp } from 'vue'
import './styles.css'
import App from './App.vue'
import { initTheme } from './composables/useTheme'
import { initNotes } from './composables/notes'

// Before mounting, so the saved look is there from the first frame.
initTheme()
initNotes()
createApp(App).mount('#app')
