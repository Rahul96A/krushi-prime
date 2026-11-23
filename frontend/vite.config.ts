import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { defineConfig } from 'vite'

// Dev: /api is proxied to the .NET API. Build: output goes to the API's wwwroot so one App Service serves both.
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: { port: 5173, proxy: { '/api': 'http://localhost:5080', '/health': 'http://localhost:5080' } },
  build: { outDir: process.env.KP_OUT_DIR ?? 'dist', emptyOutDir: true, chunkSizeWarningLimit: 1200 },
})
