import { createApp } from 'vue'
import { createRouter, createWebHistory } from 'vue-router'
import App from './App.vue'
import Dashboard from './views/Dashboard.vue'
import Details from './views/Details.vue'
import Compare from './views/Compare.vue'
import './style.css'

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: '/',
      name: 'Dashboard',
      component: Dashboard
    },
    {
      path: '/details/:filename',
      name: 'Details',
      component: Details
    },
    {
      path: '/compare/:filename',
      name: 'Compare',
      component: Compare
    }
  ]
})

const app = createApp(App)
app.use(router)
app.mount('#app')
