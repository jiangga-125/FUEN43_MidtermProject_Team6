import './styles/theme.css'
import { createApp } from 'vue'
import App from './App.vue'
import router from './router'
import { initAuth } from '@/stores/auth'
import { createPinia } from 'pinia'

import { initAuth } from '@/stores/auth'
const app = createApp(App)
app.use(createPinia())
app.use(router)

useAuth().tryLoadSession().finally(() => {
app.mount('#app')
})
