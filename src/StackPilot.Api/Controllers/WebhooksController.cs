using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using StackPilot.Application.Interfaces;
using StackPilot.Application.Workflow;

namespace StackPilot.Api.Controllers;

[ApiController]
[Route("api/v1/webhooks")]
public class WebhooksController : ControllerBase
{
    private readonly IWebhookService _webhooks;
    private readonly IConfiguration _config;
    private readonly ILogger<WebhooksController> _logger;
    private readonly IHostEnvironment _env;

    public WebhooksController(IWebhookService webhooks, IConfiguration config, ILogger<WebhooksController> logger, IHostEnvironment env)
    {
        _webhooks = webhooks;
        _config = config;
        _logger = logger;
        _env = env;
    }

    [HttpPost("github")]
    [AllowAnonymous]
    public async Task<IActionResult> GitHub(CancellationToken ct)
    {
        var eventType = Request.Headers["X-GitHub-Event"].FirstOrDefault() ?? "unknown";
        using var reader = new StreamReader(Request.Body);
        var payload = await reader.ReadToEndAsync(ct);

        var secret = _config["Webhooks:GitHub:Secret"];
        if (!string.IsNullOrWhiteSpace(secret))
        {
            var signature = Request.Headers["X-Hub-Signature-256"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(signature) ||
                !GitHubWebhookSignature.IsValid(payload, signature, secret))
                return Unauthorized();
        }
        else
        {
            // Fail closed in production-like environments to prevent request forgery.
            if (!_env.IsDevelopment() && !_env.IsEnvironment("Testing"))
                return StatusCode(500);

            _logger.LogWarning("GitHub webhook secret not configured; accepting request without signature verification in dev/testing");
        }

        await _webhooks.HandleGitHubEventAsync(eventType, payload, ct);
        return Ok();
    }
}
