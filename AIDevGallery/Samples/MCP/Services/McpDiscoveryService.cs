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
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
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
    private string[] ExtractKeywordsFromTool(McpClientTool tool, string serverId)
    {
        var keywords = new List<string>();

        // 从工具名称中提取
        if (!string.IsNullOrEmpty(tool.Name))
        {
            keywords.AddRange(tool.Name.Split('_', '-', ' ', '.'));
        }

        // 从描述中提取
        if (!string.IsNullOrEmpty(tool.Description))
        {
            var description = tool.Description.ToLower();
            
            // 基于服务器类型定义更具体的关键词
            var serverSpecificKeywords = GetServerSpecificKeywords(serverId);
            keywords.AddRange(serverSpecificKeywords.Where(k => description.Contains(k)));
            
            // 通用关键词
            var commonKeywords = new[] { "get", "set", "list", "info", "status", "read", "write", "create", "delete", "update" };
            keywords.AddRange(commonKeywords.Where(k => description.Contains(k)));
        }

        // 根据服务器ID添加特定标签
        keywords.AddRange(GetServerTypeKeywords(serverId));

        return keywords.Distinct().Where(k => !string.IsNullOrWhiteSpace(k)).ToArray();
    }

    /// <summary>
    /// 获取服务器特定关键词
    /// </summary>
    private string[] GetServerSpecificKeywords(string serverId)
    {
        return serverId switch
        {
            "system-info" => new[] { "system", "hardware", "memory", "ram", "cpu", "disk", "processor", "info", "status", "performance" },
            "file-system" => new[] { "file", "folder", "directory", "path", "read", "write", "copy", "move", "delete", "list", "create", "exists" },
            "settings" => new[] { "settings", "config", "configuration", "preferences", "registry", "policy", "option", "value", "key" },
            _ => new[] { "general", "utility", "tool" }
        };
    }

    /// <summary>
    /// 获取服务器类型关键词
    /// </summary>
    private string[] GetServerTypeKeywords(string serverId)
    {
        return serverId switch
        {
            "system-info" => new[] { "system", "hardware" },
            "file-system" => new[] { "file", "filesystem" },
            "settings" => new[] { "settings", "config" },
            _ => new[] { "general" }
        };
    }

    /// <summary>
    /// 计算工具优先级
    /// </summary>
    private int CalculateToolPriority(McpClientTool tool, string serverId)
    {
        var priority = 0;

        if (!string.IsNullOrEmpty(tool.Name))
        {
            var name = tool.Name.ToLower();
            
            // 基础功能优先级
            if (name.Contains("get") || name.Contains("info") || name.Contains("list"))
            {
                priority += 15; // 只读操作优先级高
            }
            
            if (name.Contains("set") || name.Contains("update") || name.Contains("create"))
            {
                priority += 5; // 写操作优先级低
            }

            // 服务器特定优先级调整
            switch (serverId)
            {
                case "system-info":
                    if (name.Contains("memory") || name.Contains("cpu") || name.Contains("disk"))
                        priority += 20;
                    if (name.Contains("system") || name.Contains("hardware"))
                        priority += 15;
                    break;
                case "file-system":
                    if (name.Contains("file") || name.Contains("directory") || name.Contains("path"))
                        priority += 20;
                    if (name.Contains("read") || name.Contains("list"))
                        priority += 10;
                    break;
                case "settings":
                    if (name.Contains("setting") || name.Contains("config"))
                        priority += 20;
                    if (name.Contains("get") || name.Contains("read"))
                        priority += 10;
                    break;
            }
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