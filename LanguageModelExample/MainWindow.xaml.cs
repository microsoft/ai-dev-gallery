using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Text;
using Microsoft.Windows.AI.Text.Experimental;
using System;
using System.Threading.Tasks;

namespace LanguageModelExample
{
    public sealed partial class MainWindow : Window
    {
        private LanguageModel? _languageModel;
        private TextSummarizer? _textSummarizer;
        private TextRewriter? _textRewriter;
        private TextToTableConverter? _textToTableConverter;

        public MainWindow()
        {
            this.InitializeComponent();
            this.Title = "LanguageModel 功能示例";
            
            // 设置窗口大小
            this.SetWindowSize(800, 900);
            
            // 绑定事件
            BasicGenerateButton.Click += BasicGenerateButton_Click;
            SummaryButton.Click += SummaryButton_Click;
            RewriteButton.Click += RewriteButton_Click;
            TableButton.Click += TableButton_Click;
            
            // 加载示例数据
            LoadSampleData();
            
            // 初始化AI模型
            _ = InitializeAIModelAsync();
        }

        private async Task InitializeAIModelAsync()
        {
            try
            {
                UpdateStatus("正在初始化AI模型...");
                ProgressRing.IsActive = true;

                // 检查AI功能状态
                var readyState = LanguageModel.GetReadyState();
                if (readyState is AIFeatureReadyState.Ready or AIFeatureReadyState.NotReady)
                {
                    if (readyState == AIFeatureReadyState.NotReady)
                    {
                        UpdateStatus("正在准备AI功能...");
                        var operation = await LanguageModel.EnsureReadyAsync();
                        
                        if (operation.Status != AIFeatureReadyResultState.Success)
                        {
                            UpdateStatus("AI功能初始化失败");
                            ShowError("Phi-Silica 不可用");
                            return;
                        }
                    }

                    // 创建语言模型
                    _languageModel = await LanguageModel.CreateAsync();
                    if (_languageModel == null)
                    {
                        UpdateStatus("无法创建语言模型");
                        ShowError("Phi-Silica 不可用");
                        return;
                    }

                    // 初始化各种功能
                    _textSummarizer = new TextSummarizer(_languageModel);
                    _textRewriter = new TextRewriter(_languageModel);
                    _textToTableConverter = new TextToTableConverter(_languageModel);

                    UpdateStatus("AI模型初始化完成，可以使用所有功能");
                    EnableAllButtons(true);
                }
                else
                {
                    var msg = readyState == AIFeatureReadyState.DisabledByUser
                        ? "用户已禁用"
                        : "系统不支持";
                    UpdateStatus($"AI功能不可用: {msg}");
                    ShowError($"Phi-Silica 不可用: {msg}");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"初始化失败: {ex.Message}");
                ShowError($"初始化失败: {ex.Message}");
            }
            finally
            {
                ProgressRing.IsActive = false;
            }
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

                var result = await _languageModel.GenerateResponseAsync(prompt);
                BasicResultTextBox.Text = result.Text;
                UpdateStatus("文本生成完成");
            }
            catch (Exception ex)
            {
                ShowError($"生成失败: {ex.Message}");
                UpdateStatus($"生成失败: {ex.Message}");
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

                var result = await _textSummarizer.SummarizeParagraphAsync(input);
                SummaryResultTextBox.Text = result.Text;
                UpdateStatus("摘要生成完成");
            }
            catch (Exception ex)
            {
                ShowError($"摘要生成失败: {ex.Message}");
                UpdateStatus($"摘要生成失败: {ex.Message}");
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

                var result = await _textRewriter.RewriteAsync(input);
                RewriteResultTextBox.Text = result.Text;
                UpdateStatus("文本重写完成");
            }
            catch (Exception ex)
            {
                ShowError($"文本重写失败: {ex.Message}");
                UpdateStatus($"文本重写失败: {ex.Message}");
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

                var result = await _textToTableConverter.ConvertAsync(input);
                TableResultTextBox.Text = result.Text;
                UpdateStatus("表格转换完成");
            }
            catch (Exception ex)
            {
                ShowError($"表格转换失败: {ex.Message}");
                UpdateStatus($"表格转换失败: {ex.Message}");
            }
            finally
            {
                TableButton.IsEnabled = true;
                ProgressRing.IsActive = false;
            }
        }

        private void UpdateStatus(string message)
        {
            StatusTextBlock.Text = message;
        }

        private void ShowError(string message)
        {
            var dialog = new ContentDialog
            {
                Title = "错误",
                Content = message,
                CloseButtonText = "确定",
                XamlRoot = this.Content.XamlRoot
            };
            _ = dialog.ShowAsync();
        }

        private void EnableAllButtons(bool enable)
        {
            BasicGenerateButton.IsEnabled = enable;
            SummaryButton.IsEnabled = enable;
            RewriteButton.IsEnabled = enable;
            TableButton.IsEnabled = enable;
        }

        private void LoadSampleData()
        {
            // 为各个输入框添加示例数据提示
            BasicPromptTextBox.Text = SampleData.Prompts.BasicPrompts[0];
            SummaryInputTextBox.Text = SampleData.Prompts.SummaryTexts[0];
            RewriteInputTextBox.Text = SampleData.Prompts.RewriteTexts[0];
            TableInputTextBox.Text = SampleData.Prompts.TableTexts[0];
        }

        private void SetWindowSize(int width, int height)
        {
            var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow.Resize(new Windows.Graphics.SizeInt32 { Width = width, Height = height });
        }
    }
} 