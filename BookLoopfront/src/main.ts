// src/main.ts
import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'
import { useAuth } from '@/stores/auth'

import { initAuth } from '@/stores/auth'
const app = createApp(App)
app.use(createPinia())
app.use(router)

useAuth().tryLoadSession().finally(() => {
app.mount('#app')
})
