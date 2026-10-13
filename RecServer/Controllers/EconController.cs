using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace RecServer.Controllers;

/// <summary>Economy service, host <c>econ.rec.net</c>.</summary>
[ApiController]
[Host("econ.rec.net")]
[Tags("Econ (econ.rec.net)")]
public class EconController : ControllerBase
{
    /// <summary>Avatar items every player owns by default. Empty for now (the client keeps its built-in defaults).</summary>
    [HttpGet("/api/avatar/v1/defaultunlocked")]
    [Produces("application/json")]
    public object[] DefaultUnlocked() => [];
}
