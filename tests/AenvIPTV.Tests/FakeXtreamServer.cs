using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Aenv.Tests;

/// <summary>Minimal Xtream Codes server on localhost. Responses are keyed by "action" (empty = login).</summary>
public sealed class FakeXtreamServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cts = new();
    public Dictionary<string, string> Responses { get; } = new();
    public ConcurrentQueue<Uri> Requests { get; } = new();
    public string BaseUrl { get; }

    public FakeXtreamServer()
    {
        var port = FreePort();
        BaseUrl = $"http://127.0.0.1:{port}";
        _listener.Prefixes.Add(BaseUrl + "/");
        _listener.Start();
        _ = Task.Run(LoopAsync);
    }

    private async Task LoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync(); } catch { return; }
            Requests.Enqueue(ctx.Request.Url!);
            var action = ctx.Request.QueryString["action"] ?? "";
            var body = Responses.TryGetValue(action, out var r) ? r : "[]";
            var bytes = Encoding.UTF8.GetBytes(body);
            ctx.Response.ContentType = "application/json";
            ctx.Response.ContentLength64 = bytes.Length;
            await ctx.Response.OutputStream.WriteAsync(bytes);
            ctx.Response.Close();
        }
    }

    private static int FreePort()
    {
        var l = new TcpListener(IPAddress.Loopback, 0);
        l.Start();
        var port = ((IPEndPoint)l.LocalEndpoint).Port;
        l.Stop();
        return port;
    }

    public void Dispose()
    {
        _cts.Cancel();
        _listener.Close();
    }
}
