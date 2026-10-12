using EasyHMSAPI.Api.Common;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
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

        // Runs the daily upkeep now (doctor reminders, closing stale topic requests, clearing old sign-in records) and says what it did.
        // It also runs by itself once a day; this is for checking it, or for a scheduler outside the API.
        [HttpPost("maintenance/run")]
        public async Task<IActionResult> RunMaintenance()
        {
            try
            {
                var r = await _mediator.Send(new RunHealthWikiMaintenanceRequestModel());
                return Ok(new { r.RemindersSent, r.RemindersNotSent, r.TopicsClosed, r.OtpsRemoved, r.SessionsRemoved });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error running the Health Wiki upkeep");
                return StatusCode(500, new { message = "An error occurred. Try again." });
            }
        }

        // The record of WhatsApp messages the Health Wiki sent, newest first, with WhatsApp's later delivery status for each.
        [HttpGet("notifications")]
        public async Task<IActionResult> Notifications([FromQuery] int take = 50, [FromQuery] bool problemsOnly = false)
        {
            try
            {
                var r = await _mediator.Send(new GetHealthWikiNotificationsRequestModel { Take = take, ProblemsOnly = problemsOnly });
                return Ok(r.Notifications);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading the Health Wiki message record");
                return StatusCode(500, new { message = "An error occurred. Try again." });
            }
        }

        public class DeliveryBody
        {
            public string? MessageId { get; set; }
            public string? Status { get; set; }
            public string? Detail { get; set; }
        }

        // WhatsApp's status callback for one message, passed on by the WhatsApp webhook service. A message id that is not a Health Wiki
        // message answers 200 with matched=false, because that service passes on every message status it receives.
        [HttpPost("notifications/delivery")]
        public async Task<IActionResult> RecordDelivery([FromBody] DeliveryBody? body)
        {
            try
            {
                var r = await _mediator.Send(new RecordHealthWikiDeliveryRequestModel { MessageId = body?.MessageId, Status = body?.Status, Detail = body?.Detail });
                return r.Success ? Ok(new { r.Matched, r.Applied }) : StatusCode(r.StatusCode, new { message = r.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error recording a WhatsApp delivery status");
                return StatusCode(500, new { message = "An error occurred. Try again." });
            }
        }

        // ---- topic requests (CMS inbox) ----------------------------------------------------------------------------

        public class DeclineBody { public string? Reason { get; set; } }
        public class AskDetailBody { public string? Message { get; set; } }

        [HttpGet("topic-requests")]
        public async Task<IActionResult> TopicRequests([FromQuery] string? status)
        {
            try
            {
                var res = await _mediator.Send(new GetTopicRequestsAdminRequestModel { Status = status });
                return res.Success ? Ok(res.Topics) : StatusCode(res.StatusCode, new { message = res.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while listing topic requests");
                return StatusCode(500, new { message = "An error occurred. Try again." });
            }
        }

        [HttpGet("topic-requests/{id:guid}")]
        public async Task<IActionResult> TopicRequest(Guid id)
        {
            try
            {
                var res = await _mediator.Send(new GetTopicRequestsAdminRequestModel { TopicId = id });
                return res.Success ? Ok(res.Topics.First()) : StatusCode(res.StatusCode, new { message = res.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while reading a topic request");
                return StatusCode(500, new { message = "An error occurred. Try again." });
            }
        }

        public class AcceptBody
        {
            public Guid? WriterContributorId { get; set; }
            public bool ContentTeam { get; set; }
        }

        // Creates a draft for the contributor to write. When the doctor who suggested it asked for a writer, the body says who writes it
        // (writerContributorId), or contentTeam=true for the NexEagle content team, who write it in the CMS; the doctor becomes the reviewer.
        [HttpPost("topic-requests/{id:guid}/accept")]
        public Task<IActionResult> AcceptTopic(Guid id, [FromBody] AcceptBody? body) => DecideTopic(id, DecideTopicRequestModel.Accept, null, body);

        [HttpPost("topic-requests/{id:guid}/decline")]
        public Task<IActionResult> DeclineTopic(Guid id, [FromBody] DeclineBody? body) => DecideTopic(id, DecideTopicRequestModel.Decline, body?.Reason, null);

        [HttpPost("topic-requests/{id:guid}/ask-detail")]
        public Task<IActionResult> AskTopicDetail(Guid id, [FromBody] AskDetailBody? body) => DecideTopic(id, DecideTopicRequestModel.AskDetail, body?.Message, null);

        private async Task<IActionResult> DecideTopic(Guid id, string action, string? note, AcceptBody? accept)
        {
            try
            {
                var res = await _mediator.Send(new DecideTopicRequestModel { TopicId = id, Action = action, Note = note, ActorName = Actor(), WriterContributorId = accept?.WriterContributorId, ContentTeam = accept?.ContentTeam ?? false });
                return res.Success ? Ok(res.Topic) : StatusCode(res.StatusCode, new { message = res.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while deciding a topic request");
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
