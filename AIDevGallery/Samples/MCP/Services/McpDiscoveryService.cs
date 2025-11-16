// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using AIDevGallery.Samples.MCP.Models;
using AIDevGallery.Samples.MCP.Models;
using ModelContextProtocol.Protocol;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AIDevGallery.Samples.MCP.Services;

/// <summary>
/// MCP Server 发现和连接管理服务
/// </summary>
public class McpDiscoveryService : IDisposable
{
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

    /// <summary>
    /// 连接到指定的 MCP server
    /// </summary>
    private async Task<McpClientWrapper?> ConnectToServerAsync(McpServerInfo serverInfo, CancellationToken cancellationToken)
    {
        try
        {
            McpClientWrapper? client = null;
            
            // 根据服务器类型选择合适的连接方式
            if (serverInfo.Id == "system-info")
            {
                client = await McpClientFactory.CreateSystemInfoClientAsync(cancellationToken);
            }
            else if (serverInfo.Id == "file-system")
            {
                client = await McpClientFactory.CreateFileOperationsClientAsync(cancellationToken);
            }
            else if (serverInfo.Id == "settings")
            {
                client = await McpClientFactory.CreateSettingsClientAsync(cancellationToken);
            }
            
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
                foreach (var tool in toolsResponse)
                {
                    var toolInfo = new McpToolInfo
                    {
                        Name = tool.Name ?? string.Empty,
                        Description = tool.Description ?? string.Empty,
                        ServerId = serverId,
                        InputSchema = ConvertJsonSchemaToDict(tool.JsonSchema),
                        Keywords = ExtractKeywordsFromTool(tool),
                        Priority = CalculateToolPriority(tool)
                    };
                    tools.Add(toolInfo);
                }
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
    private Dictionary<string, object> ConvertJsonSchemaToDict(System.Text.Json.JsonElement jsonSchema)
    {
        var dict = new Dictionary<string, object>();
        
        try
        {
            if (jsonSchema.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                foreach (var property in jsonSchema.EnumerateObject())
                {
                    dict[property.Name] = property.Value.ToString();
                }
            }
        }
        catch
        {
            // 转换失败，返回空字典
        }
        
        return dict;
    }

    /// <summary>
    /// 从工具描述中提取关键词
    /// </summary>
    private string[] ExtractKeywordsFromTool(McpClientTool tool)
    {
        var keywords = new List<string>();
        
        // 从工具名称中提取
        if (!string.IsNullOrEmpty(tool.Name))
        {
            keywords.AddRange(tool.Name.Split('_', '-', ' '));
        }

        // 从描述中提取
        if (!string.IsNullOrEmpty(tool.Description))
        {
            var description = tool.Description.ToLower();
            var commonKeywords = new[] { "system", "memory", "ram", "cpu", "disk", "file", "process", "network", "hardware", "info" };
            keywords.AddRange(commonKeywords.Where(k => description.Contains(k)));
        }

        return keywords.Distinct().ToArray();
    }

    /// <summary>
    /// 计算工具优先级
    /// </summary>
    private int CalculateToolPriority(McpClientTool tool)
    {
        // 基于工具名称和描述的简单优先级计算
        var priority = 0;
        
        if (!string.IsNullOrEmpty(tool.Name))
        {
            var name = tool.Name.ToLower();
            if (name.Contains("get") || name.Contains("info")) priority += 10;
            if (name.Contains("system") || name.Contains("hardware")) priority += 20;
        }

        return priority;
    }

    /// <summary>
    /// 获取预定义的 MCP servers（基于现有的Windows MCP服务器）
    /// </summary>
    private List<McpServerInfo> GetPredefinedServers()
    {
        return new List<McpServerInfo>
        {
            new McpServerInfo
            {
                Id = "system-info",
                Name = "System Information Server",
                Description = "Provides system hardware and software information",
                ExecutablePath = "odr.exe", // 使用Windows MCP代理
                Arguments = ["mcp", "--proxy", "MicrosoftWindows.Client.Core_cw5n1h2txyewy_com.microsoft.windows.ai.mcpServer_systeminfo-mcp-server"],
                Categories = ["system", "hardware"],
                Tags = ["ram", "cpu", "disk", "memory", "hardware", "system", "info"],
                IsEnabled = true
            },
            new McpServerInfo
            {
                Id = "file-system",
                Name = "File Operations Server",
                Description = "File system operations and information",
                ExecutablePath = "odr.exe", // 使用Windows MCP代理
                Arguments = ["mcp", "--proxy", "MicrosoftWindows.Client.Core_cw5n1h2txyewy_com.microsoft.windows.ai.mcpServer_file-mcp-server"],
                Categories = ["filesystem", "files"],
                Tags = ["file", "directory", "path", "storage", "read", "write", "list"],
                IsEnabled = true
            },
            new McpServerInfo
            {
                Id = "settings",
                Name = "Settings Server",
                Description = "Windows settings and configuration management",
                ExecutablePath = "odr.exe", // 使用Windows MCP代理
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
    public IEnumerable<McpClientWrapper> GetAllServerClients()
    {
        return _connections.Values.ToList();
    }

    /// <summary>
    /// 获取指定 server 的工具列表
    /// </summary>
    public List<McpToolInfo> GetServerTools(string serverId)
    {
        return _serverTools.TryGetValue(serverId, out var tools) ? tools : new List<McpToolInfo>();
    }

    /// <summary>
    /// 获取所有工具
    /// </summary>
    public List<McpToolInfo> GetAllTools()
    {
        return _serverTools.Values.SelectMany(tools => tools).ToList();
    }

    /// <summary>
    /// 获取指定 server 的客户端连接
    /// </summary>
    public McpClientWrapper? GetServerClient(string serverId)
    {
        return _connections.TryGetValue(serverId, out var client) ? client : null;
    }

    public void Dispose()
    {
        if (_disposed) return;

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