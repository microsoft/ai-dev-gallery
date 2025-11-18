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
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _serverId = !string.IsNullOrWhiteSpace(serverId) ? serverId : throw new ArgumentException("ServerId cannot be null or whitespace.", nameof(serverId));
    }

    public async Task<IReadOnlyList<McpClientTool>?> ListToolsAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        
        var tools = await _client.ListToolsAsync(cancellationToken);
        return tools?.ToList().AsReadOnly();
    }

    public async Task<CallToolResult> CallToolAsync(string toolName, Dictionary<string, object?> arguments, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(arguments);
        
        return await _client.CallToolAsync(toolName, arguments, cancellationToken);
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            try
            {
                _client?.Dispose();
            }
            catch (Exception)
            {
                // 忽略清理过程中的异常，避免在终结器中抛出异常
            }
        }

        _disposed = true;
    }
}

/// <summary>
/// MCP 客户端工厂，用于创建不同类型的 MCP 连接
/// </summary>
public static class McpClientFactory
{
    // 服务器ID常量
    private const string SystemInfoServerId = "MicrosoftWindows.Client.Core_cw5n1h2txyewy_com.microsoft.windows.ai.mcpServer_systeminfo-mcp-server";
    private const string FileServerId = "MicrosoftWindows.Client.Core_cw5n1h2txyewy_com.microsoft.windows.ai.mcpServer_file-mcp-server";
    private const string SettingsServerId = "MicrosoftWindows.Client.Core_cw5n1h2txyewy_com.microsoft.windows.ai.mcpServer_settings-mcp-server";

    // 客户端配置
    private const string OdrCommand = "odr.exe";
    private const string McpArgument = "mcp";
    private const string ProxyArgument = "--proxy";

    // 服务器配置
    private static readonly Dictionary<string, ServerConfig> ServerConfigs = new()
    {
        {
            "system-info", new ServerConfig
            {
                ServerId = SystemInfoServerId,
                ClientName = "SystemInfo-MCP-Client"
            }
        },
        {
            "file-system", new ServerConfig
            {
                ServerId = FileServerId,
                ClientName = "File-MCP-Client"
            }
        },
        {
            "settings", new ServerConfig
            {
                ServerId = SettingsServerId,
                ClientName = "Settings-MCP-Client"
            }
        }
    };

    private record ServerConfig(string ServerId, string ClientName);

    /// <summary>
    /// 通用的 MCP 客户端创建方法
    /// </summary>
    private static async Task<McpClientWrapper?> CreateClientAsync(ServerConfig config, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        try
        {
            var transportOptions = new StdioClientTransportOptions
            {
                Name = config.ClientName,
                Command = OdrCommand,
                Arguments = new[] { McpArgument, ProxyArgument, config.ServerId }
            };

            var transport = new StdioClientTransport(transportOptions);
            var mcpClientOptions = new McpClientOptions();
            var client = await McpClient.CreateAsync(transport, mcpClientOptions, cancellationToken);

            return new McpClientWrapper(client, config.ServerId);
        }
        catch (Exception)
        {
            // 连接失败时返回 null，让调用者处理
            return null;
        }
    }

    /// <summary>
    /// 创建系统信息 MCP 客户端
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task<McpClientWrapper?> CreateSystemInfoClientAsync(CancellationToken cancellationToken = default)
    {
        return await CreateClientAsync(ServerConfigs["system-info"], cancellationToken);
    }

    /// <summary>
    /// 创建文件操作 MCP 客户端
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task<McpClientWrapper?> CreateFileOperationsClientAsync(CancellationToken cancellationToken = default)
    {
        return await CreateClientAsync(ServerConfigs["file-system"], cancellationToken);
    }

    /// <summary>
    /// 创建设置 MCP 客户端
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task<McpClientWrapper?> CreateSettingsClientAsync(CancellationToken cancellationToken = default)
    {
        return await CreateClientAsync(ServerConfigs["settings"], cancellationToken);
    }

    /// <summary>
    /// 通用的客户端创建方法，根据服务器类型创建相应的客户端
    /// </summary>
    /// <param name="serverType">服务器类型 ("system-info", "file-system", "settings")</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>创建的客户端包装器，如果创建失败则返回 null</returns>
    public static async Task<McpClientWrapper?> CreateClientAsync(string serverType, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serverType);
        
        if (!ServerConfigs.TryGetValue(serverType, out var config))
        {
            throw new ArgumentException($"Unknown server type: {serverType}. Valid types are: {string.Join(", ", ServerConfigs.Keys)}", nameof(serverType));
        }

        return await CreateClientAsync(config, cancellationToken);
    }

    /// <summary>
    /// 获取所有支持的服务器类型
    /// </summary>
    /// <returns>支持的服务器类型列表</returns>
    public static IReadOnlyList<string> GetSupportedServerTypes()
    {
        return ServerConfigs.Keys.ToList().AsReadOnly();
    }
}