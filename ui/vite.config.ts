import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// The desktop host serves the built UI from its wwwroot folder (https://app.local/).
export default defineConfig({
  plugins: [vue()],
  base: './',
  server: { port: 5173, strictPort: true },
  build: {
    outDir: '../src/FortniteTracker.Desktop/wwwroot',
    emptyOutDir: true,
  },
})
