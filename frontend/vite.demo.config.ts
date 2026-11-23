import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { defineConfig } from 'vite'
import { viteSingleFile } from 'vite-plugin-singlefile'

// Self-contained demo build (no backend): `npm run build:demo` → dist-demo/index.html, shareable as one web page.
export default defineConfig({
  plugins: [react(), tailwindcss(), viteSingleFile()],
  define: { 'import.meta.env.VITE_DEMO': JSON.stringify('1') },
  build: { outDir: 'dist-demo', emptyOutDir: true, chunkSizeWarningLimit: 5000 },
})
