import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import vueDevTools from 'vite-plugin-vue-devtools'

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue(), vueDevTools()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  server: {
    // https: true,
    port: 5173, // 前端開發埠
    proxy: {
      '/api': {
        target: 'https://localhost:7176', // 後端 https 埠
        changeOrigin: true,
        secure: false,
      },
    },
  },
})
