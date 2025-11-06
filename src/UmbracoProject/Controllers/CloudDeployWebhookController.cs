using System;
using System.Linq;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Scoping;

namespace UmbracoProject.Controllers
{
    [ApiController]
    [Route("api/webhooks")]
    public class CloudDeployWebhookController : ControllerBase
    {
        private readonly IContentService _contentService;
        private readonly IConfiguration _config;
        private readonly ICoreScopeProvider _scopeProvider;

        public CloudDeployWebhookController(
            IContentService contentService,
            IConfiguration config,
            ICoreScopeProvider scopeProvider)
        {
            _contentService = contentService;
            _config = config;
            _scopeProvider = scopeProvider;
        }

        [HttpPost("cloud-deploy")]
        public IActionResult ReceiveFromCloudDeploy(
            [FromBody] JsonElement payload,
            [FromQuery(Name = "t")] string? token = null)
        {
            return HandleWebhook(payload, token);
        }

        private IActionResult HandleWebhook(JsonElement payload, string? token)
        {
            var section = _config.GetSection("DeploymentWebhook");
            var secret = section["Secret"] ?? string.Empty;

            if (string.IsNullOrWhiteSpace(token) ||
                !string.Equals(token, secret, StringComparison.Ordinal))
                return Unauthorized();

            if (!Guid.TryParse(section["ContentKey"], out var contentKey))
                return BadRequest("Invalid ContentKey.");

            var propAlias = section["PropertyAlias"] ?? "deploymentData";
            var content = _contentService.GetById(contentKey);

            if (content is null)
                return NotFound();

            using (var scope = _scopeProvider.CreateCoreScope())
            {
                content.SetValue(propAlias, payload.GetRawText());
                _contentService.Save(content);

                var cultures = content.ContentType.VariesByCulture()
                    ? (content.AvailableCultures ?? Enumerable.Empty<string>()).ToArray()
                    : Array.Empty<string>();

                var publishResult = _contentService.Publish(content, cultures, -1);
                scope.Complete();

                if (!publishResult.Success)
                    return StatusCode(500, "Publish failed.");
            }

            return Ok(new { ok = true });
        }
    }
}