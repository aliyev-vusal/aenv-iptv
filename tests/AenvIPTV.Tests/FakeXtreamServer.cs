using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Aenv.Tests;

/// <summary>
/// Minimal Xtream Codes / file server on localhost.
/// Responses are keyed by "action" (empty = login) or by path; Faults lets a test make a key
/// answer with error statuses first. It also records how many requests ran at the same time.
/// </summary>
public sealed class FakeXtreamServer : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cts = new();
    private int _active;

    public Dictionary<string, string> Responses { get; } = new();
    public Dictionary<string, byte[]> Files { get; } = new();
    public ConcurrentDictionary<string, ConcurrentQueue<(int Status, string? RetryAfter)>> Faults { get; } = new();
    public ConcurrentQueue<Uri> Requests { get; } = new();
    public int MaxConcurrent { get; private set; }
    public int ResponseDelayMs { get; set; } = 20;
    public string BaseUrl { get; }

    public FakeXtreamServer()
    {
        var port = FreePort();
        BaseUrl = $"http://127.0.0.1:{port}";
        _listener.Prefixes.Add(BaseUrl + "/");
        _listener.Start();
        _ = Task.Run(LoopAsync);
    }

    public void Fail(string key, int status, int times = 1, string? retryAfter = null)
    {
        var q = Faults.GetOrAdd(key, _ => new ConcurrentQueue<(int, string?)>());
        for (int i = 0; i < times; i++) q.Enqueue((status, retryAfter));
    }

    public int Count(string key)
    {
        int n = 0;
        foreach (var u in Requests)
            if ((System.Web.HttpUtility.ParseQueryString(u.Query)["action"] ?? u.AbsolutePath) == key ||
                (key == "" && u.AbsolutePath.EndsWith("player_api.php") && System.Web.HttpUtility.ParseQueryString(u.Query)["action"] == null)) n++;
        return n;
    }

    private async Task LoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync(); } catch { return; }
            _ = Task.Run(() => HandleAsync(ctx));
        }
    }

    private async Task HandleAsync(HttpListenerContext ctx)
    {
        var now = Interlocked.Increment(ref _active);
        lock (this) MaxConcurrent = Math.Max(MaxConcurrent, now);
        try
        {
            Requests.Enqueue(ctx.Request.Url!);
            await Task.Delay(ResponseDelayMs);
            var path = ctx.Request.Url!.AbsolutePath;
            var key = path.EndsWith("player_api.php") ? ctx.Request.QueryString["action"] ?? "" : path;

            if (Faults.TryGetValue(key, out var q) && q.TryDequeue(out var fault))
            {
                ctx.Response.StatusCode = fault.Status;
                if (fault.RetryAfter != null) ctx.Response.AddHeader("Retry-After", fault.RetryAfter);
                ctx.Response.Close();
                return;
            }

            byte[] bytes = Files.TryGetValue(path, out var file) ? file
                : Encoding.UTF8.GetBytes(Responses.TryGetValue(key, out var r) ? r : "[]");
            ctx.Response.ContentType = Files.ContainsKey(path) ? "application/octet-stream" : "application/json";
            ctx.Response.ContentLength64 = bytes.Length;
            await ctx.Response.OutputStream.WriteAsync(bytes);
            ctx.Response.Close();
        }
        catch { /* client went away */ }
        finally { Interlocked.Decrement(ref _active); }
    }

    public static int FreePort()
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
