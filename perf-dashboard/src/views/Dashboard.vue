<template>
  <div class="dashboard">
    <!-- 统计卡片 -->
    <div class="grid" v-if="stats">
      <div class="stat-card">
        <h3>Total Test Runs</h3>
        <div class="value">{{ stats.totalRuns }}</div>
        <div class="label">Performance Tests</div>
        <div class="trend" v-if="stats.totalRuns > 0">
          📊 {{ stats.categories?.length || 0 }} Categories
        </div>
      </div>
      
      <div class="stat-card">
        <h3>Latest Run</h3>
        <div class="value">{{ formatTime(stats.latestRun) }}</div>
        <div class="label">{{ formatDate(stats.latestRun) }}</div>
        <div class="trend" v-if="timeSinceLastRun">
          ⏱️ {{ timeSinceLastRun }}
        </div>
      </div>
      
      <div class="stat-card">
        <h3>Categories</h3>
        <div class="value">{{ stats.categories?.length || 0 }}</div>
        <div class="label">Test Categories</div>
        <div class="trend">
          {{ categoryList }}
        </div>
      </div>
      
      <div class="stat-card">
        <h3>Measurements</h3>
        <div class="value">{{ Object.keys(stats.measurements || {}).length }}</div>
        <div class="label">Unique Metrics</div>
        <div class="trend">
          📈 Tracking Performance
        </div>
      </div>
    </div>

    <!-- 过滤器 -->
    <div class="card filters-card" v-if="stats">
      <h2>🔍 Filters & Options</h2>
      <div class="filters">
        <div class="filter-group">
          <label>Category:</label>
          <select v-model="selectedCategory" class="filter-select">
            <option value="">All Categories</option>
            <option v-for="cat in stats.categories" :key="cat" :value="cat">
              {{ cat }}
            </option>
          </select>
        </div>
        
        <div class="filter-group">
          <label>Show:</label>
          <select v-model="displayLimit" class="filter-select">
            <option :value="5">Last 5 runs</option>
            <option :value="10">Last 10 runs</option>
            <option :value="20">Last 20 runs</option>
            <option :value="perfData.length">All runs</option>
          </select>
        </div>

        <div class="filter-group">
          <label>Compare Mode:</label>
          <button 
            @click="toggleCompareMode" 
            :class="['btn', compareMode ? 'btn-primary' : 'btn-secondary']"
          >
            {{ compareMode ? '✓ Enabled' : 'Disabled' }}
          </button>
        </div>
      </div>
    </div>

    <!-- 性能回归警告 -->
    <div class="card alert-card" v-if="performanceRegressions.length > 0">
      <h2>⚠️ Performance Regressions Detected</h2>
      <div class="regressions-list">
        <div v-for="reg in performanceRegressions" :key="reg.metric" class="regression-item">
          <div class="regression-icon">📉</div>
          <div class="regression-info">
            <strong>{{ reg.metric }}</strong>
            <span class="regression-change">{{ reg.change }}% slower than average</span>
          </div>
          <div class="regression-values">
            <span class="current">{{ reg.current }} {{ reg.unit }}</span>
            <span class="separator">vs</span>
            <span class="average">{{ reg.average }} {{ reg.unit }}</span>
          </div>
        </div>
      </div>
    </div>

    <!-- 图表区域 -->
    <div class="card" v-if="filteredPerfData.length > 0">
      <h2>📊 Performance Trends</h2>
      <PerformanceChart :data="filteredPerfData" :category="selectedCategory" />
    </div>

    <!-- 按类别分组的快速概览 -->
    <div class="card" v-if="categoryMetrics && Object.keys(categoryMetrics).length > 0">
      <h2>📊 Performance Overview by Category</h2>
      <div class="category-metrics">
        <div v-for="(metrics, category) in categoryMetrics" :key="category" class="category-section">
          <h3 class="category-title">{{ category }}</h3>
          <div class="metrics-grid">
            <div v-for="metric in metrics" :key="metric.name" class="metric-item">
              <div class="metric-name">{{ metric.name }}</div>
              <div class="metric-stats">
                <div class="stat">
                  <span class="stat-label">Latest:</span>
                  <span class="stat-value latest">{{ metric.latest }} {{ metric.unit }}</span>
                </div>
                <div class="stat">
                  <span class="stat-label">Avg:</span>
                  <span class="stat-value">{{ metric.average }} {{ metric.unit }}</span>
                </div>
                <div class="stat">
                  <span class="stat-label">Best:</span>
                  <span class="stat-value best">{{ metric.min }} {{ metric.unit }}</span>
                </div>
                <div class="stat">
                  <span class="stat-label">Worst:</span>
                  <span class="stat-value worst">{{ metric.max }} {{ metric.unit }}</span>
                </div>
              </div>
              <div class="metric-trend" :class="metric.trendClass">
                {{ metric.trendIndicator }} {{ metric.trendText }}
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- 最近测试运行 -->
    <div class="card">
      <h2>📋 Recent Test Runs</h2>
      
      <div v-if="loading" class="loading">
        <div class="spinner"></div>
        <p>Loading performance data...</p>
      </div>
      
      <div v-else-if="perfData.length === 0" class="empty-state">
        <p>📭 No performance data found</p>
        <p style="font-size: 0.9rem; margin-top: 0.5rem;">
          Run tests to generate performance data
        </p>
      </div>
      
      <div v-else class="table-container">
        <table>
          <thead>
            <tr>
              <th v-if="compareMode">
                <input type="checkbox" @change="toggleSelectAll" :checked="isAllSelected">
              </th>
              <th>Timestamp</th>
              <th>Category</th>
              <th>Branch</th>
              <th>Environment</th>
              <th>Measurements</th>
              <th>Key Metrics</th>
              <th>Actions</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="item in displayedPerfData" :key="item.filename" 
                :class="{ 'selected': selectedRuns.includes(item.filename) }">
              <td v-if="compareMode">
                <input 
                  type="checkbox" 
                  :checked="selectedRuns.includes(item.filename)"
                  @change="toggleRunSelection(item.filename)"
                >
              </td>
              <td>
                <div class="timestamp-cell">
                  <div class="date">{{ formatDate(item.timestamp) }}</div>
                  <div class="time">{{ formatTime(item.timestamp) }}</div>
                </div>
              </td>
              <td>
                <div class="categories-cell">
                  <span 
                    v-for="cat in getCategories(item)" 
                    :key="cat" 
                    class="badge badge-info category-badge"
                  >
                    {{ cat }}
                  </span>
                </div>
              </td>
              <td>{{ item.data.Meta?.Branch || '-' }}</td>
              <td>
                <div class="env-cell">
                  <div>{{ item.data.Environment?.OS?.split(' ')[0] || '-' }}</div>
                  <div class="env-detail">{{ item.data.Environment?.Configuration }}</div>
                </div>
              </td>
              <td>
                <span class="badge badge-success">
                  {{ item.data.Measurements?.length || 0 }} metrics
                </span>
              </td>
              <td>
                <div class="key-metrics">
                  <div v-for="metric in getKeyMetrics(item)" :key="metric.name" class="key-metric">
                    <span class="metric-name">{{ metric.name }}:</span>
                    <span class="metric-value">{{ metric.value }} {{ metric.unit }}</span>
                  </div>
                </div>
              </td>
              <td>
                <router-link 
                  :to="`/details/${item.filename}`" 
                  class="btn btn-primary btn-sm"
                >
                  View Details
                </router-link>
              </td>
            </tr>
          </tbody>
        </table>

        <!-- 比较按钮 -->
        <div v-if="compareMode && selectedRuns.length > 1" class="compare-actions">
          <button @click="compareSelected" class="btn btn-primary">
            Compare {{ selectedRuns.length }} Selected Runs
          </button>
        </div>
      </div>
    </div>

    <!-- 实时通知 -->
    <div v-if="notification" class="notification">
      {{ notification }}
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, onUnmounted, inject, computed } from 'vue'
import { useRouter } from 'vue-router'
import PerformanceChart from '../components/PerformanceChart.vue'

const router = useRouter()
const perfData = ref([])
const stats = ref(null)
const loading = ref(true)
const notification = ref('')
const { ws, isConnected } = inject('websocket')

// 新增状态
const selectedCategory = ref('')
const displayLimit = ref(10)
const compareMode = ref(false)
const selectedRuns = ref([])

// 加载数据
async function loadData() {
  try {
    loading.value = true
    
    const [dataRes, statsRes] = await Promise.all([
      fetch('http://localhost:3000/api/perf-data'),
      fetch('http://localhost:3000/api/stats')
    ])
    
    perfData.value = await dataRes.json()
    stats.value = await statsRes.json()
  } catch (error) {
    console.error('Error loading data:', error)
    showNotification('❌ Failed to load data')
  } finally {
    loading.value = false
  }
}

// 显示通知
function showNotification(message) {
  notification.value = message
  setTimeout(() => {
    notification.value = ''
  }, 3000)
}

// 格式化日期
function formatDate(timestamp) {
  if (!timestamp) return '-'
  const date = new Date(timestamp)
  return date.toLocaleDateString()
}

// 格式化时间
function formatTime(timestamp) {
  if (!timestamp) return '-'
  const date = new Date(timestamp)
  return date.toLocaleTimeString()
}

// 计算过滤后的数据
const filteredPerfData = computed(() => {
  if (!selectedCategory.value) return perfData.value
  
  return perfData.value.filter(item => {
    return item.data.Measurements?.some(m => m.Category === selectedCategory.value)
  })
})

// 显示的数据（应用限制）
const displayedPerfData = computed(() => {
  return filteredPerfData.value.slice(0, displayLimit.value)
})

// 计算距离上次运行的时间
const timeSinceLastRun = computed(() => {
  if (!stats.value?.latestRun) return ''
  
  const now = new Date()
  const last = new Date(stats.value.latestRun)
  const diff = now - last
  
  const minutes = Math.floor(diff / 60000)
  const hours = Math.floor(diff / 3600000)
  const days = Math.floor(diff / 86400000)
  
  if (days > 0) return `${days} day${days > 1 ? 's' : ''} ago`
  if (hours > 0) return `${hours} hour${hours > 1 ? 's' : ''} ago`
  if (minutes > 0) return `${minutes} min ago`
  return 'Just now'
})

// 分类列表
const categoryList = computed(() => {
  if (!stats.value?.categories) return ''
  return stats.value.categories.join(', ')
})

// 获取测试的分类
function getCategories(item) {
  const categories = new Set()
  item.data.Measurements?.forEach(m => {
    if (m.Category) categories.add(m.Category)
  })
  return Array.from(categories)
}

// 获取关键指标
function getKeyMetrics(item) {
  const metrics = item.data.Measurements?.slice(0, 2) || []
  return metrics.map(m => ({
    name: m.Name.length > 20 ? m.Name.substring(0, 20) + '...' : m.Name,
    value: typeof m.Value === 'number' ? m.Value.toFixed(2) : m.Value,
    unit: m.Unit
  }))
}

// 按类别分组的指标
const categoryMetrics = computed(() => {
  if (!stats.value?.measurements) return {}
  
  const grouped = {}
  
  Object.values(stats.value.measurements).forEach(measurement => {
    const category = measurement.category || 'Other'
    
    if (!grouped[category]) {
      grouped[category] = []
    }
    
    const latest = measurement.values[measurement.values.length - 1]?.value || 0
    const trend = calculateTrend(measurement.values)
    
    grouped[category].push({
      name: measurement.name,
      unit: measurement.unit,
      latest: latest.toFixed(2),
      average: measurement.avg.toFixed(2),
      min: measurement.min.toFixed(2),
      max: measurement.max.toFixed(2),
      trendIndicator: trend.indicator,
      trendText: trend.text,
      trendClass: trend.class
    })
  })
  
  return grouped
})

// 计算趋势
function calculateTrend(values) {
  if (values.length < 2) return { indicator: '➡️', text: 'Stable', class: 'stable' }
  
  const latest = values[values.length - 1]?.value || 0
  const previous = values[values.length - 2]?.value || 0
  
  if (latest === previous) return { indicator: '➡️', text: 'No change', class: 'stable' }
  
  const change = ((latest - previous) / previous * 100).toFixed(1)
  
  if (latest < previous) {
    return { indicator: '📈', text: `${Math.abs(change)}% better`, class: 'improving' }
  } else {
    return { indicator: '📉', text: `${change}% worse`, class: 'regressing' }
  }
}

// 性能回归检测
const performanceRegressions = computed(() => {
  if (!stats.value?.measurements) return []
  
  const regressions = []
  
  Object.values(stats.value.measurements).forEach(measurement => {
    const latest = measurement.values[measurement.values.length - 1]?.value || 0
    const avg = measurement.avg
    
    // 如果最新值比平均值高20%以上（性能下降）
    if (latest > avg * 1.2) {
      const change = ((latest - avg) / avg * 100).toFixed(1)
      regressions.push({
        metric: measurement.name,
        current: latest.toFixed(2),
        average: avg.toFixed(2),
        change: change,
        unit: measurement.unit
      })
    }
  })
  
  return regressions.slice(0, 5) // 只显示前5个
})

// 切换比较模式
function toggleCompareMode() {
  compareMode.value = !compareMode.value
  if (!compareMode.value) {
    selectedRuns.value = []
  }
}

// 切换运行选择
function toggleRunSelection(filename) {
  const index = selectedRuns.value.indexOf(filename)
  if (index > -1) {
    selectedRuns.value.splice(index, 1)
  } else {
    if (selectedRuns.value.length < 5) { // 最多选择5个
      selectedRuns.value.push(filename)
    } else {
      showNotification('⚠️ Maximum 5 runs can be compared')
    }
  }
}

// 全选/取消全选
const isAllSelected = computed(() => {
  return displayedPerfData.value.length > 0 && 
         selectedRuns.value.length === displayedPerfData.value.length
})

function toggleSelectAll() {
  if (isAllSelected.value) {
    selectedRuns.value = []
  } else {
    selectedRuns.value = displayedPerfData.value.slice(0, 5).map(item => item.filename)
  }
}

// 比较选中的运行
function compareSelected() {
  router.push({
    name: 'Compare',
    params: { filename: selectedRuns.value[0] },
    query: { compare: selectedRuns.value.slice(1).join(',') }
  })
}

// 监听 WebSocket 消息
function setupWebSocket() {
  const socket = ws()
  if (!socket) return
  
  socket.addEventListener('message', (event) => {
    const message = JSON.parse(event.data)
    
    switch (message.type) {
      case 'initial-data':
        perfData.value = message.data
        break
        
      case 'new-file':
        showNotification('✨ New performance data available!')
        perfData.value.unshift({
          filename: message.filename,
          timestamp: message.data.Meta?.Timestamp,
          data: message.data
        })
        loadData() // 重新加载统计数据
        break
        
      case 'file-changed':
        showNotification('🔄 Performance data updated')
        const index = perfData.value.findIndex(item => item.filename === message.filename)
        if (index !== -1) {
          perfData.value[index] = {
            filename: message.filename,
            timestamp: message.data.Meta?.Timestamp,
            data: message.data
          }
        }
        break
        
      case 'file-removed':
        showNotification('🗑️ Performance data removed')
        perfData.value = perfData.value.filter(item => item.filename !== message.filename)
        loadData() // 重新加载统计数据
        break
    }
  })
}

onMounted(() => {
  loadData()
  setupWebSocket()
})
</script>

<style scoped>
.dashboard {
  position: relative;
}

.stat-card .trend {
  margin-top: 0.75rem;
  font-size: 0.8rem;
  color: #a0aec0;
  font-weight: 500;
}

.filters-card {
  background: linear-gradient(135deg, rgba(102, 126, 234, 0.03) 0%, rgba(118, 75, 162, 0.03) 100%);
  border: 2px solid rgba(102, 126, 234, 0.1);
}

.filters {
  display: flex;
  gap: 2rem;
  flex-wrap: wrap;
  align-items: center;
}

.filter-group {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.filter-group label {
  font-size: 0.875rem;
  font-weight: 600;
  color: #4a5568;
  text-transform: uppercase;
  letter-spacing: 0.3px;
}

.filter-select {
  padding: 0.625rem 1rem;
  border: 2px solid #e2e8f0;
  border-radius: 10px;
  font-size: 0.9rem;
  color: #2d3748;
  background: white;
  cursor: pointer;
  transition: all 0.2s;
  min-width: 200px;
}

.filter-select:hover {
  border-color: #667eea;
}

.filter-select:focus {
  outline: none;
  border-color: #667eea;
  box-shadow: 0 0 0 3px rgba(102, 126, 234, 0.1);
}

.alert-card {
  background: linear-gradient(135deg, rgba(245, 101, 101, 0.05) 0%, rgba(237, 137, 54, 0.05) 100%);
  border-left: 4px solid #f56565;
}

.regressions-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.regression-item {
  display: flex;
  align-items: center;
  gap: 1rem;
  padding: 1rem;
  background: white;
  border-radius: 10px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05);
}

.regression-icon {
  font-size: 2rem;
  flex-shrink: 0;
}

.regression-info {
  flex: 1;
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.regression-info strong {
  color: #2d3748;
  font-size: 1rem;
}

.regression-change {
  color: #e53e3e;
  font-size: 0.875rem;
  font-weight: 600;
}

.regression-values {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  font-size: 0.9rem;
}

.regression-values .current {
  color: #e53e3e;
  font-weight: 700;
}

.regression-values .separator {
  color: #a0aec0;
}

.regression-values .average {
  color: #48bb78;
  font-weight: 700;
}

.category-metrics {
  display: flex;
  flex-direction: column;
  gap: 2rem;
}

.category-section {
  border-left: 4px solid #667eea;
  padding-left: 1.5rem;
}

.category-title {
  font-size: 1.25rem;
  font-weight: 700;
  color: #2d3748;
  margin-bottom: 1rem;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.metrics-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(300px, 1fr));
  gap: 1rem;
}

.metric-item {
  background: white;
  padding: 1.25rem;
  border-radius: 10px;
  border: 2px solid #e2e8f0;
  transition: all 0.3s ease;
}

.metric-item:hover {
  border-color: #667eea;
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(102, 126, 234, 0.15);
}

.metric-name {
  font-weight: 700;
  color: #2d3748;
  margin-bottom: 0.75rem;
  font-size: 0.9rem;
}

.metric-stats {
  display: grid;
  grid-template-columns: repeat(2, 1fr);
  gap: 0.75rem;
  margin-bottom: 0.75rem;
}

.stat {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.stat-label {
  font-size: 0.75rem;
  color: #718096;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  font-weight: 600;
}

.stat-value {
  font-size: 1rem;
  font-weight: 700;
  color: #4a5568;
}

.stat-value.latest {
  color: #667eea;
}

.stat-value.best {
  color: #48bb78;
}

.stat-value.worst {
  color: #ed8936;
}

.metric-trend {
  padding: 0.5rem;
  border-radius: 6px;
  font-size: 0.875rem;
  font-weight: 600;
  text-align: center;
}

.metric-trend.improving {
  background: #c6f6d5;
  color: #22543d;
}

.metric-trend.regressing {
  background: #fed7d7;
  color: #742a2a;
}

.metric-trend.stable {
  background: #e6fffa;
  color: #234e52;
}

.timestamp-cell {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.timestamp-cell .date {
  font-weight: 600;
  color: #2d3748;
}

.timestamp-cell .time {
  font-size: 0.85rem;
  color: #718096;
}

.categories-cell {
  display: flex;
  flex-wrap: wrap;
  gap: 0.375rem;
}

.category-badge {
  font-size: 0.75rem;
  padding: 0.25rem 0.625rem;
}

.env-cell {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.env-detail {
  font-size: 0.85rem;
  color: #718096;
}

.key-metrics {
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
}

.key-metric {
  font-size: 0.85rem;
  display: flex;
  gap: 0.5rem;
}

.metric-name {
  color: #718096;
  font-weight: 500;
}

.metric-value {
  color: #2d3748;
  font-weight: 700;
}

tbody tr.selected {
  background: linear-gradient(135deg, rgba(102, 126, 234, 0.08) 0%, rgba(118, 75, 162, 0.08) 100%);
}

.compare-actions {
  margin-top: 1.5rem;
  padding-top: 1.5rem;
  border-top: 2px solid #e2e8f0;
  text-align: center;
}

.btn-sm {
  padding: 0.5rem 1rem;
  font-size: 0.85rem;
}

.notification {
  position: fixed;
  bottom: 2rem;
  right: 2rem;
  background: white;
  padding: 1rem 1.75rem;
  border-radius: 12px;
  box-shadow: 0 10px 40px rgba(102, 126, 234, 0.3);
  animation: slideIn 0.4s cubic-bezier(0.68, -0.55, 0.265, 1.55);
  z-index: 1000;
  border-left: 4px solid #667eea;
  font-weight: 600;
  color: #2d3748;
}

@keyframes slideIn {
  from {
    transform: translateX(120%) scale(0.8);
    opacity: 0;
  }
  to {
    transform: translateX(0) scale(1);
    opacity: 1;
  }
}
</style>
