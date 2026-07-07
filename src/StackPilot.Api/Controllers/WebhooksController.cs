using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StackPilot.Application.Interfaces;

namespace StackPilot.Api.Controllers;

[ApiController]
[Route("api/v1/webhooks")]
public class WebhooksController : ControllerBase
{
    private readonly IWebhookService _webhooks;

    public WebhooksController(IWebhookService webhooks) => _webhooks = webhooks;

    [HttpPost("github")]
    [AllowAnonymous]
    public async Task<IActionResult> GitHub(CancellationToken ct)
    {
        var eventType = Request.Headers["X-GitHub-Event"].FirstOrDefault() ?? "unknown";
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(ct);

        await _webhooks.HandleGitHubEventAsync(eventType, payload, ct);
        return Ok();
    }
}
