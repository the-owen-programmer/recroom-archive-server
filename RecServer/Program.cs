using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using Microsoft.OpenApi;
using RecServer.Infrastructure;
using RecServer.Services;

// Fake Rec Room backend (RecNet) for offline preservation.
// Listens for HTTPS on REC_PORT (default 443); the certificate for whatever host name the client asks for (SNI) is issued
// on the fly by a local test CA (server/certs/make-certs.sh). Meant to run inside a private network namespace together with the game
// (server/run-in-netns.sh), where every *.rec.net name resolves to 127.0.0.1.

var builder = WebApplication.CreateBuilder(args);

int port = int.Parse(Environment.GetEnvironmentVariable("REC_PORT") ?? "443");
string certDir = Environment.GetEnvironmentVariable("REC_CERTS")
                 ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "certs");
string logDir = Environment.GetEnvironmentVariable("REC_LOGS")
                ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "logs");
Directory.CreateDirectory(logDir);
string dataDir = Environment.GetEnvironmentVariable("REC_DATA")
                 ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "data");
int localAccountId = int.Parse(Environment.GetEnvironmentVariable("REC_ACCOUNT_ID") ?? "1");

if (Environment.GetEnvironmentVariable("REC_LOGLEVEL") == "Debug")   // see TLS handshake failures and the alert the client sent
    builder.Logging.AddFilter("Microsoft.AspNetCore.Server.Kestrel", LogLevel.Debug);
var certs = new CertificateAuthority(certDir);
builder.WebHost.ConfigureKestrel(k => k.ListenAnyIP(port, l => l.UseHttps(new Microsoft.AspNetCore.Server.Kestrel.Https.HttpsConnectionAdapterOptions
{
    ServerCertificateSelector = (_, name) => certs.For(name),
})));

builder.Services.AddSingleton(new RequestLog(Path.Combine(logDir, "requests.jsonl")));
var data = new DataStore(dataDir);
builder.Services.AddSingleton(data);
builder.Services.AddSingleton(new RoomService(data, Path.Combine(AppContext.BaseDirectory, "Seed", "rooms.json")));
builder.Services.AddSingleton(new PresenceService(localAccountId));
builder.Services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.PropertyNamingPolicy = null; // keep PascalCase exactly as declared; the client's formatters are case sensitive
    o.JsonSerializerOptions.DictionaryKeyPolicy = null;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Fake Rec Room backend",
        Version = "v1",
        Description = "Re-implementation of the RecNet services the Rec Room client talks to, reconstructed from the client's " +
                      "own code and from the requests it makes. Every *.rec.net host is served by this one process; the " +
                      "'Host' column in each endpoint says which service name the client uses."
    });
    c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml"));
    c.CustomSchemaIds(t => t.Name);
    c.ResolveConflictingActions(a => a.First());   // several hosts share paths such as "/"
});

var app = builder.Build();

// Single-player Photon stand-in (UDP name/master/game servers) so the client can "join" room instances offline.
if (Environment.GetEnvironmentVariable("REC_PHOTON") != "0")
{
    var photonLog = Path.Combine(logDir, "photon.log");
    var photonLock = new object();
    void PhotonLog(string line)
    {
        line = $"{DateTime.Now:HH:mm:ss.fff} [photon] {line}";
        Console.WriteLine(line);
        lock (photonLock) File.AppendAllText(photonLog, line + "\n");
    }
    int PortVar(string name, int fallback) => int.TryParse(Environment.GetEnvironmentVariable(name), out var v) ? v : fallback;
    var photon = new RecServer.Photon.PhotonStandIn(
        Environment.GetEnvironmentVariable("REC_PHOTON_HOST") ?? "127.0.0.1",
        PortVar("REC_PHOTON_NS_PORT", 5058), PortVar("REC_PHOTON_MASTER_PORT", 5055), PortVar("REC_PHOTON_GAME_PORT", 5056),
        (Environment.GetEnvironmentVariable("REC_PHOTON_REGIONS") ?? "usw,us,eu").Split(','), PhotonLog);
    photon.Start();
    app.Lifetime.ApplicationStopping.Register(photon.Dispose);
}
app.UseMiddleware<RequestLogMiddleware>();
app.UseWebSockets();
app.UseMiddleware<OverrideMiddleware>();   // data/overrides/<host>/<path>.json wins over everything below
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Fake Rec Room backend v1"));
app.MapControllers();
// Anything the client asks for that is not implemented yet: log it (RequestLogMiddleware) and answer 404.
app.MapFallback(async ctx =>
{
    ctx.Response.StatusCode = 404;
    ctx.Response.ContentType = "application/json";
    await ctx.Response.WriteAsync("{\"error\":\"not implemented\"}");
}).WithMetadata(new Microsoft.AspNetCore.Routing.EndpointNameMetadata("fallback"));

// Write the generated API reference next to the code every time the server starts.
app.Lifetime.ApplicationStarted.Register(() =>
{
    _ = Task.Run(async () =>
    {
        try
        {
            using var http = new HttpClient(new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true });
            var json = await http.GetStringAsync($"https://localhost:{port}/swagger/v1/swagger.json");
            var docs = Path.Combine(logDir, "..", "docs");
            Directory.CreateDirectory(docs);
            await File.WriteAllTextAsync(Path.Combine(docs, "openapi.json"), json);
            Console.WriteLine($"API reference written to {Path.GetFullPath(Path.Combine(docs, "openapi.json"))}");
        }
        catch (Exception e) { Console.WriteLine("could not write openapi.json: " + e.Message); }
    });
});

Console.WriteLine($"Fake Rec Room backend on https://*:{port}");
app.Run();
