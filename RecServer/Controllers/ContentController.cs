using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.StaticFiles;
using RecServer.Infrastructure;

namespace RecServer.Controllers;

/// <summary>
/// Images, host <c>img.rec.net</c>: room thumbnails, profile pictures and photos by file name
/// (<c>https://img.rec.net/2uqgowkv970eheb1v9gp0loaa.jpg</c>, often with <c>?width=</c>/<c>cropSquare=</c>, which are ignored).
/// Served from <c>data/img/</c>; anything missing gets a grey placeholder so the UI never shows a broken image.
/// </summary>
[ApiController]
[Host("img.rec.net")]
[Tags("Images (img.rec.net)")]
public class ImagesController : ControllerBase
{
    /// <summary>1x1 grey PNG.</summary>
    private static readonly byte[] Placeholder = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAAAAAA6fptVAAAACklEQVR4nGNoAAAAggCBd81ytgAAAABJRU5ErkJggg==");
    private static readonly FileExtensionContentTypeProvider Types = new();
    private readonly DataStore _data;

    public ImagesController(DataStore data) => _data = data;

    /// <summary>An image by name; the extension may be left off (the archive file can be .jpg or .png).</summary>
    [HttpGet("/{**name}")]
    public IActionResult Get(string? name)
    {
        if (!string.IsNullOrEmpty(name))
        {
            foreach (var candidate in new[] { name, name + ".jpg", name + ".png" })
            {
                var file = _data.SafePath("img", candidate);
                if (file != null && System.IO.File.Exists(file))
                    return PhysicalFile(file, Types.TryGetContentType(file, out var t) ? t : "image/jpeg");
            }
        }
        Response.Headers["X-Rec-Placeholder"] = "1";
        return File(Placeholder, "image/png");
    }
}

/// <summary>
/// Content delivery, host <c>cdn.rec.net</c>: saved room data (the <c>DataBlob</c> of a sub-room, i.e. everything players built in a
/// room), inventions and other blobs, by path. Served byte for byte from <c>data/cdn/&lt;path&gt;</c>; a missing blob is a 404 so the
/// gap shows up in requests.jsonl instead of the client loading garbage.
/// </summary>
[ApiController]
[Host("cdn.rec.net")]
[Tags("CDN (cdn.rec.net)")]
public class CdnController : ControllerBase
{
    private static readonly FileExtensionContentTypeProvider Types = new();
    private readonly DataStore _data;

    public CdnController(DataStore data) => _data = data;

    /// <summary>A blob by path (e.g. <c>/room/&lt;blob name&gt;</c>).</summary>
    [HttpGet("/{**path}")]
    public IActionResult Get(string? path)
    {
        var file = string.IsNullOrEmpty(path) ? null : _data.SafePath("cdn", path);
        if (file == null || !System.IO.File.Exists(file)) return NotFound();
        return PhysicalFile(file, ContentType(file), enableRangeProcessing: true);
    }

    /// <summary>By extension; extension-less files (cdn.rec.net/config/LoadingScreenTipData) are JSON if they start like JSON.</summary>
    private static string ContentType(string file)
    {
        if (Types.TryGetContentType(file, out var t)) return t;
        using var f = System.IO.File.OpenRead(file);
        int b;
        while ((b = f.ReadByte()) is ' ' or '\t' or '\r' or '\n' or 0xEF or 0xBB or 0xBF) { }   // whitespace / UTF-8 BOM
        return b is '{' or '[' ? "application/json" : "application/octet-stream";
    }
}
