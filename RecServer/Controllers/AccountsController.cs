using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using RecServer.Models;

namespace RecServer.Controllers;

/// <summary>Account service, host <c>accounts.rec.net</c>. Only the local player exists.</summary>
[ApiController]
[Host("accounts.rec.net")]
[Tags("Accounts (accounts.rec.net)")]
public class AccountsController : ControllerBase
{
    /// <summary>Several accounts by id, query <c>?id=1&amp;id=2</c>. Unknown ids are skipped.</summary>
    [HttpGet("/account/bulk")]
    [Produces("application/json")]
    public object[] Bulk([FromQuery(Name = "id")] int[] ids) =>
        ids.Contains(LocalPlayer.AccountId) ? [LocalPlayer.Account()] : [];

    /// <summary>The logged-in account.</summary>
    [HttpGet("/account/me")]
    [Produces("application/json")]
    public object Me() => LocalPlayer.Account();

    /// <summary>One account by id.</summary>
    [HttpGet("/account/{id:int}")]
    [Produces("application/json")]
    public IActionResult ById(int id) => id == LocalPlayer.AccountId ? Ok(LocalPlayer.Account()) : NotFound();
}
