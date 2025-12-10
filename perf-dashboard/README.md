# AI Dev Gallery Performance Dashboard

一个基于 **Vite + Vue 3** 的实时性能监控 Dashboard，用于监控和可视化 AI Dev Gallery 的测试性能数据。

## ✨ 特性

- 🔄 **实时监控** - 使用 WebSocket 实时推送性能数据更新
- 📊 **数据可视化** - 使用 ECharts 展示性能趋势图表，支持线形图和柱状图
- 📁 **无需数据库** - 直接读取 JSON 文件，无需额外配置
- 🎨 **现代化界面** - 清新的配色设计和响应式布局
- 🔍 **详细视图** - 查看每次测试运行的完整信息
- 📈 **统计分析** - 自动计算和展示性能统计数据
- 🔎 **智能过滤** - 按类别、时间范围筛选测试数据
- ⚠️ **性能回归检测** - 自动识别性能下降并高亮显示
- 🆚 **对比模式** - 同时比较多个测试运行的性能差异
- 📊 **分类概览** - 按类别分组展示关键性能指标

## 🚀 快速开始

### 前置要求

- Node.js 16.0 或更高版本
- npm 或 yarn

### 安装

```bash
# 进入项目目录
cd perf-dashboard

# 安装依赖
npm install
```

### 运行

```bash
# 同时启动后端服务和前端开发服务器
npm run dev
```

服务启动后：
- 前端界面: http://localhost:5173
- 后端 API: http://localhost:3000

### 单独运行

```bash
# 仅启动后端服务
npm run server

# 仅启动前端（需要后端已运行）
npm run client
```

### 构建生产版本

```bash
npm run build
```

## 📁 项目结构

```
perf-dashboard/
├── server/
│   └── index.js          # Express 服务器 + WebSocket + 文件监听
├── src/
│   ├── components/
│   │   └── PerformanceChart.vue  # ECharts 图表组件
│   ├── views/
│   │   ├── Dashboard.vue         # 主仪表板页面
│   │   └── Details.vue           # 详情页面
│   ├── App.vue           # 根组件
│   ├── main.js           # 入口文件
│   └── style.css         # 全局样式
├── index.html
├── vite.config.js
└── package.json
```

## 🔧 配置

### 修改监听路径

如果需要修改性能数据文件夹的路径，请编辑 `server/index.js`:

```javascript
const PERF_RESULTS_PATH = path.resolve(__dirname, '你的路径/PerfResults');
```

### 修改端口

- **后端端口**: 在 `server/index.js` 中修改 `PORT` 常量
- **前端端口**: 在 `vite.config.js` 中修改 `server.port`

## 📊 功能说明

### 主仪表板

- **统计卡片**: 显示测试运行总数、最新测试时间、分类数量和指标数量，带有彩色边框和趋势信息
- **智能过滤器**: 
  - 按类别筛选测试数据（PageLoad, AppLifecycle 等）
  - 控制显示数量（最近 5/10/20 次或全部）
  - 启用对比模式同时选择多个测试运行
- **性能回归警告**: 自动检测并突出显示性能下降超过 20% 的指标
- **分类性能概览**: 按类别分组展示所有指标的最新值、平均值、最佳和最差值
- **性能趋势图**: 
  - 支持选择任何指标查看历史趋势
  - 可切换线形图或柱状图显示
  - 显示平均线和统计信息
  - 根据性能好坏自动着色
- **测试运行列表**: 
  - 展示所有测试运行和关键指标
  - 支持多选对比模式
  - 显示每个测试的分类标签
- **实时通知**: 新数据到达时自动提醒

### 详情页面

- **测试信息**: Run ID、时间戳、分支、提交哈希等，带有视觉卡片设计
- **环境信息**: 操作系统、平台、硬件配置
- **性能指标**: 按类别分组展示所有测量数据，带有彩色边框和渐变文字
- **标签展示**: 显示每个指标的相关标签

### 对比页面

- **多运行对比**: 同时比较最多 5 个测试运行
- **元信息对比表**: 并排显示时间戳、分支、配置等信息
- **指标对比表**: 
  - 按类别分组对比所有指标
  - 自动标记最佳值（绿色）和最差值（红色）
  - 显示性能差异百分比
- **可视化对比图**: 使用图表直观展示性能差异

### 实时更新

Dashboard 会自动监听 PerfResults 文件夹的变化：
- ✨ 新文件添加 → 自动加载并显示通知
- 🔄 文件修改 → 自动更新数据
- 🗑️ 文件删除 → 自动从列表移除

## 🛠️ 技术栈

### 前端
- **Vue 3** - 渐进式 JavaScript 框架
- **Vue Router** - 官方路由管理器
- **ECharts** - 强大的数据可视化库
- **Vite** - 下一代前端构建工具

### 后端
- **Express** - 快速的 Node.js Web 框架
- **WebSocket (ws)** - 实时双向通信
- **Chokidar** - 高效的文件系统监听
- **CORS** - 跨域资源共享支持

## 📝 API 端点

### GET /api/perf-data
获取所有性能数据文件

**响应示例:**
```json
[
  {
    "filename": "perf-20251209-034830.json",
    "timestamp": "2025-12-09T03:48:30.078Z",
    "data": { ... }
  }
]
```

### GET /api/perf-data/:filename
获取单个性能数据文件

### GET /api/stats
获取统计信息（总运行次数、分类、指标统计等）

### WebSocket
连接到 `ws://localhost:3000` 接收实时更新

**消息类型:**
- `initial-data` - 初始数据加载
- `new-file` - 新文件添加
- `file-changed` - 文件修改
- `file-removed` - 文件删除

## 🎨 自定义样式

主要样式定义在 `src/style.css` 中，可以修改以下变量来自定义主题：

- 主色调: `#667eea`
- 次要色调: `#764ba2`
- 卡片圆角: `12px`
- 间距: `1.5rem`

## 🤝 开发建议

1. **添加新功能**: 在 `src/components` 中创建新组件
2. **修改路由**: 编辑 `src/main.js` 中的路由配置
3. **添加 API**: 在 `server/index.js` 中添加新的端点
4. **样式修改**: 优先使用组件内的 scoped 样式

## 📄 许可证

此项目是 AI Dev Gallery 项目的一部分。

## 🐛 问题反馈

如遇到问题，请检查：
1. Node.js 版本是否符合要求
2. PerfResults 文件夹路径是否正确
3. 端口 3000 和 5173 是否被占用
4. 依赖是否正确安装

---

Made with ❤️ for AI Dev Gallery
