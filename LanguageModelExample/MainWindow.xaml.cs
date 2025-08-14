using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Text;
using Microsoft.Windows.AI.Text.Experimental;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.ApplicationModel.DataTransfer;

namespace LanguageModelExample
{
    public sealed partial class MainWindow : Window
    {
        private LanguageModel? _languageModel;
        private TextSummarizer? _textSummarizer;
        private TextRewriter? _textRewriter;
        private TextToTableConverter? _textToTableConverter;
        private ILogger? _logger;
        private int _currentExampleIndex = 0;

        public MainWindow()
        {
            this.InitializeComponent();
            this.Title = "LanguageModel 功能示例 - Visual Studio 优化版";
            
            // 设置窗口大小
            this.SetWindowSize(900, 1000);
            
            // 绑定事件
            BasicGenerateButton.Click += BasicGenerateButton_Click;
            SummaryButton.Click += SummaryButton_Click;
            RewriteButton.Click += RewriteButton_Click;
            TableButton.Click += TableButton_Click;
            
            // 绑定新的事件
            LoadBasicPromptButton.Click += LoadBasicPromptButton_Click;
            LoadSummaryButton.Click += LoadSummaryButton_Click;
            LoadRewriteButton.Click += LoadRewriteButton_Click;
            LoadTableButton.Click += LoadTableButton_Click;
            LoadAllExamplesButton.Click += LoadAllExamplesButton_Click;
            ClearBasicButton.Click += ClearBasicButton_Click;
            ClearSummaryButton.Click += ClearSummaryButton_Click;
            ClearRewriteButton.Click += ClearRewriteButton_Click;
            ClearTableButton.Click += ClearTableButton_Click;
            ClearLogButton.Click += ClearLogButton_Click;
            CopyLogButton.Click += CopyLogButton_Click;
            
            // 初始化日志
            InitializeLogger();
            
            // 加载示例数据
            LoadSampleData();
            
            // 初始化AI模型
            _ = InitializeAIModelAsync();
        }

        private void InitializeLogger()
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddDebug();
                builder.SetMinimumLevel(LogLevel.Debug);
                // 添加自定义日志提供程序，将日志输出到状态栏
                builder.AddProvider(new StatusBarLoggerProvider(this));
            });
            _logger = loggerFactory.CreateLogger<MainWindow>();
        }

        private async Task InitializeAIModelAsync()
        {
            try
            {
                // 步骤1: 开始初始化
                UpdateStatus("🚀 开始初始化AI模型...");
                ProgressRing.IsActive = true;
                _logger?.LogInformation("=== AI模型初始化开始 ===");
                AddLogMessage("🚀 开始初始化AI模型...");
                
                // 步骤2: 检查AI功能状态
                AddLogMessage("📋 步骤1: 检查AI功能状态...");
                var readyState = LanguageModel.GetReadyState();
                _logger?.LogInformation("AI功能状态检查结果: {ReadyState}", readyState);
                AddLogMessage($"📋 AI功能状态: {readyState}");
                
                if (readyState is AIFeatureReadyState.Ready or AIFeatureReadyState.NotReady)
                {
                    if (readyState == AIFeatureReadyState.NotReady)
                    {
                        // 步骤3: 准备AI功能
                        UpdateStatus("⚙️ 正在准备AI功能...");
                        AddLogMessage("⚙️ 步骤2: AI功能未就绪，开始准备...");
                        _logger?.LogInformation("开始准备AI功能...");
                        
                        var operation = await LanguageModel.EnsureReadyAsync();
                        AddLogMessage($"⚙️ AI功能准备操作状态: {operation.Status}");
                        
                        if (operation.Status != AIFeatureReadyResultState.Success)
                        {
                            UpdateStatus("❌ AI功能初始化失败");
                            AddLogMessage($"❌ AI功能准备失败，状态: {operation.Status}");
                            _logger?.LogError("AI功能初始化失败: {Status}", operation.Status);
                            ShowError("Phi-Silica 不可用");
                            return;
                        }
                        
                        AddLogMessage("✅ AI功能准备完成");
                        _logger?.LogInformation("AI功能准备完成");
                    }
                    else
                    {
                        AddLogMessage("✅ AI功能已就绪，无需额外准备");
                    }

                    // 步骤4: 创建语言模型
                    UpdateStatus("🤖 正在创建语言模型...");
                    AddLogMessage("🤖 步骤3: 创建语言模型...");
                    _logger?.LogInformation("开始创建语言模型...");
                    
                    _languageModel = await LanguageModel.CreateAsync();
                    if (_languageModel == null)
                    {
                        UpdateStatus("❌ 无法创建语言模型");
                        AddLogMessage("❌ 语言模型创建失败");
                        _logger?.LogError("无法创建语言模型");
                        ShowError("Phi-Silica 不可用");
                        return;
                    }
                    
                    AddLogMessage("✅ 语言模型创建成功");
                    _logger?.LogInformation("语言模型创建成功");

                    // 步骤5: 初始化各种功能
                    UpdateStatus("🔧 正在初始化AI功能模块...");
                    AddLogMessage("🔧 步骤4: 初始化AI功能模块...");
                    
                    AddLogMessage("  - 初始化文本摘要器...");
                    _textSummarizer = new TextSummarizer(_languageModel);
                    AddLogMessage("  ✅ 文本摘要器初始化完成");
                    
                    AddLogMessage("  - 初始化文本重写器...");
                    _textRewriter = new TextRewriter(_languageModel);
                    AddLogMessage("  ✅ 文本重写器初始化完成");
                    
                    AddLogMessage("  - 初始化表格转换器...");
                    _textToTableConverter = new TextToTableConverter(_languageModel);
                    AddLogMessage("  ✅ 表格转换器初始化完成");

                    // 步骤6: 完成初始化
                    UpdateStatus("🎉 AI模型初始化完成，可以使用所有功能");
                    AddLogMessage("🎉 步骤5: AI模型初始化完成！");
                    AddLogMessage("🎯 所有功能模块已就绪:");
                    AddLogMessage("  ✅ 基础文本生成");
                    AddLogMessage("  ✅ 文本摘要");
                    AddLogMessage("  ✅ 文本重写");
                    AddLogMessage("  ✅ 文本转表格");
                    
                    _logger?.LogInformation("AI模型初始化完成");
                    EnableAllButtons(true);
                }
                else
                {
                    // 步骤7: 处理不可用状态
                    var msg = readyState == AIFeatureReadyState.DisabledByUser
                        ? "用户已禁用"
                        : "系统不支持";
                    
                    UpdateStatus($"❌ AI功能不可用: {msg}");
                    AddLogMessage($"❌ AI功能不可用: {msg}");
                    AddLogMessage($"📋 状态详情: {readyState}");
                    
                    _logger?.LogWarning("AI功能不可用: {Message}", msg);
                    ShowError($"Phi-Silica 不可用: {msg}");
                }
            }
            catch (Exception ex)
            {
                // 步骤8: 异常处理
                UpdateStatus($"❌ 初始化失败: {ex.Message}");
                AddLogMessage($"❌ 初始化过程中发生异常:");
                AddLogMessage($"  📋 异常类型: {ex.GetType().Name}");
                AddLogMessage($"  📋 异常消息: {ex.Message}");
                AddLogMessage($"  📋 异常消息: {ex.ToString()}");
                if (ex.InnerException != null)
                {
                    AddLogMessage($"  📋 内部异常: {ex.InnerException.Message}");
                }
                
                _logger?.LogError(ex, "AI模型初始化失败");
                ShowError($"初始化失败: {ex.Message}");
            }
            finally
            {
                // 步骤9: 清理工作
                ProgressRing.IsActive = false;
                AddLogMessage("🔄 初始化流程结束");
                _logger?.LogInformation("=== AI模型初始化流程结束 ===");
            }
        }

        // 示例数据加载事件
        private void LoadBasicPromptButton_Click(object sender, RoutedEventArgs e)
        {
            var prompts = SampleData.Prompts.BasicPrompts;
            BasicPromptTextBox.Text = prompts[_currentExampleIndex % prompts.Length];
            _logger?.LogInformation("加载文本生成示例: {Index}", _currentExampleIndex % prompts.Length);
        }

        private void LoadSummaryButton_Click(object sender, RoutedEventArgs e)
        {
            var texts = SampleData.Prompts.SummaryTexts;
            SummaryInputTextBox.Text = texts[_currentExampleIndex % texts.Length];
            _logger?.LogInformation("加载摘要示例: {Index}", _currentExampleIndex % texts.Length);
        }

        private void LoadRewriteButton_Click(object sender, RoutedEventArgs e)
        {
            var texts = SampleData.Prompts.RewriteTexts;
            RewriteInputTextBox.Text = texts[_currentExampleIndex % texts.Length];
            _logger?.LogInformation("加载重写示例: {Index}", _currentExampleIndex % texts.Length);
        }

        private void LoadTableButton_Click(object sender, RoutedEventArgs e)
        {
            var texts = SampleData.Prompts.TableTexts;
            TableInputTextBox.Text = texts[_currentExampleIndex % texts.Length];
            _logger?.LogInformation("加载表格示例: {Index}", _currentExampleIndex % texts.Length);
        }

        private void LoadAllExamplesButton_Click(object sender, RoutedEventArgs e)
        {
            LoadBasicPromptButton_Click(sender, e);
            LoadSummaryButton_Click(sender, e);
            LoadRewriteButton_Click(sender, e);
            LoadTableButton_Click(sender, e);
            _currentExampleIndex = (_currentExampleIndex + 1) % 5; // 循环切换示例
            _logger?.LogInformation("加载所有示例，切换到索引: {Index}", _currentExampleIndex);
        }

        // 清空功能事件
        private void ClearBasicButton_Click(object sender, RoutedEventArgs e)
        {
            BasicPromptTextBox.Text = string.Empty;
            BasicResultTextBox.Text = string.Empty;
        }

        private void ClearSummaryButton_Click(object sender, RoutedEventArgs e)
        {
            SummaryInputTextBox.Text = string.Empty;
            SummaryResultTextBox.Text = string.Empty;
        }

        private void ClearRewriteButton_Click(object sender, RoutedEventArgs e)
        {
            RewriteInputTextBox.Text = string.Empty;
            RewriteResultTextBox.Text = string.Empty;
        }

        private void ClearTableButton_Click(object sender, RoutedEventArgs e)
        {
            TableInputTextBox.Text = string.Empty;
            TableResultTextBox.Text = string.Empty;
        }

        // 调试信息刷新
        private void RefreshDebugInfoButton_Click(object sender, RoutedEventArgs e)
        {
            var debugInfo = $@"调试信息 - {DateTime.Now:yyyy-MM-dd HH:mm:ss}
AI模型状态: {(_languageModel != null ? "已初始化" : "未初始化")}
摘要器状态: {(_textSummarizer != null ? "已初始化" : "未初始化")}
重写器状态: {(_textRewriter != null ? "已初始化" : "未初始化")}
表格转换器状态: {(_textToTableConverter != null ? "已初始化" : "未初始化")}
当前示例索引: {_currentExampleIndex}
.NET版本: {Environment.Version}
操作系统: {Environment.OSVersion}
工作目录: {Environment.CurrentDirectory}
进程ID: {Process.GetCurrentProcess().Id}";

            // 将调试信息添加到日志区域
            AddLogMessage("调试信息已刷新");
            AddLogMessage(debugInfo);
            _logger?.LogInformation("刷新调试信息");
        }

        private async void BasicGenerateButton_Click(object sender, RoutedEventArgs e)
        {
            if (_languageModel == null)
            {
                ShowError("AI模型未初始化");
                return;
            }

            var prompt = BasicPromptTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(prompt))
            {
                ShowError("请输入提示词");
                return;
            }

            try
            {
                BasicGenerateButton.IsEnabled = false;
                ProgressRing.IsActive = true;
                UpdateStatus("正在生成文本...");
                _logger?.LogInformation("开始生成文本，提示词: {Prompt}", prompt);

                var result = await _languageModel.GenerateResponseAsync(prompt);
                BasicResultTextBox.Text = result.Text;
                UpdateStatus("文本生成完成");
                _logger?.LogInformation("文本生成完成，长度: {Length}", result.Text?.Length ?? 0);
            }
            catch (Exception ex)
            {
                ShowError($"生成失败: {ex.Message}");
                UpdateStatus($"生成失败: {ex.Message}");
                _logger?.LogError(ex, "文本生成失败");
            }
            finally
            {
                BasicGenerateButton.IsEnabled = true;
                ProgressRing.IsActive = false;
            }
        }

        private async void SummaryButton_Click(object sender, RoutedEventArgs e)
        {
            if (_textSummarizer == null)
            {
                ShowError("AI模型未初始化");
                return;
            }

            var input = SummaryInputTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(input))
            {
                ShowError("请输入要摘要的文本");
                return;
            }

            try
            {
                SummaryButton.IsEnabled = false;
                ProgressRing.IsActive = true;
                UpdateStatus("正在生成摘要...");
                _logger?.LogInformation("开始生成摘要，输入长度: {Length}", input.Length);

                var result = await _textSummarizer.SummarizeParagraphAsync(input);
                SummaryResultTextBox.Text = result.Text;
                UpdateStatus("摘要生成完成");
                _logger?.LogInformation("摘要生成完成，输出长度: {Length}", result.Text?.Length ?? 0);
            }
            catch (Exception ex)
            {
                ShowError($"摘要生成失败: {ex.Message}");
                UpdateStatus($"摘要生成失败: {ex.Message}");
                _logger?.LogError(ex, "摘要生成失败");
            }
            finally
            {
                SummaryButton.IsEnabled = true;
                ProgressRing.IsActive = false;
            }
        }

        private async void RewriteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_textRewriter == null)
            {
                ShowError("AI模型未初始化");
                return;
            }

            var input = RewriteInputTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(input))
            {
                ShowError("请输入要重写的文本");
                return;
            }

            try
            {
                RewriteButton.IsEnabled = false;
                ProgressRing.IsActive = true;
                UpdateStatus("正在重写文本...");
                _logger?.LogInformation("开始重写文本，输入长度: {Length}", input.Length);

                var result = await _textRewriter.RewriteAsync(input);
                RewriteResultTextBox.Text = result.Text;
                UpdateStatus("文本重写完成");
                _logger?.LogInformation("文本重写完成，输出长度: {Length}", result.Text?.Length ?? 0);
            }
            catch (Exception ex)
            {
                ShowError($"文本重写失败: {ex.Message}");
                UpdateStatus($"文本重写失败: {ex.Message}");
                _logger?.LogError(ex, "文本重写失败");
            }
            finally
            {
                RewriteButton.IsEnabled = true;
                ProgressRing.IsActive = false;
            }
        }

        private async void TableButton_Click(object sender, RoutedEventArgs e)
        {
            if (_textToTableConverter == null)
            {
                ShowError("AI模型未初始化");
                return;
            }

            var input = TableInputTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(input))
            {
                ShowError("请输入要转换为表格的文本");
                return;
            }

            try
            {
                TableButton.IsEnabled = false;
                ProgressRing.IsActive = true;
                UpdateStatus("正在转换为表格...");
                _logger?.LogInformation("开始转换为表格，输入长度: {Length}", input.Length);

                var result = await _textToTableConverter.ConvertAsync(input);
                TableResultTextBox.Text = result.ToString();
                UpdateStatus("表格转换完成");
                _logger?.LogInformation("表格转换完成，输出长度: {Length}", result.ToString()?.Length ?? 0);
            }
            catch (Exception ex)
            {
                ShowError($"表格转换失败: {ex.Message}");
                UpdateStatus($"表格转换失败: {ex.Message}");
                _logger?.LogError(ex, "表格转换失败");
            }
            finally
            {
                TableButton.IsEnabled = true;
                ProgressRing.IsActive = false;
            }
        }

        public void UpdateStatus(string message)
        {
            StatusTextBlock.Text = message;
            AddLogMessage($"状态: {message}");
            _logger?.LogInformation("状态更新: {Message}", message);
        }

        public void AddLogMessage(string message)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("HH:mm:ss");
                var logEntry = $"[{timestamp}] {message}";
                
                // 在UI线程中更新日志
                DispatcherQueue.TryEnqueue(() =>
                {
                    if (LogTextBlock.Text == "日志将在这里显示...")
                    {
                        LogTextBlock.Text = logEntry;
                    }
                    else
                    {
                        LogTextBlock.Text += $"\n{logEntry}";
                    }
                    
                    // 自动滚动到底部
                    var scrollViewer = LogTextBlock.Parent as ScrollViewer;
                    if (scrollViewer != null)
                    {
                        scrollViewer.ChangeView(null, scrollViewer.ScrollableHeight, null);
                    }
                });
            }
            catch
            {
                // 忽略日志更新过程中的任何错误
            }
        }

        private void ShowError(string message)
        {
            // 使用状态栏显示错误信息，避免 ContentDialog 冲突
            UpdateStatus($"❌ 错误: {message}");
            _logger?.LogError("显示错误: {Message}", message);
        }

        private void EnableAllButtons(bool enable)
        {
            BasicGenerateButton.IsEnabled = enable;
            SummaryButton.IsEnabled = enable;
            RewriteButton.IsEnabled = enable;
            TableButton.IsEnabled = enable;
            
            // 示例数据按钮始终可用
            LoadBasicPromptButton.IsEnabled = true;
            LoadSummaryButton.IsEnabled = true;
            LoadRewriteButton.IsEnabled = true;
            LoadTableButton.IsEnabled = true;
            LoadAllExamplesButton.IsEnabled = true;
        }

        private void LoadSampleData()
        {
            // 为各个输入框添加示例数据提示
            BasicPromptTextBox.Text = SampleData.Prompts.BasicPrompts[0];
            SummaryInputTextBox.Text = SampleData.Prompts.SummaryTexts[0];
            RewriteInputTextBox.Text = SampleData.Prompts.RewriteTexts[0];
            TableInputTextBox.Text = SampleData.Prompts.TableTexts[0];
            _logger?.LogInformation("加载初始示例数据");
        }

        private void SetWindowSize(int width, int height)
        {
            var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            if (appWindow != null)
            {
                appWindow.Resize(new Windows.Graphics.SizeInt32 { Width = width, Height = height });
            }
        }

        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            LogTextBlock.Text = "日志已清空";
            AddLogMessage("日志已清空");
        }

        private async void CopyLogButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dataPackage = new DataPackage();
                dataPackage.SetText(LogTextBlock.Text);
                Clipboard.SetContent(dataPackage);
                
                UpdateStatus("日志已复制到剪贴板");
                AddLogMessage("日志已复制到剪贴板");
            }
            catch (Exception ex)
            {
                ShowError($"复制日志失败: {ex.Message}");
            }
        }
    }

    // 自定义日志提供程序，将日志输出到状态栏
    public class StatusBarLoggerProvider : ILoggerProvider
    {
        private readonly MainWindow _mainWindow;

        public StatusBarLoggerProvider(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new StatusBarLogger(_mainWindow);
        }

        public void Dispose()
        {
            // 无需特殊清理
        }
    }

    // 自定义日志记录器
    public class StatusBarLogger : ILogger
    {
        private readonly MainWindow _mainWindow;

        public StatusBarLogger(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true; // 启用所有日志级别
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            try
            {
                var message = formatter(state, exception);
                var logMessage = $"[{logLevel}] {message}";
                
                if (exception != null)
                {
                    logMessage += $" | 异常: {exception.Message}";
                }

                // 在UI线程中更新日志区域，而不是状态栏
                _mainWindow.DispatcherQueue.TryEnqueue(() =>
                {
                    _mainWindow.AddLogMessage(logMessage);
                });
            }
            catch
            {
                // 忽略日志记录过程中的任何错误
            }
        }
    }
} 