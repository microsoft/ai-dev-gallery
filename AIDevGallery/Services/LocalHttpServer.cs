// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AIDevGallery.Services;

/// <summary>
/// Local HTTP server hosting Minimal API endpoints for interacting with the local LanguageModel.
/// Only binds to 127.0.0.1 for local development use.
/// </summary>
public sealed class LocalHttpServer : IAsyncDisposable
{
    private readonly IHost _host;

    private LocalHttpServer(IHost host)
    {
        _host = host;
    }

    public static async Task<LocalHttpServer> StartAsync(CancellationToken cancellationToken)
    {
        var builder = Host.CreateApplicationBuilder();

        builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
            p.WithOrigins("http://localhost")
             .AllowAnyHeader()
             .AllowAnyMethod()));

        builder.Services.AddSingleton<LanguageModelProvider>();

        var app = builder.Build();

        var webApp = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(LocalHttpServer).Assembly.FullName,
            Args = Array.Empty<string>()
        });

        webApp.WebHost.UseKestrel().UseUrls("http://127.0.0.1:5114");
        webApp.Services.AddCors(o => o.AddDefaultPolicy(p =>
            p.WithOrigins("http://localhost")
             .AllowAnyHeader()
             .AllowAnyMethod()));
        webApp.Services.AddSingleton<LanguageModelProvider>();

        var app2 = webApp.Build();
        app2.UseCors();

        app2.MapPost("/v1/generate", async (HttpContext ctx, LanguageModelProvider provider) =>
        {
            var req = await JsonSerializer.DeserializeAsync<GenerateRequest>(ctx.Request.Body, cancellationToken: ctx.RequestAborted);
            if (req is null || string.IsNullOrWhiteSpace(req.Prompt))
            {
                return Results.BadRequest(new { error = "prompt is required" });
            }

            var lm = await provider.GetAsync(ctx.RequestAborted);
            var op = lm.GenerateResponseAsync(req.Prompt);
            var text = await op;
            return Results.Json(new { text });
        });

        app2.MapPost("/v1/stream", async (HttpContext ctx, LanguageModelProvider provider) =>
        {
            ctx.Response.Headers.Append("Content-Type", "text/event-stream");
            var req = await JsonSerializer.DeserializeAsync<GenerateRequest>(ctx.Request.Body, cancellationToken: ctx.RequestAborted);
            if (req is null || string.IsNullOrWhiteSpace(req.Prompt))
            {
                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                await ctx.Response.WriteAsync("event:error\ndata:{\"message\":\"prompt is required\"}\n\n", ctx.RequestAborted);
                return;
            }

            var lm = await provider.GetAsync(ctx.RequestAborted);
            var op = lm.GenerateResponseAsync(req.Prompt);
            op.Progress = async (info, delta) =>
            {
                await ctx.Response.WriteAsync($"data:{JsonSerializer.Serialize(new { delta })}\n\n", ctx.RequestAborted);
                await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
            };
            _ = await op;
            await ctx.Response.WriteAsync("event:end\ndata:{}\n\n", ctx.RequestAborted);
        });

        await app2.StartAsync(cancellationToken);

        return new LocalHttpServer(app2);
    }

    public async ValueTask DisposeAsync()
    {
        if (_host is IAsyncDisposable asyncDisp)
        {
            await asyncDisp.DisposeAsync();
        }
        else
        {
            _host.Dispose();
        }
    }

    private sealed class GenerateRequest
    {
        public string Prompt { get; set; } = string.Empty;
    }
}


