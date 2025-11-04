import './styles/theme.css'
import { createApp } from 'vue'
import App from './App.vue'
import router from './router'
import { initAuth } from '@/stores/auth'
import { createPinia } from 'pinia'

const app = createApp(App) // 先建立 app
const pinia = createPinia()
app.use(pinia) // 再掛載 Pinia

await initAuth() // 你的初始化 auth
app.use(router).mount('#app') // 最後掛載 router 並 mount