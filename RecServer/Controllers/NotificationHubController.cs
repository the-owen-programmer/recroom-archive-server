using System.Net.WebSockets;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace RecServer.Controllers;

/// <summary>
/// Notification hub, host <c>notifications.rec.net</c>: the client negotiates (POST /hub/v1/negotiate) and then opens a websocket at
/// <c>GET /hub/v1?id=&lt;connection token&gt;</c>. Without a working hub the login fails with error code <c>note</c>.
/// Implements the SignalR handshake only (JSON protocol: messages end with 0x1E): reply <c>{}</c> to the client's handshake, answer
/// pings, and keep the connection open. No notifications are ever pushed. Everything the client sends is logged to the console.
/// </summary>
[ApiController]
[Host("notifications.rec.net")]
[Tags("NotificationHub (notifications.rec.net)")]
public class NotificationHubController : ControllerBase
{
    private const char RS = '\u001e';

    [HttpGet("/hub/v1")]
    public async Task Connect()
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest) { Response.StatusCode = 400; return; }
        using var ws = await HttpContext.WebSockets.AcceptWebSocketAsync();
        var buf = new byte[16 * 1024];
        var pending = new StringBuilder();
        try
        {
            while (ws.State == WebSocketState.Open)
            {
                var r = await ws.ReceiveAsync(buf, HttpContext.RequestAborted);
                if (r.MessageType == WebSocketMessageType.Close) break;
                string text = Encoding.UTF8.GetString(buf, 0, r.Count);
                Console.WriteLine($"[hub] recv {r.MessageType} {text.Replace(RS, '|')[..Math.Min(300, text.Length)]}");
                pending.Append(text);
                if (!r.EndOfMessage) continue;
                string all = pending.ToString(); pending.Clear();
                foreach (var msg in all.Split(RS, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (msg.Contains("\"protocol\""))                       // handshake request
                        await Send(ws, "{}");
                    else if (msg.Contains("\"type\":6"))                    // ping -> ping
                        await Send(ws, "{\"type\":6}");
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (WebSocketException) { }
    }

    private static Task Send(WebSocket ws, string json) =>
        ws.SendAsync(Encoding.UTF8.GetBytes(json + RS), WebSocketMessageType.Text, true, CancellationToken.None);
}
