namespace RecServer.Infrastructure;

/// <summary>
/// Canned responses that win over the built-in endpoints, so a response shape can be corrected (or a new endpoint added) by
/// dropping a file in place, without recompiling. Lookup order for <c>POST https://api.rec.net/api/config/v2</c>:
/// <c>overrides/api.rec.net/api/config/v2.POST.json</c>, then <c>overrides/api.rec.net/api/config/v2.json</c>.
/// The file is sent as-is with status 200 and <c>application/json</c>. Files are re-read on every request.
/// </summary>
public class OverrideMiddleware
{
    private readonly RequestDelegate _next;
    private readonly DataStore _data;

    public OverrideMiddleware(RequestDelegate next, DataStore data) { _next = next; _data = data; }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var path = ctx.Request.Path.Value?.TrimEnd('/') ?? "";
        if (path.Length == 0) path = "/index";
        var host = ctx.Request.Host.Host.ToLowerInvariant();
        foreach (var candidate in new[] { $"{host}{path}.{ctx.Request.Method}.json", $"{host}{path}.json" })
        {
            var file = _data.SafePath("overrides", candidate);
            if (file == null || !File.Exists(file)) continue;
            ctx.Items[RequestLogMiddleware.OverrideKey] = candidate;
            ctx.Response.StatusCode = 200;
            ctx.Response.ContentType = "application/json";
            await ctx.Response.SendFileAsync(file);
            return;
        }
        await _next(ctx);
    }
}
