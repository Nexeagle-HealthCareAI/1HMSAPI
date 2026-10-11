using EasyHMSAPI.Api.Common;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics.CodeAnalysis;

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
    }
}
