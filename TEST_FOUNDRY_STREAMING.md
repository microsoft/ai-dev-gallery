# FoundryLocal 串流問題根本原因分析與解決方案

## 問題診斷

### 官方範例的配置
- **OpenAI SDK**: `2.5.0`
- **使用方式**: 同步串流 `CompleteChatStreaming`
- **結果**: ✅ 正常工作

```csharp
var chatClient = client.GetChatClient(model.Id);
var completionUpdates = chatClient.CompleteChatStreaming("Why is the sky blue?");

foreach (var completionUpdate in completionUpdates)
{
    if (completionUpdate.ContentUpdate.Count > 0)
    {
        Console.Write(completionUpdate.ContentUpdate[0].Text);
    }
}
```

### AI-Dev-Gallery 原配置（有問題）
- **OpenAI SDK**: 通過 `Microsoft.Extensions.AI.OpenAI` `9.9.1-preview`
- **使用方式**: 異步串流 `GetStreamingResponseAsync` (透過 `.AsIChatClient()`)
- **結果**: ❌ SSE 錯誤 "The response ended prematurely"

```csharp
var client = new OpenAIClient(...).GetChatClient(modelId).AsIChatClient();

await foreach (var messagePart in client.GetStreamingResponseAsync(...))
{
    // 在這裡失敗 - SSE 錯誤
}
```

## 根本原因

**Microsoft.Extensions.AI.OpenAI 的異步 SSE 包裝層與 FoundryLocal 的 SSE 實作不兼容。**

技術細節：
1. `Microsoft.Extensions.AI.OpenAI` 的 `.AsIChatClient()` 使用異步 SSE 解析器 (`System.Net.ServerSentEvents.SseParser`)
2. FoundryLocal 的 SSE 實作在 chunked encoding 或響應格式上可能有細微差異
3. OpenAI SDK 的同步版本 (`CompleteChatStreaming`) 使用不同的 HTTP 處理路徑，更加寬容
4. 異步版本在檢測到連接提前結束時會拋出 `ResponseEnded` 錯誤

## 已實施的解決方案 ✅

### 創建自定義適配器

新增文件：`AIDevGallery/ExternalModelUtils/FoundryLocal/FoundryLocalChatClientAdapter.cs`

**適配器的工作原理：**

1. **實作 `Microsoft.Extensions.AI.IChatClient` 接口** - 保持與 AI-Dev-Gallery 架構的兼容性
2. **內部使用 OpenAI SDK 的同步串流 API** - `CompleteChatStreaming`（已驗證可用）
3. **將同步串流包裝成異步** - 使用 `yield return` 和 `await Task.Yield()`
4. **消息格式轉換** - 在 `Microsoft.Extensions.AI.ChatMessage` 和 `OpenAI.Chat.ChatMessage` 之間轉換

**核心代碼：**
```csharp
public async IAsyncEnumerable<StreamingChatCompletionUpdate> CompleteStreamingAsync(
    IList<ChatMessage> chatMessages,
    ChatOptions? options = null,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    var openAIMessages = ConvertToOpenAIMessages(chatMessages);
    
    // 使用同步 API - 這是關鍵！
    var completionUpdates = _chatClient.CompleteChatStreaming(openAIMessages, ...);
    
    await Task.Yield();
    
    foreach (var update in completionUpdates)
    {
        cancellationToken.ThrowIfCancellationRequested();
        yield return ConvertToStreamingUpdate(update);
    }
}
```

### 修改 FoundryLocalModelProvider

修改 `GetIChatClient` 方法使用新適配器：

```csharp
public IChatClient? GetIChatClient(string url)
{
    // ... 準備工作 ...
    
    var (serviceUrl, modelId) = preparedInfo.Value;
    
    // 使用自定義適配器而不是 .AsIChatClient()
    return new FoundryLocal.FoundryLocalChatClientAdapter(serviceUrl, modelId);
}
```

## 優點

✅ **保持架構兼容性** - 仍然返回 `IChatClient`，無需修改調用代碼  
✅ **使用已驗證的 API** - 使用 FoundryLocal 官方範例中的同步串流方式  
✅ **避免 SSE 問題** - 繞過 `Microsoft.Extensions.AI.OpenAI` 的異步 SSE 解析器  
✅ **完整的功能支持** - 支持串流和非串流、選項參數、取消令牌等  
✅ **詳細的日誌** - 包含調試輸出以便追蹤問題  

## 測試步驟

1. **編譯項目**
   ```powershell
   cd c:\Users\yuanwei\repo\AI-Dev-Gallery
   dotnet build
   ```

2. **運行應用程式**
   - 選擇 FoundryLocal 模型
   - 嘗試 Generate 範例
   - 觀察 Debug 輸出

3. **預期結果**
   - ✅ 串流應該正常工作，無 SSE 錯誤
   - ✅ Debug 輸出應顯示：
     ```
     [FoundryLocalAdapter] CompleteStreamingAsync called
     [FoundryLocalAdapter] Starting to enumerate streaming updates
     [FoundryLocalAdapter] Received first streaming update
     [FoundryLocalAdapter] Streaming completed. Total updates: XXX
     ```

## 如果問題仍存在

如果適配器仍有問題，可以檢查：

1. **查看 Debug 輸出** - 確認適配器是否被正確創建和調用
2. **驗證 FoundryLocal 服務** - 使用 curl 測試端點：
   ```powershell
   curl http://127.0.0.1:PORT/v1/models
   ```
3. **測試非串流請求** - 先測試 `CompleteAsync` 是否工作
4. **檢查 OpenAI SDK 版本** - 確保與 FoundryLocal 範例使用相同版本

## 技術背景

這個問題揭示了一個重要的兼容性問題：

- **抽象層的代價**：`Microsoft.Extensions.AI` 提供了很好的統一抽象，但其 OpenAI 適配器的異步實作可能與某些 OpenAI 兼容服務（如 FoundryLocal）不完全兼容
- **同步 vs 異步**：OpenAI SDK 提供了兩種串流方式，它們在底層 HTTP 處理上可能有差異
- **SSE 實作差異**：Server-Sent Events 雖然有標準，但實作細節（如 chunked encoding、header 格式）可能導致兼容性問題

## 相關文件

- FoundryLocal SDK 範例：`Foundry-Local/samples/cs/GettingStarted/`
- OpenAI SDK 文檔：https://github.com/openai/openai-dotnet
- Microsoft.Extensions.AI：https://github.com/dotnet/extensions

