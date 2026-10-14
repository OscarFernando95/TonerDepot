import './style.css'
import 'element-plus/dist/index.css'
import 'element-plus/theme-chalk/dark/css-vars.css'
import './styles/board-theme.css'

import { createApp } from 'vue'
import { createPinia } from 'pinia'
import App from './App.vue'
import router from './router'

document.documentElement.classList.add('dark')

const app = createApp(App)

app.use(createPinia())
app.use(router)

app.mount('#app')
