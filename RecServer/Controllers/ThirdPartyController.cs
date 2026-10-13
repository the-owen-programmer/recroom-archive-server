using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace RecServer.Controllers;

/// <summary>
/// Third-party services the client calls at startup (feature flags, analytics). They are not RecNet, but if they answer with an
/// HTTP error the client's behaviour changes (observed: game starts shutting down within a second), so they are faked too.
/// </summary>
[ApiController]
[Tags("Third-party: Statsig (statsigapi.net)")]
[Host("statsigapi.net")]
public class StatsigController : ControllerBase
{
    /// <summary>Statsig SDK initialisation: the client's feature gates, dynamic configs and layers.</summary>
    /// <remarks>
    /// Empty ("everything off / default") config. The client also logs "Detected change in CdnKey from statsig", so a dynamic config
    /// named after the CDN key may be needed later; the request body (user, statsigMetadata) is logged by the request log.
    /// </remarks>
    [HttpPost("/v1/initialize")]
    [Produces("application/json")]
    public object Initialize() => new
    {
        feature_gates = new { },
        dynamic_configs = new { },
        layer_configs = new { },
        sdkParams = new { },
        has_updates = true,
        generator = "recserver",
        time = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
        hash_used = "djb2",
        evaluated_keys = new { },
    };

    /// <summary>Statsig event upload.</summary>
    [HttpPost("/v1/log_event")]
    [Produces("application/json")]
    public object LogEvent() => new { success = true };
}

/// <summary>RudderStack analytics (recroom-dataplane.rudderstack.com).</summary>
[ApiController]
[Tags("Third-party: RudderStack")]
[Host("recroom-dataplane.rudderstack.com")]
public class RudderStackController : ControllerBase
{
    /// <summary>Accept any analytics call (track, identify, batch, webhook).</summary>
    [HttpPost("/v1/{**call}")]
    [Produces("application/json")]
    public object Accept(string call) => new { success = true };
}

/// <summary>Unity cloud services: analytics config, CDP events, performance reports.</summary>
[ApiController]
[Tags("Third-party: Unity cloud")]
[Host("config.uca.cloud.unity3d.com", "cdp.cloud.unity3d.com", "perf-events.cloud.unity3d.com")]
public class UnityCloudController : ControllerBase
{
    /// <summary>Accept any Unity cloud POST/GET with an empty JSON object.</summary>
    [HttpPost("/{**path}")]
    [HttpGet("/{**path}")]
    [Produces("application/json")]
    public object Accept(string? path) => new { };
}
