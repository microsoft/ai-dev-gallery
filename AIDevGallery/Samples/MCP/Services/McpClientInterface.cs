// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.MCP.Services;

/// <summary>
/// MCP 客户端封装器，用于统一管理不同的 MCP 服务器连接
/// </summary>
public class McpClientWrapper : IDisposable
{
    private readonly McpClient _client;
    private readonly string _serverId;
    private bool _disposed;

    public string ServerId => _serverId;

    public McpClientWrapper(McpClient client, string serverId)
    {
        _client = client;
        _serverId = serverId;
    }

    public async Task<IReadOnlyList<McpClientTool>?> ListToolsAsync(CancellationToken cancellationToken = default)
    {
        return await _client.ListToolsAsync();
    }

    public async Task<CallToolResult> CallToolAsync(string toolName, Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        return await _client.CallToolAsync(toolName, arguments);
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        try
        {
            // McpClient实现了IAsyncDisposable，不是IDisposable
            // 在异步上下文中应该使用DisposeAsync，这里可以不处理
        }
        catch
        {
            // 忽略清理错误
        }
        
        _disposed = true;
    }
}

/// <summary>
/// MCP 客户端工厂，用于创建不同类型的 MCP 连接
/// </summary>
public static class McpClientFactory
{
    /// <summary>
    /// 创建系统信息 MCP 客户端
    /// </summary>
    public static async Task<McpClientWrapper?> CreateSystemInfoClientAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            const string systemInfoServerId = "MicrosoftWindows.Client.Core_cw5n1h2txyewy_com.microsoft.windows.ai.mcpServer_systeminfo-mcp-server";
            
            var transportOptions = new StdioClientTransportOptions
            {
                Name = "SystemInfo-MCP-Client",
                Command = "odr.exe",
                Arguments = new[]
                {
                    "mcp",
                    "--proxy",
                    systemInfoServerId
                }
            };

            var transport = new StdioClientTransport(transportOptions);
            var mcpClientOptions = new McpClientOptions();
            var client = await McpClient.CreateAsync(transport, mcpClientOptions);

            return new McpClientWrapper(client, systemInfoServerId);
        }
        catch
        {
            return null; // 连接失败
        }
    }

    /// <summary>
    /// 创建文件操作 MCP 客户端
    /// </summary>
    public static async Task<McpClientWrapper?> CreateFileOperationsClientAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            const string fileServerId = "MicrosoftWindows.Client.Core_cw5n1h2txyewy_com.microsoft.windows.ai.mcpServer_file-mcp-server";
            
            var transportOptions = new StdioClientTransportOptions
            {
                Name = "File-MCP-Client",
                Command = "odr.exe",
                Arguments = new[]
                {
                    "mcp",
                    "--proxy",
                    fileServerId
                }
            };

            var transport = new StdioClientTransport(transportOptions);
            var mcpClientOptions = new McpClientOptions();
            var client = await McpClient.CreateAsync(transport, mcpClientOptions);

            return new McpClientWrapper(client, fileServerId);
        }
        catch
        {
            return null; // 连接失败
        }
    }

    /// <summary>
    /// 创建设置 MCP 客户端
    /// </summary>
    public static async Task<McpClientWrapper?> CreateSettingsClientAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            const string settingsServerId = "MicrosoftWindows.Client.Core_cw5n1h2txyewy_com.microsoft.windows.ai.mcpServer_settings-mcp-server";
            
            var transportOptions = new StdioClientTransportOptions
            {
                Name = "Settings-MCP-Client",
                Command = "odr.exe",
                Arguments = new[]
                {
                    "mcp",
                    "--proxy",
                    settingsServerId
                }
            };

            var transport = new StdioClientTransport(transportOptions);
            var mcpClientOptions = new McpClientOptions();
            var client = await McpClient.CreateAsync(transport, mcpClientOptions);

            return new McpClientWrapper(client, settingsServerId);
        }
        catch
        {
            return null; // 连接失败
        }
    }

    /// <summary>
    /// 创建模拟客户端用于演示（当真实 MCP 服务器不可用时）
    /// </summary>
    public static McpClientWrapper CreateDemoClient()
    {
        // 这里返回一个模拟的客户端，用于演示目的
        // 实际实现中应该创建一个模拟的 McpClient
        throw new NotImplementedException("Demo client should be implemented for fallback scenarios");
    }
}