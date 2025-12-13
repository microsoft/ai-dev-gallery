# FoundryLocal SSE 串流問題調試指南

## 問題描述
使用 FoundryLocal SDK 時，串流通信出現 SSE 錯誤：
```
The response ended prematurely. (ResponseEnded)
```

## 已修復的問題

### 1. ConnectionClose Header
**問題**：設置 `httpClient.DefaultRequestHeaders.ConnectionClose = true` 導致連接提前關閉  
**修復**：移除此設置，讓 HTTP 連接保持 Keep-Alive

### 2. LoggingHttpMessageHandler
**問題**：自定義 HttpMessageHandler 可能干擾串流處理  
**修復**：使用標準 HttpClientHandler

## FoundryLocal SDK 使用方式對比

### 方式 A：直接使用 SDK 的 OpenAIChatClient（推薦）
```csharp
// 不需要啟動 web service
var model = await catalog.GetModelAsync("qwen2.5-0.5b");
await model.LoadAsync();

var chatClient = await model.GetChatClientAsync();
var streamingResponse = chatClient.CompleteChatStreamingAsync(messages, ct);

await foreach (var chunk in streamingResponse)
{
    Console.Write(chunk.Choices[0].Message.Content);
}
```

**優點**：
- 直接使用 SDK，無需 web service
- SDK 自己處理所有串流細節
- 更簡單，更可靠

**缺點**：
- 返回的是 `OpenAIChatClient`（Betalgo.Ranul.OpenAI 類型）
- 不是 `Microsoft.Extensions.AI.IChatClient`
- 需要適配器層

### 方式 B：通過 Web Service 使用 OpenAI SDK（當前實作）
```csharp
// 需要啟動 web service
await mgr.StartWebServiceAsync();

var httpClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
var client = new OpenAIClient(new ApiKeyCredential("none"), new OpenAIClientOptions
{
    Endpoint = new Uri($"{serviceUrl}/v1"),
    Transport = new HttpClientPipelineTransport(httpClient)
}).GetChatClient(modelId).AsIChatClient();

await foreach (var messagePart in client.GetStreamingResponseAsync(...))
{
    Console.Write(messagePart);
}
```

**優點**：
- 可以使用標準 OpenAI SDK
- 返回 `Microsoft.Extensions.AI.IChatClient`
- 符合 AI-Dev-Gallery 的架構

**缺點**：
- 需要管理 web service 生命週期
- 多一層網絡通信
- SSE 串流可能更容易出問題

## 如果問題仍存在的調試步驟

### 1. 檢查 FoundryLocal 服務狀態
```csharp
Debug.WriteLine($"Service running: {_manager.IsServiceRunning}");
Debug.WriteLine($"Service URLs: {string.Join(", ", _manager.Urls ?? [])}");
```

### 2. 測試非串流請求
先測試非串流的請求是否正常：
```csharp
var response = await chatClient.CompleteAsync(messages);
Debug.WriteLine($"Non-streaming response: {response.Message.Text}");
```

### 3. 直接測試 HTTP 端點
```powershell
curl http://127.0.0.1:PORT/v1/models
```

### 4. 比較同步 vs 異步串流
官方範例使用同步串流，可以測試是否有差異：
```csharp
// 同步版本（官方範例風格）
var completionUpdates = chatClient.CompleteChatStreaming("Test");
foreach (var completionUpdate in completionUpdates)
{
    if (completionUpdate.ContentUpdate.Count > 0)
    {
        Console.Write(completionUpdate.ContentUpdate[0].Text);
    }
}
```

### 5. 增加詳細日誌
在 FoundryClient 創建時啟用 Debug 日誌：
```csharp
var config = new Configuration
{
    AppName = "AIDevGallery",
    LogLevel = Microsoft.AI.Foundry.Local.LogLevel.Debug,  // 詳細日誌
    Web = new Configuration.WebService
    {
        Urls = "http://127.0.0.1:0"
    }
};
```

## 可能的根本原因

1. **網絡層問題**
   - HttpClient 配置不當
   - 超時設置
   - 連接池管理

2. **FoundryLocal Service 問題**
   - Service 未正確啟動
   - Model 未正確加載
   - 端口衝突

3. **OpenAI SDK SSE 解析問題**
   - FoundryLocal 的 SSE 格式與 OpenAI SDK 預期不完全兼容
   - Chunked encoding 處理問題

## 下一步

1. ✅ 已移除 ConnectionClose header
2. ✅ 已移除 LoggingHttpMessageHandler
3. ⏳ 測試修改後是否解決問題
4. 如問題仍存在，考慮切換到方式 A（直接使用 SDK 的 OpenAIChatClient）
