using EasyHMSAPI.Api.Common;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Api.Controllers
{
    /// <summary>
    /// Write side of Health Wiki, for the CMS / EasyHMS review UI (server-to-server, shared key —
    /// see InternalApiKeyFilter). Doctor Dekho never calls this; it only reads
    /// GET /public/health-articles. The review-decision webhook back to the CMS is not part of this.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("internal/health-articles")]
    [AllowAnonymous]
    [SkipHospitalAccessCheck]
    [ServiceFilter(typeof(InternalApiKeyFilter))]
    [EnableRateLimiting("PerIpPolicy")]
    public class HealthArticlesInternalController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<HealthArticlesInternalController> _logger;

        public HealthArticlesInternalController(IMediator mediator, ILogger<HealthArticlesInternalController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        public class HealthArticleBody
        {
            public string? Slug { get; set; }
            public string? Title { get; set; }
            public string? Description { get; set; }
            public string? Content { get; set; }
            public string? RelatedConditionSlug { get; set; }
            public Guid? AuthorDoctorId { get; set; }
            public Guid? ReviewerDoctorId { get; set; }
            public string? Status { get; set; }
        }

        // Create. 201 / 400 / 409 (slug taken).
        [HttpPost]
        public Task<IActionResult> Create([FromBody] HealthArticleBody body) => Save(body, body.Slug, isCreate: true);

        // Partial update — omitted (null) fields are left unchanged. 200 / 400 / 404.
        [HttpPatch("{slug}")]
        public Task<IActionResult> Update(string slug, [FromBody] HealthArticleBody body) => Save(body, slug, isCreate: false);

        private async Task<IActionResult> Save(HealthArticleBody body, string? slug, bool isCreate)
        {
            try
            {
                var response = await _mediator.Send(new SaveHealthArticleRequestModel
                {
                    IsCreate = isCreate,
                    Slug = slug,
                    Title = body.Title,
                    Description = body.Description,
                    Content = body.Content,
                    RelatedConditionSlug = body.RelatedConditionSlug,
                    AuthorDoctorId = body.AuthorDoctorId,
                    ReviewerDoctorId = body.ReviewerDoctorId,
                    Status = body.Status,
                });
                return StatusCode(response.StatusCode, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving health article {Slug}", slug);
                return StatusCode(500, new SaveHealthArticleResponseModel { Success = false, StatusCode = 500, Message = "An error occurred while saving the article." });
            }
        }
    }
}
