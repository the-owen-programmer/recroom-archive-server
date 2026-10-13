using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using RecServer.Models;

namespace RecServer.Controllers;

/// <summary>Service discovery, host <c>ns.rec.net</c>. The very first request the client makes.</summary>
[ApiController]
[Host("ns.rec.net")]
[Tags("NameServer (ns.rec.net)")]
public class NameServerController : ControllerBase
{
    /// <summary>List the base URL of every RecNet service.</summary>
    /// <remarks>
    /// The client logs "RecNet name server query failed" and "Failed to connect to RecNet (error code: summer)" when this
    /// does not answer. The exact path the client requests is not known yet, so every GET on this host returns the table;
    /// check <c>logs/requests.jsonl</c> for what the client really sends.
    /// </remarks>
    [HttpGet("/")]
    [HttpGet("/{**path}")]
    [ProducesResponseType<NameServerResponse>(StatusCodes.Status200OK)]
    public NameServerResponse Get() => NameServerResponse.Local();
}
