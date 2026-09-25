import { createApp } from 'vue'
import './styles.css'
import App from './App.vue'
import { initTheme } from './composables/useTheme'

// Before mounting, so the saved look is there from the first frame.
initTheme()
createApp(App).mount('#app')
