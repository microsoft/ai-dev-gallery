<template>
  <div class="compare">
    <div class="breadcrumb">
      <router-link to="/" class="btn btn-secondary">← Back to Dashboard</router-link>
    </div>

    <div class="card">
      <h2>🔄 Compare Test Runs</h2>
      
      <div v-if="loading" class="loading">
        <div class="spinner"></div>
        <p>Loading comparison data...</p>
      </div>

      <div v-else-if="compareData.length > 0">
        <!-- 元信息对比 -->
        <div class="comparison-section">
          <h3>Test Run Information</h3>
          <table class="comparison-table">
            <thead>
              <tr>
                <th>Property</th>
                <th v-for="(item, index) in compareData" :key="index">
                  Run {{ index + 1 }}
                </th>
              </tr>
            </thead>
            <tbody>
              <tr>
                <td class="label-cell">Timestamp</td>
                <td v-for="(item, index) in compareData" :key="index">
                  {{ formatDateTime(item.Meta?.Timestamp) }}
                </td>
              </tr>
              <tr>
                <td class="label-cell">Branch</td>
                <td v-for="(item, index) in compareData" :key="index">
                  {{ item.Meta?.Branch || '-' }}
                </td>
              </tr>
              <tr>
                <td class="label-cell">Configuration</td>
                <td v-for="(item, index) in compareData" :key="index">
                  {{ item.Environment?.Configuration || '-' }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- 指标对比 -->
        <div class="comparison-section" v-for="category in categories" :key="category">
          <h3>{{ category }} Metrics</h3>
          <table class="comparison-table metrics-table">
            <thead>
              <tr>
                <th>Metric</th>
                <th v-for="(item, index) in compareData" :key="index">
                  Run {{ index + 1 }}
                </th>
                <th>Difference</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="metric in getMetricsByCategory(category)" :key="metric">
                <td class="label-cell">{{ metric }}</td>
                <td v-for="(item, index) in compareData" :key="index" 
                    :class="getCellClass(metric, index)">
                  {{ getMetricValue(item, metric) }}
                </td>
                <td class="diff-cell">
                  <span :class="getDiffClass(metric)">
                    {{ getDifference(metric) }}
                  </span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>

        <!-- 视觉对比图表 -->
        <div class="comparison-section">
          <h3>Visual Comparison</h3>
          <div ref="comparisonChartRef" class="comparison-chart"></div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, computed } from 'vue'
import { useRoute } from 'vue-router'
import * as echarts from 'echarts'

const route = useRoute()
const compareData = ref([])
const loading = ref(true)
const comparisonChartRef = ref(null)
let chartInstance = null

// 获取所有分类
const categories = computed(() => {
  const cats = new Set()
  compareData.value.forEach(item => {
    item.Measurements?.forEach(m => {
      if (m.Category) cats.add(m.Category)
    })
  })
  return Array.from(cats)
})

// 按分类获取指标
function getMetricsByCategory(category) {
  const metrics = new Set()
  compareData.value.forEach(item => {
    item.Measurements?.forEach(m => {
      if (m.Category === category) {
        metrics.add(m.Name)
      }
    })
  })
  return Array.from(metrics)
}

// 获取指标值
function getMetricValue(item, metricName) {
  const measurement = item.Measurements?.find(m => m.Name === metricName)
  if (!measurement) return '-'
  
  const value = typeof measurement.Value === 'number' 
    ? measurement.Value.toFixed(2) 
    : measurement.Value
  return `${value} ${measurement.Unit}`
}

// 获取单元格样式类
function getCellClass(metric, index) {
  const values = compareData.value.map(item => {
    const m = item.Measurements?.find(m => m.Name === metric)
    return m ? m.Value : null
  }).filter(v => v !== null)
  
  if (values.length < 2) return ''
  
  const current = values[index]
  const min = Math.min(...values)
  const max = Math.max(...values)
  
  if (current === min) return 'best-value'
  if (current === max) return 'worst-value'
  return ''
}

// 获取差异
function getDifference(metric) {
  const values = compareData.value.map(item => {
    const m = item.Measurements?.find(m => m.Name === metric)
    return m ? m.Value : null
  }).filter(v => v !== null)
  
  if (values.length < 2) return '-'
  
  const first = values[0]
  const last = values[values.length - 1]
  const diff = ((last - first) / first * 100).toFixed(1)
  
  if (diff > 0) return `+${diff}%`
  if (diff < 0) return `${diff}%`
  return 'No change'
}

// 获取差异样式类
function getDiffClass(metric) {
  const diff = getDifference(metric)
  if (diff === '-' || diff === 'No change') return 'diff-neutral'
  
  const value = parseFloat(diff)
  if (value > 0) return 'diff-worse'
  return 'diff-better'
}

// 格式化日期时间
function formatDateTime(timestamp) {
  if (!timestamp) return '-'
  const date = new Date(timestamp)
  return date.toLocaleString()
}

// 加载数据
async function loadData() {
  try {
    loading.value = true
    
    const filenames = [route.params.filename]
    if (route.query.compare) {
      filenames.push(...route.query.compare.split(','))
    }
    
    const promises = filenames.map(filename => 
      fetch(`http://localhost:3000/api/perf-data/${filename}`).then(r => r.json())
    )
    
    compareData.value = await Promise.all(promises)
    
    // 初始化对比图表
    setTimeout(() => {
      initComparisonChart()
    }, 100)
  } catch (error) {
    console.error('Error loading comparison data:', error)
  } finally {
    loading.value = false
  }
}

// 初始化对比图表
function initComparisonChart() {
  if (!comparisonChartRef.value) return
  
  chartInstance = echarts.init(comparisonChartRef.value)
  
  // 获取共同的指标
  const commonMetrics = getMetricsByCategory(categories.value[0] || '')
  const metric = commonMetrics[0]
  
  if (!metric) return
  
  const seriesData = compareData.value.map((item, index) => {
    const m = item.Measurements?.find(m => m.Name === metric)
    return {
      value: m ? m.Value : 0,
      name: `Run ${index + 1}`
    }
  })
  
  const option = {
    title: {
      text: metric,
      left: 'center',
      textStyle: {
        color: '#2d3748',
        fontSize: 16,
        fontWeight: 700
      }
    },
    tooltip: {
      trigger: 'item'
    },
    series: [
      {
        type: 'bar',
        data: seriesData,
        itemStyle: {
          color: (params) => {
            const colors = ['#667eea', '#48bb78', '#ed8936', '#9f7aea', '#f56565']
            return colors[params.dataIndex % colors.length]
          },
          borderRadius: [8, 8, 0, 0]
        }
      }
    ]
  }
  
  chartInstance.setOption(option)
}

onMounted(() => {
  loadData()
})
</script>

<style scoped>
.compare {
  max-width: 1400px;
  margin: 0 auto;
}

.breadcrumb {
  margin-bottom: 2rem;
}

.comparison-section {
  margin-bottom: 2rem;
}

.comparison-section h3 {
  font-size: 1.25rem;
  font-weight: 700;
  color: #2d3748;
  margin-bottom: 1rem;
  padding-bottom: 0.5rem;
  border-bottom: 2px solid #e2e8f0;
}

.comparison-table {
  width: 100%;
  border-collapse: collapse;
  margin-bottom: 1rem;
}

.comparison-table th {
  background: linear-gradient(135deg, rgba(102, 126, 234, 0.1) 0%, rgba(118, 75, 162, 0.1) 100%);
  padding: 1rem;
  text-align: left;
  font-weight: 700;
  color: #2d3748;
  border: 1px solid #e2e8f0;
}

.comparison-table td {
  padding: 0.875rem 1rem;
  border: 1px solid #e2e8f0;
  color: #4a5568;
}

.label-cell {
  font-weight: 600;
  background: #f7fafc;
  color: #2d3748;
}

.best-value {
  background: #c6f6d5;
  color: #22543d;
  font-weight: 700;
}

.worst-value {
  background: #fed7d7;
  color: #742a2a;
  font-weight: 700;
}

.diff-cell {
  text-align: center;
  font-weight: 700;
}

.diff-better {
  color: #48bb78;
}

.diff-worse {
  color: #e53e3e;
}

.diff-neutral {
  color: #718096;
}

.comparison-chart {
  width: 100%;
  height: 400px;
  margin-top: 1rem;
}
</style>
