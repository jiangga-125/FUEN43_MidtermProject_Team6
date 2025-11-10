import './styles/theme.css'
import { createApp } from 'vue'
import App from './App.vue'
import router from './router'
import { createPinia } from 'pinia'
import { useAuth } from '@/stores/auth' // <-- 先 import store
import 'bootstrap-icons/font/bootstrap-icons.css'


const app = createApp(App)
const pinia = createPinia()
app.use(pinia)
app.use(router)

// 這裡要在 pinia 註冊後呼叫 store
const auth = useAuth()
auth.tryLoadSession().finally(() => {
  app.mount('#app')
})