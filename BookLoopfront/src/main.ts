import './styles/theme.css'
import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'
import { initAuth } from '@/stores/auth'

const app = createApp(App)
await initAuth()
app.use(createPinia()).use(router).mount('#app')
