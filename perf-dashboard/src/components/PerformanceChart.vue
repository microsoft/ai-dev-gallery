<template>
  <div class="chart-container">
    <div class="chart-controls">
      <div class="control-group">
        <label>Metric:</label>
        <select v-model="selectedMetric" class="metric-select">
          <option value="">Select a metric...</option>
          <option v-for="metric in availableMetrics" :key="metric" :value="metric">
            {{ metric }}
          </option>
        </select>
      </div>

      <div class="control-group" v-if="selectedMetric">
        <label>Chart Type:</label>
        <div class="chart-type-buttons">
          <button 
            @click="chartType = 'line'" 
            :class="['chart-type-btn', chartType === 'line' ? 'active' : '']"
          >
            📈 Line
          </button>
          <button 
            @click="chartType = 'bar'" 
            :class="['chart-type-btn', chartType === 'bar' ? 'active' : '']"
          >
            📊 Bar
          </button>
        </div>
      </div>

      <div class="control-group" v-if="selectedMetric && metricStats">
        <div class="metric-summary">
          <div class="summary-item">
            <span class="summary-label">Average:</span>
            <span class="summary-value">{{ metricStats.avg }} {{ metricStats.unit }}</span>
          </div>
          <div class="summary-item">
            <span class="summary-label">Best:</span>
            <span class="summary-value best">{{ metricStats.min }} {{ metricStats.unit }}</span>
          </div>
          <div class="summary-item">
            <span class="summary-label">Worst:</span>
            <span class="summary-value worst">{{ metricStats.max }} {{ metricStats.unit }}</span>
          </div>
        </div>
      </div>
    </div>
    
    <div ref="chartRef" class="chart" style="width: 100%; height: 450px;"></div>
    
    <div v-if="!selectedMetric" class="chart-placeholder">
      <div class="placeholder-icon">📈</div>
      <p>Select a metric to view the trend chart</p>
      <p class="placeholder-hint">Choose from {{ availableMetrics.length }} available metrics</p>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, watch, computed } from 'vue'
import * as echarts from 'echarts'

const props = defineProps({
  data: {
    type: Array,
    required: true
  },
  category: {
    type: String,
    default: ''
  }
})

const chartRef = ref(null)
const selectedMetric = ref('')
const chartType = ref('line')
let chartInstance = null

// 获取所有可用的指标
const availableMetrics = computed(() => {
  const metrics = new Set()
  
  props.data.forEach(item => {
    item.data.Measurements?.forEach(m => {
      // 如果有分类过滤，只显示该分类的指标
      if (!props.category || m.Category === props.category) {
        metrics.add(m.Name)
      }
    })
  })
  
  return Array.from(metrics).sort()
})

// 指标统计信息
const metricStats = computed(() => {
  if (!selectedMetric.value) return null
  
  const values = []
  let unit = ''
  
  props.data.forEach(item => {
    const measurement = item.data.Measurements?.find(m => m.Name === selectedMetric.value)
    if (measurement) {
      values.push(measurement.Value)
      unit = measurement.Unit
    }
  })
  
  if (values.length === 0) return null
  
  return {
    avg: (values.reduce((sum, v) => sum + v, 0) / values.length).toFixed(2),
    min: Math.min(...values).toFixed(2),
    max: Math.max(...values).toFixed(2),
    unit: unit
  }
})

// 准备图表数据
const prepareChartData = () => {
  if (!selectedMetric.value) return null
  
  const chartData = []
  const timestamps = []
  
  // 反转数组以按时间顺序显示
  const sortedData = [...props.data].reverse()
  
  sortedData.forEach(item => {
    const measurement = item.data.Measurements?.find(m => m.Name === selectedMetric.value)
    
    if (measurement) {
      timestamps.push(new Date(item.timestamp).toLocaleString())
      chartData.push({
        value: measurement.Value,
        unit: measurement.Unit
      })
    }
  })
  
  return { timestamps, chartData }
}

// 初始化图表
const initChart = () => {
  if (!chartRef.value) return
  
  chartInstance = echarts.init(chartRef.value)
  updateChart()
}

// 更新图表
const updateChart = () => {
  if (!chartInstance) return
  
  const data = prepareChartData()
  
  if (!data || data.chartData.length === 0) {
    chartInstance.clear()
    return
  }
  
  const unit = data.chartData[0]?.unit || ''
  const values = data.chartData.map(d => d.value)
  const avg = values.reduce((sum, v) => sum + v, 0) / values.length
  
  const option = {
    title: {
      text: selectedMetric.value,
      left: 'center',
      textStyle: {
        color: '#2d3748',
        fontSize: 18,
        fontWeight: 700
      }
    },
    tooltip: {
      trigger: 'axis',
      backgroundColor: 'rgba(255, 255, 255, 0.95)',
      borderColor: '#667eea',
      borderWidth: 2,
      textStyle: {
        color: '#2d3748'
      },
      formatter: (params) => {
        const param = params[0]
        const value = param.value
        const diffFromAvg = ((value - avg) / avg * 100).toFixed(1)
        const indicator = value > avg ? '📉' : '📈'
        
        return `
          <div style="padding: 5px;">
            <strong>${param.axisValue}</strong><br/>
            ${param.marker}Value: <strong>${value} ${unit}</strong><br/>
            ${indicator} ${Math.abs(diffFromAvg)}% ${value > avg ? 'above' : 'below'} average
          </div>
        `
      }
    },
    grid: {
      left: '3%',
      right: '4%',
      bottom: '10%',
      top: '15%',
      containLabel: true
    },
    xAxis: {
      type: 'category',
      boundaryGap: chartType.value === 'bar',
      data: data.timestamps,
      axisLabel: {
        rotate: 45,
        fontSize: 11,
        color: '#718096'
      },
      axisLine: {
        lineStyle: {
          color: '#e2e8f0'
        }
      }
    },
    yAxis: {
      type: 'value',
      name: unit,
      nameTextStyle: {
        color: '#718096',
        fontSize: 12,
        padding: [0, 0, 0, 10]
      },
      axisLabel: {
        color: '#718096',
        formatter: (value) => {
          if (value >= 1000) {
            return (value / 1000).toFixed(1) + 'K'
          }
          return value.toFixed(0)
        }
      },
      axisLine: {
        lineStyle: {
          color: '#e2e8f0'
        }
      },
      splitLine: {
        lineStyle: {
          color: '#f7fafc',
          type: 'dashed'
        }
      }
    },
    series: [
      {
        name: selectedMetric.value,
        type: chartType.value,
        smooth: true,
        data: values,
        areaStyle: chartType.value === 'line' ? {
          color: new echarts.graphic.LinearGradient(0, 0, 0, 1, [
            { offset: 0, color: 'rgba(102, 126, 234, 0.4)' },
            { offset: 1, color: 'rgba(102, 126, 234, 0.05)' }
          ])
        } : undefined,
        lineStyle: chartType.value === 'line' ? {
          color: '#667eea',
          width: 3
        } : undefined,
        itemStyle: {
          color: (params) => {
            // 根据值与平均值的关系设置颜色
            const value = params.value
            if (value > avg * 1.1) return '#ed8936' // 高于平均10%
            if (value < avg * 0.9) return '#48bb78' // 低于平均10%
            return '#667eea'
          },
          borderRadius: chartType.value === 'bar' ? [8, 8, 0, 0] : 0
        },
        emphasis: {
          itemStyle: {
            shadowBlur: 10,
            shadowColor: 'rgba(102, 126, 234, 0.5)'
          }
        },
        markLine: {
          data: [
            {
              type: 'average',
              name: 'Average',
              lineStyle: {
                color: '#9f7aea',
                type: 'dashed',
                width: 2
              },
              label: {
                formatter: 'Avg: {c} ' + unit,
                color: '#9f7aea'
              }
            }
          ]
        }
      }
    ]
  }
  
  chartInstance.setOption(option, true)
}

// 监听数据变化
watch(() => props.data, () => {
  if (chartInstance && selectedMetric.value) {
    updateChart()
  }
}, { deep: true })

// 监听选中的指标变化
watch(selectedMetric, () => {
  updateChart()
})

// 监听图表类型变化
watch(chartType, () => {
  updateChart()
})

// 监听分类过滤变化
watch(() => props.category, () => {
  // 如果当前选中的指标不在新的分类中，重置选择
  if (selectedMetric.value && !availableMetrics.value.includes(selectedMetric.value)) {
    selectedMetric.value = availableMetrics.value[0] || ''
  }
})

// 组件挂载时初始化图表
onMounted(() => {
  initChart()
  
  // 自动选择第一个指标
  if (availableMetrics.value.length > 0) {
    selectedMetric.value = availableMetrics.value[0]
  }
  
  // 响应式调整图表大小
  window.addEventListener('resize', () => {
    if (chartInstance) {
      chartInstance.resize()
    }
  })
})
</script>

<style scoped>
.chart-container {
  position: relative;
}

.chart-controls {
  margin-bottom: 1.5rem;
  display: flex;
  gap: 2rem;
  align-items: flex-start;
  flex-wrap: wrap;
  padding: 1.5rem;
  background: linear-gradient(135deg, rgba(102, 126, 234, 0.03) 0%, rgba(118, 75, 162, 0.03) 100%);
  border-radius: 12px;
}

.control-group {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.control-group label {
  font-size: 0.875rem;
  font-weight: 600;
  color: #4a5568;
  text-transform: uppercase;
  letter-spacing: 0.3px;
}

.metric-select {
  padding: 0.625rem 1.25rem;
  border: 2px solid #e2e8f0;
  border-radius: 10px;
  font-size: 0.9rem;
  color: #2d3748;
  background: white;
  cursor: pointer;
  transition: all 0.2s;
  min-width: 280px;
  font-weight: 500;
}

.metric-select:hover {
  border-color: #667eea;
}

.metric-select:focus {
  outline: none;
  border-color: #667eea;
  box-shadow: 0 0 0 3px rgba(102, 126, 234, 0.1);
}

.chart-type-buttons {
  display: flex;
  gap: 0.5rem;
}

.chart-type-btn {
  padding: 0.625rem 1.25rem;
  border: 2px solid #e2e8f0;
  border-radius: 10px;
  background: white;
  color: #4a5568;
  font-size: 0.9rem;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s;
}

.chart-type-btn:hover {
  border-color: #667eea;
  background: rgba(102, 126, 234, 0.05);
}

.chart-type-btn.active {
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  color: white;
  border-color: #667eea;
}

.metric-summary {
  display: flex;
  gap: 1.5rem;
  padding: 0.75rem 1.25rem;
  background: white;
  border-radius: 10px;
  border: 2px solid #e2e8f0;
}

.summary-item {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
}

.summary-label {
  font-size: 0.75rem;
  color: #718096;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  font-weight: 600;
}

.summary-value {
  font-size: 1rem;
  font-weight: 700;
  color: #2d3748;
}

.summary-value.best {
  color: #48bb78;
}

.summary-value.worst {
  color: #ed8936;
}

.chart {
  min-height: 450px;
  background: white;
  border-radius: 8px;
}

.chart-placeholder {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  height: 450px;
  color: #718096;
  background: linear-gradient(135deg, rgba(102, 126, 234, 0.03) 0%, rgba(118, 75, 162, 0.03) 100%);
  border-radius: 12px;
}

.placeholder-icon {
  font-size: 4rem;
  margin-bottom: 1rem;
  opacity: 0.5;
}

.chart-placeholder p {
  font-size: 1.125rem;
  font-weight: 600;
  color: #4a5568;
  margin: 0.5rem 0;
}

.placeholder-hint {
  font-size: 0.9rem;
  color: #a0aec0;
  font-weight: 500;
}
</style>
