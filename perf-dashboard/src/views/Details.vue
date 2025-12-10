<template>
  <div class="details">
    <div class="breadcrumb">
      <router-link to="/" class="btn btn-secondary">← Back to Dashboard</router-link>
    </div>

    <div v-if="loading" class="loading">
      <div class="spinner"></div>
      <p>Loading details...</p>
    </div>

    <div v-else-if="data">
      <!-- Meta Information -->
      <div class="card">
        <h2>ℹ️ Test Run Information</h2>
        <div class="info-grid">
          <div class="info-item">
            <span class="label">Run ID:</span>
            <span class="value">{{ data.Meta?.RunId }}</span>
          </div>
          <div class="info-item">
            <span class="label">Timestamp:</span>
            <span class="value">{{ formatDateTime(data.Meta?.Timestamp) }}</span>
          </div>
          <div class="info-item">
            <span class="label">Branch:</span>
            <span class="value">{{ data.Meta?.Branch }}</span>
          </div>
          <div class="info-item">
            <span class="label">Commit:</span>
            <span class="value">{{ data.Meta?.CommitHash }}</span>
          </div>
          <div class="info-item">
            <span class="label">Trigger:</span>
            <span class="badge badge-info">{{ data.Meta?.Trigger }}</span>
          </div>
        </div>
      </div>

      <!-- Environment -->
      <div class="card">
        <h2>💻 Environment</h2>
        <div class="info-grid">
          <div class="info-item">
            <span class="label">OS:</span>
            <span class="value">{{ data.Environment?.OS }}</span>
          </div>
          <div class="info-item">
            <span class="label">Platform:</span>
            <span class="value">{{ data.Environment?.Platform }}</span>
          </div>
          <div class="info-item">
            <span class="label">Configuration:</span>
            <span class="value">{{ data.Environment?.Configuration }}</span>
          </div>
          <div class="info-item">
            <span class="label">CPU:</span>
            <span class="value">{{ data.Environment?.Hardware?.Cpu }}</span>
          </div>
          <div class="info-item">
            <span class="label">RAM:</span>
            <span class="value">{{ data.Environment?.Hardware?.Ram }}</span>
          </div>
          <div class="info-item" v-if="data.Environment?.Hardware?.Gpu">
            <span class="label">GPU:</span>
            <span class="value">{{ data.Environment?.Hardware?.Gpu }}</span>
          </div>
        </div>
      </div>

      <!-- Measurements by Category -->
      <div class="card" v-for="[category, measurements] in groupedMeasurements" :key="category">
        <h2>📊 {{ category }} Measurements</h2>
        <div class="measurements-grid">
          <div class="measurement-card" v-for="m in measurements" :key="m.Name">
            <h3>{{ m.Name }}</h3>
            <div class="measurement-value">
              {{ formatValue(m.Value) }} <span class="unit">{{ m.Unit }}</span>
            </div>
            <div class="measurement-tags" v-if="m.Tags && Object.keys(m.Tags).length > 0">
              <span class="tag" v-for="[key, value] in Object.entries(m.Tags)" :key="key">
                {{ key }}: {{ value }}
              </span>
            </div>
          </div>
        </div>
      </div>

      <!-- All Measurements Table -->
      <div class="card">
        <h2>📋 All Measurements</h2>
        <div class="table-container">
          <table>
            <thead>
              <tr>
                <th>Category</th>
                <th>Name</th>
                <th>Value</th>
                <th>Unit</th>
                <th>Tags</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="(m, index) in data.Measurements" :key="index">
                <td>
                  <span class="badge badge-info">{{ m.Category }}</span>
                </td>
                <td>{{ m.Name }}</td>
                <td class="value-cell">{{ formatValue(m.Value) }}</td>
                <td>{{ m.Unit }}</td>
                <td>
                  <div class="tags-cell">
                    <span class="tag" v-for="[key, value] in Object.entries(m.Tags || {})" :key="key">
                      {{ key }}: {{ value }}
                    </span>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>

    <div v-else class="empty-state">
      <p>❌ Data not found</p>
    </div>
  </div>
</template>

<script setup>
import { ref, onMounted, computed } from 'vue'
import { useRoute } from 'vue-router'

const route = useRoute()
const data = ref(null)
const loading = ref(true)

const groupedMeasurements = computed(() => {
  if (!data.value?.Measurements) return []
  
  const groups = {}
  data.value.Measurements.forEach(m => {
    if (!groups[m.Category]) {
      groups[m.Category] = []
    }
    groups[m.Category].push(m)
  })
  
  return Object.entries(groups)
})

async function loadData() {
  try {
    loading.value = true
    const response = await fetch(`http://localhost:3000/api/perf-data/${route.params.filename}`)
    data.value = await response.json()
  } catch (error) {
    console.error('Error loading data:', error)
  } finally {
    loading.value = false
  }
}

function formatDateTime(timestamp) {
  if (!timestamp) return '-'
  const date = new Date(timestamp)
  return date.toLocaleString()
}

function formatValue(value) {
  if (typeof value === 'number') {
    return value.toLocaleString(undefined, { maximumFractionDigits: 2 })
  }
  return value
}

onMounted(() => {
  loadData()
})
</script>

<style scoped>
.details {
  max-width: 1200px;
  margin: 0 auto;
}

.breadcrumb {
  margin-bottom: 2rem;
}

.info-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
  gap: 1.5rem;
}

.info-item {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  padding: 1rem;
  background: linear-gradient(135deg, rgba(102, 126, 234, 0.03) 0%, rgba(118, 75, 162, 0.03) 100%);
  border-radius: 8px;
  border-left: 3px solid #667eea;
}

.info-item .label {
  font-size: 0.875rem;
  color: #718096;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.3px;
}

.info-item .value {
  font-size: 1.125rem;
  color: #2d3748;
  font-weight: 600;
}

.measurements-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 1.5rem;
}

.measurement-card {
  background: white;
  border-radius: 12px;
  padding: 1.75rem;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.05), 0 10px 30px rgba(0, 0, 0, 0.05);
  border-top: 4px solid transparent;
  transition: all 0.3s ease;
  position: relative;
  overflow: hidden;
}

.measurement-card::before {
  content: '';
  position: absolute;
  top: 0;
  right: 0;
  width: 80px;
  height: 80px;
  background: linear-gradient(135deg, rgba(102, 126, 234, 0.1) 0%, rgba(118, 75, 162, 0.05) 100%);
  border-radius: 0 0 0 80px;
}

.measurement-card:nth-child(4n+1) {
  border-top-color: #667eea;
}

.measurement-card:nth-child(4n+2) {
  border-top-color: #48bb78;
}

.measurement-card:nth-child(4n+3) {
  border-top-color: #ed8936;
}

.measurement-card:nth-child(4n+4) {
  border-top-color: #9f7aea;
}

.measurement-card:hover {
  transform: translateY(-4px);
  box-shadow: 0 4px 6px rgba(0, 0, 0, 0.05), 0 20px 40px rgba(102, 126, 234, 0.15);
}

.measurement-card h3 {
  font-size: 0.875rem;
  margin-bottom: 0.75rem;
  color: #718096;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.3px;
}

.measurement-value {
  font-size: 2rem;
  font-weight: 800;
  margin-bottom: 0.75rem;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
}

.measurement-value .unit {
  font-size: 1rem;
  color: #a0aec0;
  font-weight: 600;
}

.measurement-tags {
  display: flex;
  flex-wrap: wrap;
  gap: 0.5rem;
  margin-top: 0.75rem;
}

.tag {
  background: linear-gradient(135deg, rgba(102, 126, 234, 0.1) 0%, rgba(118, 75, 162, 0.1) 100%);
  padding: 0.375rem 0.75rem;
  border-radius: 6px;
  font-size: 0.75rem;
  font-weight: 600;
  color: #667eea;
  border: 1px solid rgba(102, 126, 234, 0.2);
}

.measurement-card .tag {
  color: #667eea;
}

.tags-cell {
  display: flex;
  flex-wrap: wrap;
  gap: 0.375rem;
}

.tags-cell .tag {
  background: linear-gradient(135deg, rgba(102, 126, 234, 0.05) 0%, rgba(118, 75, 162, 0.05) 100%);
  color: #667eea;
}

.value-cell {
  font-weight: 700;
  font-size: 1.125rem;
  background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  background-clip: text;
}
</style>
