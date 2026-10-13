using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RecServer.Services;

/// <summary>Issues real-format HS256 JWTs for the local player. The key is fixed and public: tokens only have to look right to the
/// client (and to the local services); they protect nothing.</summary>
public static class TokenService
{
    private static readonly byte[] Key = Encoding.UTF8.GetBytes("recserver-local-archive-not-a-secret");
    public const string Issuer = "https://auth.rec.net";

    private static string B64(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static string B64(string s) => B64(Encoding.UTF8.GetBytes(s));

    public static string AccessToken(int accountId, int lifetimeSeconds, string[] scopes)
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = new Dictionary<string, object>
        {
            ["iss"] = Issuer, ["aud"] = Issuer + "/resources", ["exp"] = now + lifetimeSeconds, ["nbf"] = now, ["iat"] = now,
            ["client_id"] = "recroom", ["sub"] = accountId.ToString(), ["auth_time"] = now, ["idp"] = "local",
            ["role"] = "gameClient", ["scope"] = scopes,
        };
        string head = B64("{\"alg\":\"HS256\",\"typ\":\"JWT\"}");
        string body = B64(JsonSerializer.Serialize(payload));
        string sig = B64(HMACSHA256.HashData(Key, Encoding.ASCII.GetBytes(head + "." + body)));
        return $"{head}.{body}.{sig}";
    }
}
