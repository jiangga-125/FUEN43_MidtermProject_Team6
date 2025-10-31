import './styles/theme.css'
import { createApp } from 'vue'
import App from './App.vue'
import router from './router'
import { initAuth } from '@/stores/auth'

const app = createApp(App)
await initAuth()
app.use(router).mount('#app')
