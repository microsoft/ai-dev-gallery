<template>
  <div id="app">
    <header class="header">
      <div class="container">
        <h1>🎯 AI Dev Gallery - Performance Dashboard</h1>
        <div class="status">
          <span :class="['status-indicator', isConnected ? 'connected' : 'disconnected']"></span>
          <span>{{ isConnected ? 'Connected' : 'Disconnected' }}</span>
        </div>
      </div>
    </header>
    
    <main class="main">
      <div class="container">
        <router-view />
      </div>
    </main>
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted, provide } from 'vue'
import { useRouter } from 'vue-router'

const isConnected = ref(false)
const router = useRouter()
let ws = null

// 连接 WebSocket
function connectWebSocket() {
  ws = new WebSocket('ws://localhost:3000')
  
  ws.onopen = () => {
    console.log('WebSocket connected')
    isConnected.value = true
  }
  
  ws.onclose = () => {
    console.log('WebSocket disconnected')
    isConnected.value = false
    
    // 5秒后重连
    setTimeout(connectWebSocket, 5000)
  }
  
  ws.onerror = (error) => {
    console.error('WebSocket error:', error)
  }
}

onMounted(() => {
  connectWebSocket()
})

onUnmounted(() => {
  if (ws) {
    ws.close()
  }
})

// 提供 WebSocket 实例给子组件
provide('websocket', { ws: () => ws, isConnected })
</script>

<style>
#app {
  min-height: 100vh;
  background: #f7fafc;
}
</style>
