using System.Text;
using Microsoft.AspNetCore.Routing;

namespace RecServer.Infrastructure;

/// <summary>Logs every request (host, path, interesting headers, first 4 KB of the body) and whether an endpoint handled it.</summary>
public class RequestLogMiddleware
{
    /// <summary>HttpContext.Items key set by OverrideMiddleware when a canned file answered the request.</summary>
    public const string OverrideKey = "rec.override";
    private static readonly string[] Interesting =
        { "Authorization", "X-Rec-Room-Key", "X-RNSK", "X-Rec-Platform", "X-Rec-Build", "Platform", "Cookie", "Accept", "Accept-Language", "X-Unity-Version" };
    private readonly RequestDelegate _next;
    private readonly RequestLog _log;

    public RequestLogMiddleware(RequestDelegate next, RequestLog log) { _next = next; _log = log; }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var req = ctx.Request;
        string? body = null;
        if (req.ContentLength is > 0 or null && req.Method is "POST" or "PUT" or "PATCH" or "DELETE")
        {
            req.EnableBuffering();
            var buf = new byte[4096];
            int n = await req.Body.ReadAsync(buf);
            body = Encoding.UTF8.GetString(buf, 0, n);
            req.Body.Position = 0;
        }
        await _next(ctx);
        var headers = req.Headers
            .Where(h => Interesting.Contains(h.Key, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(h => h.Key, h => h.Value.ToString());
        var endpoint = ctx.GetEndpoint();
        bool handled = ctx.Items.ContainsKey(OverrideKey) || req.Path.StartsWithSegments("/swagger") || endpoint != null && endpoint.DisplayName != "fallback" && !(endpoint.Metadata.GetMetadata<EndpointNameMetadata>()?.EndpointName == "fallback");
        _log.Add(new LoggedRequest(DateTime.Now, req.Host.ToString(), req.Method, req.Path, req.QueryString.ToString(),
            req.ContentType, req.Headers.UserAgent.ToString(), headers, body, ctx.Response.StatusCode, handled));
    }
}
