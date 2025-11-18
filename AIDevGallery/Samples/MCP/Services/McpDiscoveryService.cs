// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Samples.MCP.Models;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Samples.MCP.Services;

/// <summary>
/// MCP Server 发现和连接管理服务
/// </summary>
public class McpDiscoveryService : IDisposable
{
    // 服务器类型常量
    private const string SystemInfoServerId = "system-info";
    private const string FileSystemServerId = "file-system";
    private const string SettingsServerId = "settings";

    // 优先级权重常量
    private const int ReadOperationPriority = 15;
    private const int WriteOperationPriority = 5;
    private const int ServerSpecificPriority = 20;

    private readonly ILogger<McpDiscoveryService>? _logger;
    private readonly ConcurrentDictionary<string, McpServerInfo> _servers = new();
    private readonly ConcurrentDictionary<string, McpClientWrapper> _connections = new();
    private readonly ConcurrentDictionary<string, List<McpToolInfo>> _serverTools = new();
    private bool _disposed;

    public McpDiscoveryService(ILogger<McpDiscoveryService>? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// 初始化并发现所有可用的 MCP servers
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public async Task<List<McpServerInfo>> DiscoverServersAsync(CancellationToken cancellationToken = default)
    {
        var servers = new List<McpServerInfo>();

        // 添加一些预定义的 MCP servers（演示用）
        var predefinedServers = GetPredefinedServers();
        foreach (var server in predefinedServers)
        {
            _servers.TryAdd(server.Id, server);
            servers.Add(server);
        }

        // 尝试连接每个 server 并获取其工具列表
        var connectionTasks = servers.Select(async server =>
        {
            try
            {
                var client = await ConnectToServerAsync(server, cancellationToken);
                if (client != null)
                {
                    var tools = await GetServerToolsAsync(client, server.Id, cancellationToken);
                    _serverTools.TryAdd(server.Id, tools);
                    _logger?.LogInformation($"Connected to MCP server {server.Name} with {tools.Count} tools");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning($"Failed to connect to MCP server {server.Name}: {ex.Message}");
                server.IsEnabled = false;
            }
        });

        await Task.WhenAll(connectionTasks);

        return servers.Where(s => s.IsEnabled).ToList();
    }

    // 服务器连接工厂映射
    private static readonly Dictionary<string, Func<CancellationToken, Task<McpClientWrapper?>>> ServerFactoryMap = new()
    {
        { SystemInfoServerId, McpClientFactory.CreateSystemInfoClientAsync },
        { FileSystemServerId, McpClientFactory.CreateFileOperationsClientAsync },
        { SettingsServerId, McpClientFactory.CreateSettingsClientAsync }
    };

    /// <summary>
    /// 连接到指定的 MCP server
    /// </summary>
    private async Task<McpClientWrapper?> ConnectToServerAsync(McpServerInfo serverInfo, CancellationToken cancellationToken)
    {
        try
        {
            if (!ServerFactoryMap.TryGetValue(serverInfo.Id, out var factory))
            {
                _logger?.LogWarning($"No factory found for server type: {serverInfo.Id}");
                return null;
            }

            var client = await factory(cancellationToken);
            if (client != null)
            {
                _connections.TryAdd(serverInfo.Id, client);
            }

            return client;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error connecting to MCP server {serverInfo.Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 获取指定 server 的工具列表
    /// </summary>
    private async Task<List<McpToolInfo>> GetServerToolsAsync(McpClientWrapper client, string serverId, CancellationToken cancellationToken)
    {
        try
        {
            var toolsResponse = await client.ListToolsAsync(cancellationToken);
            var tools = new List<McpToolInfo>();

            if (toolsResponse != null)
            {
                _logger?.LogDebug($"Server {serverId} returned {toolsResponse.Count} tools");

                foreach (var tool in toolsResponse)
                {
                    var toolInfo = new McpToolInfo
                    {
                        Name = tool.Name ?? string.Empty,
                        Description = tool.Description ?? string.Empty,
                        ServerId = serverId,
                        InputSchema = ConvertJsonSchemaToDict(tool.JsonSchema),
                        Keywords = ExtractKeywordsFromTool(tool, serverId),
                        Priority = CalculateToolPriority(tool, serverId)
                    };
                    tools.Add(toolInfo);

                    _logger?.LogDebug($"  Tool: {toolInfo.Name} - {toolInfo.Description} (Priority: {toolInfo.Priority})");
                }
            }
            else
            {
                _logger?.LogWarning($"Server {serverId} returned null tools response");
            }

            return tools;
        }
        catch (Exception ex)
        {
            _logger?.LogError($"Error getting tools from server {serverId}: {ex.Message}");
            return new List<McpToolInfo>();
        }
    }

    /// <summary>
    /// 将 JsonSchema 转换为字典格式
    /// </summary>
    private static Dictionary<string, object> ConvertJsonSchemaToDict(System.Text.Json.JsonElement jsonSchema)
    {
        if (jsonSchema.ValueKind != System.Text.Json.JsonValueKind.Object)
        {
            return new Dictionary<string, object>();
        }

        try
        {
            return jsonSchema.EnumerateObject()
                .ToDictionary(property => property.Name, property => (object)property.Value.ToString());
        }
        catch (Exception)
        {
            return new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// 从工具描述中提取关键词
    /// </summary>
    private string[] ExtractKeywordsFromTool(McpClientTool tool, string serverId)
    {
        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 从工具名称中提取
        if (!string.IsNullOrEmpty(tool.Name))
        {
            keywords.UnionWith(tool.Name.Split('_', '-', ' ', '.'));
        }

        // 从描述中提取
        if (!string.IsNullOrEmpty(tool.Description))
        {
            var description = tool.Description.ToLower();
            var allKeywords = GetServerSpecificKeywords(serverId).Concat(CommonKeywords);
            keywords.UnionWith(allKeywords.Where(description.Contains));
        }

        // 根据服务器ID添加特定标签
        keywords.UnionWith(GetServerTypeKeywords(serverId));

        return keywords.Where(k => !string.IsNullOrWhiteSpace(k)).ToArray();
    }

    // 服务器特定关键词配置
    private static readonly Dictionary<string, string[]> ServerSpecificKeywords = new()
    {
        { SystemInfoServerId, new[] { "system", "hardware", "memory", "ram", "cpu", "disk", "processor", "info", "status", "performance" } },
        { FileSystemServerId, new[] { "file", "folder", "directory", "path", "read", "write", "copy", "move", "delete", "list", "create", "exists" } },
        { SettingsServerId, new[] { "settings", "config", "configuration", "preferences", "registry", "policy", "option", "value", "key" } }
    };

    private static readonly Dictionary<string, string[]> ServerTypeKeywords = new()
    {
        { SystemInfoServerId, new[] { "system", "hardware" } },
        { FileSystemServerId, new[] { "file", "filesystem" } },
        { SettingsServerId, new[] { "settings", "config" } }
    };

    private static readonly string[] CommonKeywords = { "get", "set", "list", "info", "status", "read", "write", "create", "delete", "update" };
    private static readonly string[] DefaultKeywords = { "general", "utility", "tool" };
    private static readonly string[] DefaultTypeKeywords = { "general" };

    /// <summary>
    /// 获取服务器特定关键词
    /// </summary>
    private string[] GetServerSpecificKeywords(string serverId)
    {
        return ServerSpecificKeywords.TryGetValue(serverId, out var keywords) ? keywords : DefaultKeywords;
    }

    /// <summary>
    /// 获取服务器类型关键词
    /// </summary>
    private string[] GetServerTypeKeywords(string serverId)
    {
        return ServerTypeKeywords.TryGetValue(serverId, out var keywords) ? keywords : DefaultTypeKeywords;
    }

    // 优先级规则配置
    private static readonly Dictionary<string, int> ReadOperationKeywords = new()
    {
        { "get", ReadOperationPriority },
        { "info", ReadOperationPriority },
        { "list", ReadOperationPriority }
    };

    private static readonly Dictionary<string, int> WriteOperationKeywords = new()
    {
        { "set", WriteOperationPriority },
        { "update", WriteOperationPriority },
        { "create", WriteOperationPriority }
    };

    private static readonly Dictionary<string, Dictionary<string, int>> ServerPriorityRules = new()
    {
        {
            SystemInfoServerId, new Dictionary<string, int>
            {
                { "memory", ServerSpecificPriority },
                { "cpu", ServerSpecificPriority },
                { "disk", ServerSpecificPriority },
                { "system", ReadOperationPriority },
                { "hardware", ReadOperationPriority }
            }
        },
        {
            FileSystemServerId, new Dictionary<string, int>
            {
                { "file", ServerSpecificPriority },
                { "directory", ServerSpecificPriority },
                { "path", ServerSpecificPriority },
                { "read", 10 },
                { "list", 10 }
            }
        },
        {
            SettingsServerId, new Dictionary<string, int>
            {
                { "setting", ServerSpecificPriority },
                { "config", ServerSpecificPriority },
                { "get", 10 },
                { "read", 10 }
            }
        }
    };

    /// <summary>
    /// 计算工具优先级
    /// </summary>
    private int CalculateToolPriority(McpClientTool tool, string serverId)
    {
        if (string.IsNullOrEmpty(tool.Name))
        {
            return 0;
        }

        var name = tool.Name.ToLower();
        var priority = 0;

        // 基础功能优先级
        priority += ReadOperationKeywords.Where(kv => name.Contains(kv.Key)).Sum(kv => kv.Value);
        priority += WriteOperationKeywords.Where(kv => name.Contains(kv.Key)).Sum(kv => kv.Value);

        // 服务器特定优先级调整
        if (ServerPriorityRules.TryGetValue(serverId, out var rules))
        {
            priority += rules.Where(kv => name.Contains(kv.Key)).Sum(kv => kv.Value);
        }

        return priority;
    }

    /// <summary>
    /// 获取预定义的 MCP servers（基于现有的Windows MCP服务器）
    /// </summary>
    private static List<McpServerInfo> GetPredefinedServers()
    {
        return new List<McpServerInfo>
        {
            new McpServerInfo
            {
                Id = SystemInfoServerId,
                Name = "System Information Server",
                Description = "Provides system hardware and software information",
                ExecutablePath = "odr.exe",
                Arguments = ["mcp", "--proxy", "MicrosoftWindows.Client.Core_cw5n1h2txyewy_com.microsoft.windows.ai.mcpServer_systeminfo-mcp-server"],
                Categories = ["system", "hardware"],
                Tags = ["ram", "cpu", "disk", "memory", "hardware", "system", "info"],
                IsEnabled = true
            },
            new McpServerInfo
            {
                Id = FileSystemServerId,
                Name = "File Operations Server",
                Description = "File system operations and information",
                ExecutablePath = "odr.exe",
                Arguments = ["mcp", "--proxy", "MicrosoftWindows.Client.Core_cw5n1h2txyewy_com.microsoft.windows.ai.mcpServer_file-mcp-server"],
                Categories = ["filesystem", "files"],
                Tags = ["file", "directory", "path", "storage", "read", "write", "list"],
                IsEnabled = true
            },
            new McpServerInfo
            {
                Id = SettingsServerId,
                Name = "Settings Server",
                Description = "Windows settings and configuration management",
                ExecutablePath = "odr.exe",
                Arguments = ["mcp", "--proxy", "MicrosoftWindows.Client.Core_cw5n1h2txyewy_com.microsoft.windows.ai.mcpServer_settings-mcp-server"],
                Categories = ["settings", "configuration"],
                Tags = ["settings", "config", "windows", "preferences", "system"],
                IsEnabled = true
            }
        };
    }

    /// <summary>
    /// 获取所有已连接的 server 客户端
    /// </summary>
    /// <returns></returns>
    public IEnumerable<McpClientWrapper> GetAllServerClients()
    {
        return _connections.Values.ToList();
    }

    /// <summary>
    /// 获取指定 server 的工具列表
    /// </summary>
    /// <returns></returns>
    public List<McpToolInfo> GetServerTools(string serverId)
    {
        return _serverTools.TryGetValue(serverId, out var tools) ? tools : new List<McpToolInfo>();
    }

    /// <summary>
    /// 获取所有工具
    /// </summary>
    /// <returns></returns>
    public List<McpToolInfo> GetAllTools()
    {
        return _serverTools.Values.SelectMany(tools => tools).ToList();
    }

    /// <summary>
    /// 获取指定 server 的客户端连接
    /// </summary>
    /// <returns></returns>
    public McpClientWrapper? GetServerClient(string serverId)
    {
        return _connections.TryGetValue(serverId, out var client) ? client : null;
    }

    /// <summary>
    /// 获取所有已连接的服务器信息
    /// </summary>
    /// <returns></returns>
    public List<McpServerInfo> GetConnectedServers()
    {
        return _servers.Values.Where(s => s.IsEnabled && _connections.ContainsKey(s.Id)).ToList();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        foreach (var connection in _connections.Values)
        {
            try
            {
                connection?.Dispose();
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Error disposing MCP client: {ex.Message}");
            }
        }

        _connections.Clear();
        _servers.Clear();
        _serverTools.Clear();
        _disposed = true;
    }
}