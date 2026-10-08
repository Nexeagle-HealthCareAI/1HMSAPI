using EasyHMSAPI.Api.Common;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace EasyHMSAPI.Api.Controllers
{
    /// <summary>
    /// Article side of Health Wiki for the CMS API (server-to-server, shared key, see InternalApiKeyFilter).
    /// Doctor Dekho never calls this; it only reads GET /public/health-articles. The caller says who acted in
    /// the X-Actor header (the signed-in CMS user) so the history can show it.
    /// A body field that is absent is left unchanged; a field sent as null is cleared.
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

        public class ReasonBody
        {
            public string? Reason { get; set; }
        }

        // List, any status. Optional ?status= and ?type=.
        [HttpGet]
        public async Task<IActionResult> List([FromQuery] string? status, [FromQuery] string? type)
        {
            try
            {
                return Ok(await _mediator.Send(new GetHealthArticlesAdminRequestModel { Status = status, Type = type }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing health articles");
                return StatusCode(500, new { message = "An error occurred while listing articles." });
            }
        }

        [HttpGet("{slug}")]
        public async Task<IActionResult> Get(string slug)
        {
            try
            {
                var res = await _mediator.Send(new GetHealthArticlesAdminRequestModel { Slug = slug });
                var article = res.Articles.FirstOrDefault();
                return article == null ? NotFound(new { message = "Article not found." }) : Ok(article);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading health article {Slug}", slug);
                return StatusCode(500, new { message = "An error occurred while reading the article." });
            }
        }

        [HttpGet("{slug}/history")]
        public async Task<IActionResult> History(string slug)
        {
            try
            {
                var res = await _mediator.Send(new GetHealthArticleHistoryRequestModel { Slug = slug });
                return res.Found ? Ok(res.Entries) : NotFound(new { message = "Article not found." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading history of {Slug}", slug);
                return StatusCode(500, new { message = "An error occurred while reading the history." });
            }
        }

        // Create. 201 / 400 / 409 (slug taken).
        [HttpPost]
        public Task<IActionResult> Create([FromBody] JsonElement body) => Save(body, isCreate: true, slugFromRoute: null);

        // Partial update. 200 / 400 / 404.
        [HttpPatch("{slug}")]
        public Task<IActionResult> Update(string slug, [FromBody] JsonElement body) => Save(body, isCreate: false, slugFromRoute: slug);

        // A CMS editor publishes a sector update, or approves the edit waiting on a published one.
        [HttpPost("{slug}/approve")]
        public Task<IActionResult> Approve(string slug) => Decide(slug, DecideHealthArticleRequestModel.Approve, null);

        // Send back for changes. The reason is shown to the author.
        [HttpPost("{slug}/withdraw")]
        public Task<IActionResult> Withdraw(string slug, [FromBody] ReasonBody body) => Decide(slug, DecideHealthArticleRequestModel.Withdraw, body?.Reason);

        // Take a live article offline at once.
        [HttpPost("{slug}/unpublish")]
        public Task<IActionResult> Unpublish(string slug, [FromBody] ReasonBody body) => Decide(slug, DecideHealthArticleRequestModel.Unpublish, body?.Reason);

        private string Actor() => Request.Headers["X-Actor"].ToString();

        private async Task<IActionResult> Decide(string slug, string action, string? reason)
        {
            try
            {
                var response = await _mediator.Send(new DecideHealthArticleRequestModel { Slug = slug, Action = action, Reason = reason, ActorName = Actor() });
                return StatusCode(response.StatusCode, response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error applying {Action} to health article {Slug}", action, slug);
                return StatusCode(500, new SaveHealthArticleResponseModel { Success = false, StatusCode = 500, Message = "An error occurred while saving the article." });
            }
        }

        private async Task<IActionResult> Save(JsonElement body, bool isCreate, string? slugFromRoute)
        {
            string? slug = slugFromRoute;
            try
            {
                if (body.ValueKind != JsonValueKind.Object)
                    return BadRequest(new SaveHealthArticleResponseModel { Success = false, StatusCode = 400, Message = "A JSON object body is required." });

                var provided = new HashSet<string>(StringComparer.Ordinal);
                foreach (var p in body.EnumerateObject()) provided.Add(p.Name);

                string? Text(string key) => body.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                Guid? Id(string key) => body.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String && Guid.TryParse(v.GetString(), out var g) ? g : null;

                // An id that is present but not a valid guid is an error, not "clear it".
                foreach (var key in new[] { "authorContributorId", "reviewerContributorId" })
                {
                    if (body.TryGetProperty(key, out var v) && v.ValueKind != JsonValueKind.Null && Id(key) == null)
                        return BadRequest(new SaveHealthArticleResponseModel { Success = false, StatusCode = 400, Message = $"{key} must be a GUID or null." });
                }

                slug ??= Text("slug");
                var response = await _mediator.Send(new SaveHealthArticleRequestModel
                {
                    IsCreate = isCreate,
                    Slug = slug,
                    Type = Text("type"),
                    Title = Text("title"),
                    Description = Text("description"),
                    Content = Text("content"),
                    RelatedConditionSlug = Text("relatedConditionSlug"),
                    CoverImageUrl = Text("coverImageUrl"),
                    CoverImageAlt = Text("coverImageAlt"),
                    Disclosure = Text("disclosure"),
                    References = Text("references"),
                    AuthorContributorId = Id("authorContributorId"),
                    ReviewerContributorId = Id("reviewerContributorId"),
                    Status = Text("status"),
                    ActorName = Actor(),
                    Provided = provided,
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
