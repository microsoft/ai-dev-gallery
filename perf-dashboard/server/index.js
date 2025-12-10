import express from 'express';
import { WebSocketServer } from 'ws';
import chokidar from 'chokidar';
import cors from 'cors';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const app = express();
const PORT = 3000;

app.use(cors());
app.use(express.json());

// 配置 PerfResults 文件夹路径
const PERF_RESULTS_PATH = path.resolve(__dirname, '../../AIDevGallery.Tests/bin/x64/Debug/net9.0-windows10.0.26100.0/win-x64/PerfResults');

// 读取所有性能数据文件
function getAllPerfData() {
  try {
    if (!fs.existsSync(PERF_RESULTS_PATH)) {
      console.warn(`PerfResults folder not found: ${PERF_RESULTS_PATH}`);
      return [];
    }

    const files = fs.readdirSync(PERF_RESULTS_PATH)
      .filter(file => file.endsWith('.json'))
      .sort()
      .reverse(); // 最新的在前

    const data = files.map(file => {
      const filePath = path.join(PERF_RESULTS_PATH, file);
      const content = fs.readFileSync(filePath, 'utf-8');
      const jsonData = JSON.parse(content);
      
      return {
        filename: file,
        timestamp: jsonData.Meta?.Timestamp || '',
        data: jsonData
      };
    });

    return data;
  } catch (error) {
    console.error('Error reading perf data:', error);
    return [];
  }
}

// API: 获取所有性能数据
app.get('/api/perf-data', (req, res) => {
  const data = getAllPerfData();
  res.json(data);
});

// API: 获取单个文件数据
app.get('/api/perf-data/:filename', (req, res) => {
  try {
    const filePath = path.join(PERF_RESULTS_PATH, req.params.filename);
    if (!fs.existsSync(filePath)) {
      return res.status(404).json({ error: 'File not found' });
    }
    
    const content = fs.readFileSync(filePath, 'utf-8');
    const jsonData = JSON.parse(content);
    res.json(jsonData);
  } catch (error) {
    res.status(500).json({ error: error.message });
  }
});

// API: 获取统计信息
app.get('/api/stats', (req, res) => {
  const data = getAllPerfData();
  
  const stats = {
    totalRuns: data.length,
    latestRun: data[0]?.timestamp || null,
    categories: new Set(),
    measurements: {}
  };

  data.forEach(item => {
    item.data.Measurements?.forEach(m => {
      stats.categories.add(m.Category);
      
      if (!stats.measurements[m.Name]) {
        stats.measurements[m.Name] = {
          name: m.Name,
          unit: m.Unit,
          category: m.Category,
          values: [],
          min: Infinity,
          max: -Infinity,
          avg: 0
        };
      }
      
      const measurement = stats.measurements[m.Name];
      measurement.values.push({
        value: m.Value,
        timestamp: item.timestamp
      });
      measurement.min = Math.min(measurement.min, m.Value);
      measurement.max = Math.max(measurement.max, m.Value);
    });
  });

  // 计算平均值
  Object.values(stats.measurements).forEach(m => {
    m.avg = m.values.reduce((sum, v) => sum + v.value, 0) / m.values.length;
  });

  stats.categories = Array.from(stats.categories);

  res.json(stats);
});

// 启动 HTTP 服务器
const server = app.listen(PORT, () => {
  console.log(`🚀 Server running at http://localhost:${PORT}`);
  console.log(`📊 Monitoring: ${PERF_RESULTS_PATH}`);
});

// 创建 WebSocket 服务器
const wss = new WebSocketServer({ server });

// 监听文件变化
const watcher = chokidar.watch(PERF_RESULTS_PATH, {
  ignored: /(^|[\/\\])\../, // 忽略隐藏文件
  persistent: true,
  ignoreInitial: true
});

// 广播消息给所有连接的客户端
function broadcast(message) {
  wss.clients.forEach(client => {
    if (client.readyState === 1) { // WebSocket.OPEN
      client.send(JSON.stringify(message));
    }
  });
}

// 文件变化处理
watcher
  .on('add', filePath => {
    console.log(`📄 New file detected: ${path.basename(filePath)}`);
    try {
      const content = fs.readFileSync(filePath, 'utf-8');
      const jsonData = JSON.parse(content);
      broadcast({
        type: 'new-file',
        filename: path.basename(filePath),
        data: jsonData
      });
    } catch (error) {
      console.error('Error reading new file:', error);
    }
  })
  .on('change', filePath => {
    console.log(`📝 File changed: ${path.basename(filePath)}`);
    try {
      const content = fs.readFileSync(filePath, 'utf-8');
      const jsonData = JSON.parse(content);
      broadcast({
        type: 'file-changed',
        filename: path.basename(filePath),
        data: jsonData
      });
    } catch (error) {
      console.error('Error reading changed file:', error);
    }
  })
  .on('unlink', filePath => {
    console.log(`🗑️  File removed: ${path.basename(filePath)}`);
    broadcast({
      type: 'file-removed',
      filename: path.basename(filePath)
    });
  })
  .on('error', error => {
    console.error('Watcher error:', error);
  });

// WebSocket 连接处理
wss.on('connection', ws => {
  console.log('🔌 New WebSocket client connected');
  
  // 发送当前所有数据
  ws.send(JSON.stringify({
    type: 'initial-data',
    data: getAllPerfData()
  }));

  ws.on('close', () => {
    console.log('🔌 WebSocket client disconnected');
  });
});

console.log('👀 File watcher started');
