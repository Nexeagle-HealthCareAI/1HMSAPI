using EasyHMSAPI.Api.Common;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace EasyHMSAPI.Api.Controllers
{
    /// <summary>
    /// Health Wiki for a hospital doctor in EasyHMS: the articles assigned to them for medical review, their decision, and how their
    /// approved articles are doing. Staff JWT plus the health_wiki permission. The doctor is always the signed-in user, never taken
    /// from the request.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("health-wiki")]
    [Authorize]
    [RequiresPermission("health_wiki")]
    public class HealthWikiController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<HealthWikiController> _logger;

        public HealthWikiController(IMediator mediator, ILogger<HealthWikiController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        public class ReviewBody
        {
            public string? Decision { get; set; }
            public string? Comment { get; set; }
            public bool AccuracyConfirmed { get; set; }
        }

        [HttpGet("inbox")]
        public async Task<IActionResult> Inbox()
        {
            var userId = UserContextHelper.GetUserId(User);
            if (userId == null) return Unauthorized();
            try
            {
                var res = await _mediator.Send(new GetHealthWikiInboxRequestModel { UserId = userId.Value });
                return res.Success ? Ok(new { pending = res.Pending, published = res.Published }) : StatusCode(res.StatusCode, new { message = res.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading the Health Wiki inbox");
                return StatusCode(500, new { message = "Something went wrong. Try again." });
            }
        }

        [HttpGet("articles/{slug}")]
        public async Task<IActionResult> Article(string slug)
        {
            var userId = UserContextHelper.GetUserId(User);
            if (userId == null) return Unauthorized();
            try
            {
                var res = await _mediator.Send(new GetHealthWikiStaffArticleRequestModel { UserId = userId.Value, Slug = slug });
                return res.Success ? Ok(res.Article) : StatusCode(res.StatusCode, new { message = res.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading a Health Wiki article for review");
                return StatusCode(500, new { message = "Something went wrong. Try again." });
            }
        }

        [HttpPost("articles/{slug}/review")]
        public async Task<IActionResult> Review(string slug, [FromBody] ReviewBody? body)
        {
            var userId = UserContextHelper.GetUserId(User);
            if (userId == null) return Unauthorized();
            try
            {
                var res = await _mediator.Send(new ReviewHealthWikiArticleRequestModel
                {
                    UserId = userId.Value, Slug = slug, Decision = body?.Decision, Comment = body?.Comment, AccuracyConfirmed = body?.AccuracyConfirmed ?? false,
                });
                return res.Success ? Ok(new { outcome = res.Outcome }) : StatusCode(res.StatusCode, new { message = res.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving a Health Wiki review");
                return StatusCode(500, new { message = "Something went wrong. Try again." });
            }
        }

        public class WriteHandoffBody
        {
            public string? To { get; set; }
        }

        public class TopicBody
        {
            public string? Title { get; set; }
            public string? Type { get; set; }
            public string? Outline { get; set; }
            public string? WhyItMatters { get; set; }
            public string? ConditionSlug { get; set; }
            public string? References { get; set; }
            public bool RequestWriter { get; set; }
        }

        // "Write an article": a Doctor Dekho address that signs the doctor in once (no WhatsApp code), then the doctor writes there as any contributor does.
        [HttpPost("write-handoff")]
        public async Task<IActionResult> WriteHandoff([FromBody] WriteHandoffBody? body)
        {
            var userId = UserContextHelper.GetUserId(User);
            if (userId == null) return Unauthorized();
            try
            {
                var res = await _mediator.Send(new CreateHealthWikiWriteHandoffRequestModel { UserId = userId.Value, To = body?.To });
                return res.Success ? Ok(new { url = res.Url }) : StatusCode(res.StatusCode, new { message = res.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error making the Doctor Dekho hand-off");
                return StatusCode(500, new { message = "Something went wrong. Try again." });
            }
        }

        // The doctor's own topic requests.
        [HttpGet("topics")]
        public async Task<IActionResult> Topics()
        {
            var userId = UserContextHelper.GetUserId(User);
            if (userId == null) return Unauthorized();
            try
            {
                var res = await _mediator.Send(new GetHealthWikiStaffTopicsRequestModel { UserId = userId.Value });
                return res.Success ? Ok(res.Topics) : StatusCode(res.StatusCode, new { message = res.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing a doctor's topic requests");
                return StatusCode(500, new { message = "Something went wrong. Try again." });
            }
        }

        // Suggest a topic. requestWriter=true asks the team to have someone else write it, with this doctor as the reviewer.
        [HttpPost("topics")]
        public async Task<IActionResult> CreateTopic([FromBody] TopicBody? body)
        {
            var userId = UserContextHelper.GetUserId(User);
            if (userId == null) return Unauthorized();
            try
            {
                var res = await _mediator.Send(new CreateHealthWikiStaffTopicRequestModel
                {
                    UserId = userId.Value, Title = body?.Title, Type = body?.Type, Outline = body?.Outline, WhyItMatters = body?.WhyItMatters,
                    ConditionSlug = body?.ConditionSlug, References = body?.References, RequestWriter = body?.RequestWriter ?? false,
                });
                return res.Success ? StatusCode(res.StatusCode, res.Topic) : StatusCode(res.StatusCode, new { message = res.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating a doctor's topic request");
                return StatusCode(500, new { message = "Something went wrong. Try again." });
            }
        }

        // Answer "needs detail". Only the fields sent are changed.
        [HttpPatch("topics/{id:guid}")]
        public async Task<IActionResult> AddTopicDetail(Guid id, [FromBody] JsonElement body)
        {
            var userId = UserContextHelper.GetUserId(User);
            if (userId == null) return Unauthorized();
            try
            {
                var provided = body.ValueKind == JsonValueKind.Object ? body.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal) : new HashSet<string>();
                string? Text(string key) => body.ValueKind == JsonValueKind.Object && body.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                var res = await _mediator.Send(new AddHealthWikiStaffTopicDetailRequestModel
                {
                    UserId = userId.Value, TopicId = id, Outline = Text("outline"), WhyItMatters = Text("whyItMatters"), ConditionSlug = Text("conditionSlug"),
                    References = Text("references"), Provided = provided,
                });
                return res.Success ? Ok(res.Topic) : StatusCode(res.StatusCode, new { message = res.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding detail to a doctor's topic request");
                return StatusCode(500, new { message = "Something went wrong. Try again." });
            }
        }
    }
}
