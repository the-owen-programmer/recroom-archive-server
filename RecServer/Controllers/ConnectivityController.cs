using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace RecServer.Controllers;

/// <summary>
/// The client checks that the machine is online before it talks to RecNet
/// ("[RecNet.Features.NameServer] Network connectivity test failed" otherwise) by requesting Google's captive-portal URL.
/// </summary>
[ApiController]
[Host("www.google.com", "google.com")]
[Tags("Connectivity (google.com)")]
public class ConnectivityController : ControllerBase
{
    /// <summary>Internet reachability probe. Must answer 204 No Content.</summary>
    [HttpGet("/generate_204")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Generate204() => NoContent();

    /// <summary>Plain 200 for the bare host (the client also references <c>https://google.com</c>).</summary>
    [HttpGet("/")]
    [HttpHead("/")]
    [ProducesResponseType<string>(StatusCodes.Status200OK)]
    public IActionResult Root() => Ok("ok");
}
