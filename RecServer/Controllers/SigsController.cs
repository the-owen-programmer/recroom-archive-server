using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace RecServer.Controllers;

/// <summary>
/// File signatures, host <c>cdn.rec.net</c>, <c>/sigs/&lt;guid&gt;</c>. Format (read from the client, see docs/signature-key.md): text lines separated by
/// CRLF, <c>name|base64(signature of that file)</c> per file, and a FINAL line that is base64(RSA-PKCS#1-v1.5-SHA256 over the concatenation of all
/// preceding lines, no separators). The client checks that last line against the modulus baked into its (patched) metadata, so it is signed here with
/// our own key (certs/content-sign-private.pem, matching tools/patch/swap_signing_key.py). With no per-file lines the client has nothing further to verify.
/// Optional pre-made per-file lines can be placed in data/sigs/&lt;guid&gt;.lines (one <c>name|base64sig</c> per line).
/// </summary>
[ApiController]
[Host("cdn.rec.net")]
[Tags("CDN (cdn.rec.net)")]
public class SigsController : ControllerBase
{
    private static RSA? _key;
    private static RSA Key => _key ??= LoadKey();

    private static RSA LoadKey()
    {
        var path = Environment.GetEnvironmentVariable("REC_SIGN_KEY")
                   ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../certs/content-sign-private.pem"));
        var rsa = RSA.Create();
        rsa.ImportFromPem(System.IO.File.ReadAllText(path));
        return rsa;
    }

    [HttpGet("/sigs/{guid}")]
    public IActionResult Get(string guid)
    {
        var lines = new List<string>();
        var premade = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../data/sigs", Path.GetFileName(guid) + ".lines"));
        if (System.IO.File.Exists(premade))
            lines.AddRange(System.IO.File.ReadAllLines(premade).Select(l => l.Trim()).Where(l => l.Length > 0));
        var body = Encoding.UTF8.GetBytes(string.Concat(lines));
        var sig = Convert.ToBase64String(Key.SignData(body, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        lines.Add(sig);
        return Content(string.Join("\r\n", lines), "text/plain");
    }
}
