using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Aenv.Services;

/// <summary>Raised when a server keeps refusing a request; the message is localized and user-friendly.</summary>
public class HttpFailureException : Exception
{
    public HttpStatusCode? Status { get; }
    public HttpFailureException(string message, HttpStatusCode? status, Exception? inner = null) : base(message, inner) => Status = status;
}

/// <summary>
/// Shared HTTP client for playlists, Xtream API and EPG.
/// IPTV providers often allow only one or two connections per account and answer bursts with
/// 429 (Too Many Requests) or 503. Requests are therefore retried with back-off, honouring the
/// server's Retry-After header, and failures are turned into messages users can act on.
/// </summary>
public static class Http
{
    public static readonly HttpClient Client = CreateClient();

    /// <summary>Attempts per request (first try + retries).</summary>
    public static int MaxAttempts { get; set; } = 3;

    /// <summary>Longest wait we accept from a Retry-After header.</summary>
    public static TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Delay function; replaced in tests so they don't actually wait.</summary>
    public static Func<TimeSpan, CancellationToken, Task> Delay { get; set; } = (t, ct) => Task.Delay(t, ct);

    private static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All };
        var c = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("AENV-IPTV/1.0");
        return c;
    }

    public static async Task<string> GetStringAsync(string url, CancellationToken ct = default)
    {
        using var r = await SendAsync(url, ct);
        return await r.Content.ReadAsStringAsync(ct);
    }

    public static async Task<byte[]> GetBytesAsync(string url, CancellationToken ct = default)
    {
        using var r = await SendAsync(url, ct);
        return await r.Content.ReadAsByteArrayAsync(ct);
    }

    private static bool IsTransient(HttpStatusCode s) =>
        s is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable
          or HttpStatusCode.BadGateway or HttpStatusCode.GatewayTimeout or HttpStatusCode.RequestTimeout;

    private static async Task<HttpResponseMessage> SendAsync(string url, CancellationToken ct)
    {
        for (int attempt = 1; ; attempt++)
        {
            HttpResponseMessage response;
            try
            {
                response = await Client.GetAsync(url, HttpCompletionOption.ResponseContentRead, ct);
            }
            catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
            {
                if (attempt >= MaxAttempts) throw new HttpFailureException(Loc.T("http_timeout"), null, ex);
                await Delay(Backoff(attempt), ct);
                continue;
            }
            catch (HttpRequestException ex)
            {
                if (attempt >= MaxAttempts) throw new HttpFailureException(Loc.T("http_network"), null, ex);
                await Delay(Backoff(attempt), ct);
                continue;
            }

            if (response.IsSuccessStatusCode) return response;

            var status = response.StatusCode;
            if (IsTransient(status) && attempt < MaxAttempts)
            {
                var wait = RetryAfter(response) ?? Backoff(attempt);
                response.Dispose();
                await Delay(wait, ct);
                continue;
            }

            response.Dispose();
            throw new HttpFailureException(MessageFor(status), status);
        }
    }

    /// <summary>2 s, 4 s, 8 s … capped at MaxRetryDelay.</summary>
    internal static TimeSpan Backoff(int attempt) =>
        TimeSpan.FromSeconds(Math.Min(Math.Pow(2, attempt), MaxRetryDelay.TotalSeconds));

    internal static TimeSpan? RetryAfter(HttpResponseMessage r)
    {
        var ra = r.Headers.RetryAfter;
        TimeSpan? wait = ra?.Delta ?? (ra?.Date is { } d ? d - DateTimeOffset.UtcNow : null);
        if (wait == null) return null;
        if (wait < TimeSpan.Zero) wait = TimeSpan.Zero;
        return wait > MaxRetryDelay ? MaxRetryDelay : wait;
    }

    internal static string MessageFor(HttpStatusCode status) => status switch
    {
        HttpStatusCode.TooManyRequests => Loc.T("http_too_many"),
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => Loc.T("http_denied"),
        HttpStatusCode.NotFound => Loc.T("http_not_found"),
        _ when (int)status >= 500 => Loc.F("http_server_error", (int)status),
        _ => Loc.F("http_other", (int)status),
    };
}
