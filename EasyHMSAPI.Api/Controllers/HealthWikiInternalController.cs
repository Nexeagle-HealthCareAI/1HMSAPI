using EasyHMSAPI.Api.Common;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Api.Controllers
{
    /// <summary>
    /// Health Wiki lookups for the CMS API (server-to-server, shared key): the conditions an article can link to
    /// and the counts on the CMS tabs, and the contributors (list, invite, send link, verify, reject). Topic requests are
    /// added here as they are built. The caller says who acted in the X-Actor header.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("internal/health-wiki")]
    [AllowAnonymous]
    [SkipHospitalAccessCheck]
    [ServiceFilter(typeof(InternalApiKeyFilter))]
    [EnableRateLimiting("PerIpPolicy")]
    public class HealthWikiInternalController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<HealthWikiInternalController> _logger;

        public HealthWikiInternalController(IMediator mediator, ILogger<HealthWikiInternalController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        [HttpGet("conditions")]
        public async Task<IActionResult> Conditions()
        {
            try
            {
                return Ok((await _mediator.Send(new GetHealthWikiConditionsRequestModel())).Conditions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading Health Wiki conditions");
                return StatusCode(500, new { message = "An error occurred while reading the conditions." });
            }
        }

        [HttpGet("summary")]
        public async Task<IActionResult> Summary()
        {
            try
            {
                return Ok(await _mediator.Send(new GetHealthWikiSummaryRequestModel()));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading Health Wiki summary");
                return StatusCode(500, new { message = "An error occurred while reading the summary." });
            }
        }

        public class InviteBody
        {
            public string? FullName { get; set; }
            public string? Mobile { get; set; }
            public string? Type { get; set; }
        }

        public class SendLinkBody
        {
            public string? ArticleSlug { get; set; }
        }

        public class ReasonBody
        {
            public string? Reason { get; set; }
        }

        private string Actor() => Request.Headers["X-Actor"].ToString();

        [HttpGet("contributors")]
        public async Task<IActionResult> Contributors([FromQuery] string? status, [FromQuery] string? type)
        {
            try
            {
                return Ok((await _mediator.Send(new GetContributorsAdminRequestModel { Status = status, Type = type })).Contributors);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error listing contributors");
                return StatusCode(500, new { message = "An error occurred while listing contributors." });
            }
        }

        // Create the person (INVITED) and send the JOIN link. 201 / 400 / 409 (number already used) / 503 (links not configured).
        [HttpPost("contributors/invite")]
        public Task<IActionResult> Invite([FromBody] InviteBody body) =>
            Run(() => _mediator.Send(new InviteContributorRequestModel { FullName = body?.FullName, Mobile = body?.Mobile, Type = body?.Type, ActorName = Actor() }), "inviting a contributor");

        // A new link, replacing the previous unused one. With articleSlug: the review link (the article's reviewer) or write link (its author).
        [HttpPost("contributors/{id:guid}/send-link")]
        public Task<IActionResult> SendLink(Guid id, [FromBody] SendLinkBody? body) =>
            Run(() => _mediator.Send(new SendContributorLinkRequestModel { ContributorId = id, ArticleSlug = body?.ArticleSlug, ActorName = Actor() }), "sending a contributor link");

        [HttpPost("contributors/{id:guid}/verify")]
        public Task<IActionResult> Verify(Guid id) =>
            Run(() => _mediator.Send(new VerifyContributorRequestModel { ContributorId = id, ActorName = Actor() }), "verifying a contributor");

        [HttpPost("contributors/{id:guid}/reject")]
        public Task<IActionResult> Reject(Guid id, [FromBody] ReasonBody? body) =>
            Run(() => _mediator.Send(new RejectContributorRequestModel { ContributorId = id, Reason = body?.Reason, ActorName = Actor() }), "rejecting a contributor");

        // ---- topic requests (CMS inbox) ----------------------------------------------------------------------------

        public class DeclineBody { public string? Reason { get; set; } }
        public class AskDetailBody { public string? Message { get; set; } }

        [HttpGet("topic-requests")]
        public Task<IActionResult> TopicRequests([FromQuery] string? status) =>
            RunTopic(async () => await _mediator.Send(new GetTopicRequestsAdminRequestModel { Status = status }), "listing topic requests");

        [HttpGet("topic-requests/{id:guid}")]
        public Task<IActionResult> TopicRequest(Guid id) =>
            RunTopic(async () => await _mediator.Send(new GetTopicRequestsAdminRequestModel { TopicId = id }), "reading a topic request", single: true);

        // Creates a draft for the contributor to write.
        [HttpPost("topic-requests/{id:guid}/accept")]
        public Task<IActionResult> AcceptTopic(Guid id) => DecideTopic(id, DecideTopicRequestModel.Accept, null);

        [HttpPost("topic-requests/{id:guid}/decline")]
        public Task<IActionResult> DeclineTopic(Guid id, [FromBody] DeclineBody? body) => DecideTopic(id, DecideTopicRequestModel.Decline, body?.Reason);

        [HttpPost("topic-requests/{id:guid}/ask-detail")]
        public Task<IActionResult> AskTopicDetail(Guid id, [FromBody] AskDetailBody? body) => DecideTopic(id, DecideTopicRequestModel.AskDetail, body?.Message);

        private Task<IActionResult> DecideTopic(Guid id, string action, string? note) =>
            RunTopic(async () =>
            {
                var res = await _mediator.Send(new DecideTopicRequestModel { TopicId = id, Action = action, Note = note, ActorName = Actor() });
                return res.Success ? ApiResult<List<TopicRequestAdminInfo>>.Ok(new List<TopicRequestAdminInfo> { res.Data! }) : ApiResult<List<TopicRequestAdminInfo>>.Fail(res.StatusCode, res.Message!);
            }, "deciding a topic request", single: true);

        private async Task<IActionResult> RunTopic(Func<Task<ApiResult<List<TopicRequestAdminInfo>>>> send, string doing, bool single = false)
        {
            try
            {
                var res = await send();
                if (!res.Success) return StatusCode(res.StatusCode, new { message = res.Message });
                return Ok(single ? res.Data!.First() : res.Data);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while {Doing}", doing);
                return StatusCode(500, new { message = "An error occurred. Try again." });
            }
        }

        private async Task<IActionResult> Run(Func<Task<EasyHMSAPI.Application.ResponseModels.CommandResponseModels.ContributorAdminResponseModel>> send, string doing)
        {
            try
            {
                var res = await send();
                return StatusCode(res.StatusCode, res);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while {Doing}", doing);
                return StatusCode(500, new { message = "An error occurred. Try again." });
            }
        }
    }
}
