using EasyHMSAPI.Api.Common;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace EasyHMSAPI.Api.Controllers
{
    /// <summary>
    /// Health Wiki contributors (independent doctors, health workers, writers): invitation link, WhatsApp code sign-in.
    /// Called by the Doctor Dekho server on behalf of the contributor pages; the browser never calls it directly.
    /// Mobile numbers are never returned in full. Everything after sign-in sits behind ContributorSessionFilter, and the
    /// contributor id always comes from the session, never from the request.
    /// </summary>
    [ExcludeFromCodeCoverage]
    [ApiController]
    [Route("contributor")]
    [AllowAnonymous]
    [SkipHospitalAccessCheck]
    [EnableRateLimiting("ContributorAuthPolicy")]
    public class ContributorController : ControllerBase
    {
        private readonly IMediator _mediator;
        private readonly ILogger<ContributorController> _logger;

        public ContributorController(IMediator mediator, ILogger<ContributorController> logger)
        {
            _mediator = mediator;
            _logger = logger;
        }

        public class OtpSendBody
        {
            public string? Mobile { get; set; }
            public string? InviteToken { get; set; }
        }

        public class OtpVerifyBody
        {
            public string? Mobile { get; set; }
            public string? Code { get; set; }
            public string? InviteToken { get; set; }
            public string? Role { get; set; }
        }

        [HttpGet("invite/{token}")]
        public async Task<IActionResult> Invite(string token)
        {
            try
            {
                var res = await _mediator.Send(new GetContributorInviteRequestModel { Token = token });
                return res.Success ? Ok(res) : StatusCode(res.StatusCode, new { message = res.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading a contributor invitation");
                return StatusCode(500, new { message = "Something went wrong. Try again." });
            }
        }

        // "Join as a contributor" without an invitation: the same code step as sign-in.
        [HttpPost("enrol")]
        [HttpPost("otp/send")]
        public async Task<IActionResult> SendOtp([FromBody] OtpSendBody body)
        {
            try
            {
                var res = await _mediator.Send(new ContributorOtpSendRequestModel
                {
                    Mobile = body?.Mobile, InviteToken = body?.InviteToken, RequestIp = HttpContext.Connection.RemoteIpAddress?.ToString(),
                });
                if (res.RetryAfterSeconds.HasValue) Response.Headers["Retry-After"] = res.RetryAfterSeconds.Value.ToString();
                return res.Success ? Ok(res) : StatusCode(res.StatusCode, new { message = res.Message, retryAfterSeconds = res.RetryAfterSeconds });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending a contributor code");
                return StatusCode(500, new { message = "Something went wrong. Try again." });
            }
        }

        [HttpPost("otp/verify")]
        public async Task<IActionResult> VerifyOtp([FromBody] OtpVerifyBody body)
        {
            try
            {
                var res = await _mediator.Send(new ContributorOtpVerifyRequestModel
                {
                    Mobile = body?.Mobile, Code = body?.Code, InviteToken = body?.InviteToken, Role = body?.Role,
                });
                return res.Success ? Ok(res) : StatusCode(res.StatusCode, new { message = res.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying a contributor code");
                return StatusCode(500, new { message = "Something went wrong. Try again." });
            }
        }

        [HttpPost("logout")]
        [ServiceFilter(typeof(ContributorSessionFilter))]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var sessionId = ContributorSessionFilter.SessionId(HttpContext);
                if (sessionId.HasValue) await _mediator.Send(new ContributorLogoutRequestModel { SessionId = sessionId.Value });
                return Ok(new { ok = true });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error signing a contributor out");
                return StatusCode(500, new { message = "Something went wrong. Try again." });
            }
        }

        // ---- after sign-in -----------------------------------------------------------------------------------------

        public class RegisterBody
        {
            public string? FullName { get; set; }
            public string? PhotoUrl { get; set; }
            public string? Bio { get; set; }
            public string? Speciality { get; set; }
            public string? Qualification { get; set; }
            public string? RegistrationNumber { get; set; }
            public string? RegistrationCouncil { get; set; }
            public string? RegistrationYear { get; set; }
            public string? RoleTitle { get; set; }
            public string? Organisation { get; set; }
            public string? FieldOfWork { get; set; }
            public bool Consent { get; set; }
        }

        public class ArticleBody
        {
            public string? Type { get; set; }
            public string? Title { get; set; }
            public string? Description { get; set; }
            public string? Content { get; set; }
            public string? CoverImageUrl { get; set; }
            public string? CoverImageAlt { get; set; }
            public string? RelatedConditionSlug { get; set; }
            public string? Disclosure { get; set; }
            public string? References { get; set; }
        }

        public class ReviewBody
        {
            public string? Decision { get; set; }
            public string? Comment { get; set; }
            public bool AccuracyConfirmed { get; set; }
        }

        public class TopicBody
        {
            public string? Title { get; set; }
            public string? Type { get; set; }
            public string? Outline { get; set; }
            public string? WhyItMatters { get; set; }
            public string? ConditionSlug { get; set; }
            public string? References { get; set; }
        }

        private Guid Me() => ContributorSessionFilter.ContributorId(HttpContext)!.Value;

        private async Task<IActionResult> Reply<T>(Func<Task<ApiResult<T>>> send, string doing)
        {
            try
            {
                var res = await send();
                return res.Success ? StatusCode(res.StatusCode, res.Data) : StatusCode(res.StatusCode, new { message = res.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while {Doing}", doing);
                return StatusCode(500, new { message = "Something went wrong. Try again." });
            }
        }

        [HttpPost("register")]
        [ServiceFilter(typeof(ContributorSessionFilter))]
        public Task<IActionResult> Register([FromBody] RegisterBody body) => Reply(() => _mediator.Send(new RegisterContributorRequestModel
        {
            ContributorId = Me(), FullName = body?.FullName, PhotoUrl = body?.PhotoUrl, Bio = body?.Bio, Speciality = body?.Speciality,
            Qualification = body?.Qualification, RegistrationNumber = body?.RegistrationNumber, RegistrationCouncil = body?.RegistrationCouncil,
            RegistrationYear = body?.RegistrationYear, RoleTitle = body?.RoleTitle, Organisation = body?.Organisation,
            FieldOfWork = body?.FieldOfWork, Consent = body?.Consent ?? false,
        }), "saving a contributor profile");

        [HttpGet("me")]
        [ServiceFilter(typeof(ContributorSessionFilter))]
        public Task<IActionResult> GetMe() => Reply(() => _mediator.Send(new GetContributorMeRequestModel { ContributorId = Me() }), "reading a contributor profile");

        [HttpGet("articles")]
        [ServiceFilter(typeof(ContributorSessionFilter))]
        public Task<IActionResult> Articles() => Reply(() => _mediator.Send(new GetContributorArticlesRequestModel { ContributorId = Me() }), "listing a contributor's articles");

        [HttpGet("articles/{slug}")]
        [ServiceFilter(typeof(ContributorSessionFilter))]
        public Task<IActionResult> Article(string slug) => Reply(() => _mediator.Send(new GetContributorArticleRequestModel { ContributorId = Me(), Slug = slug }), "reading a contributor's article");

        [HttpPost("articles")]
        [ServiceFilter(typeof(ContributorSessionFilter))]
        public Task<IActionResult> CreateArticle([FromBody] ArticleBody body) => Reply(() => _mediator.Send(ToSave(null, body)), "creating an article");

        [HttpPatch("articles/{slug}")]
        [ServiceFilter(typeof(ContributorSessionFilter))]
        public Task<IActionResult> UpdateArticle(string slug, [FromBody] ArticleBody body) => Reply(() => _mediator.Send(ToSave(slug, body)), "editing an article");

        [HttpPost("articles/{slug}/submit")]
        [ServiceFilter(typeof(ContributorSessionFilter))]
        public Task<IActionResult> SubmitArticle(string slug) => Reply(() => _mediator.Send(new SubmitContributorArticleRequestModel { ContributorId = Me(), Slug = slug }), "submitting an article");

        [HttpPost("articles/{slug}/review")]
        [ServiceFilter(typeof(ContributorSessionFilter))]
        public Task<IActionResult> ReviewArticle(string slug, [FromBody] ReviewBody body) => Reply(() => _mediator.Send(new ReviewContributorArticleRequestModel
        {
            ContributorId = Me(), Slug = slug, Decision = body?.Decision, Comment = body?.Comment, AccuracyConfirmed = body?.AccuracyConfirmed ?? false,
        }), "reviewing an article");

        [HttpGet("topics")]
        [ServiceFilter(typeof(ContributorSessionFilter))]
        public Task<IActionResult> Topics() => Reply(() => _mediator.Send(new GetContributorTopicsRequestModel { ContributorId = Me() }), "listing topics");

        [HttpPost("topics")]
        [ServiceFilter(typeof(ContributorSessionFilter))]
        public Task<IActionResult> CreateTopic([FromBody] TopicBody body) => Reply(() => _mediator.Send(new CreateContributorTopicRequestModel
        {
            ContributorId = Me(), Title = body?.Title, Type = body?.Type, Outline = body?.Outline, WhyItMatters = body?.WhyItMatters,
            ConditionSlug = body?.ConditionSlug, References = body?.References,
        }), "creating a topic");

        // Only the fields sent are changed.
        [HttpPatch("topics/{id:guid}")]
        [ServiceFilter(typeof(ContributorSessionFilter))]
        public Task<IActionResult> AddTopicDetail(Guid id, [FromBody] JsonElement body) => Reply(() =>
        {
            var provided = body.ValueKind == JsonValueKind.Object ? body.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal) : new HashSet<string>();
            string? Text(string key) => body.ValueKind == JsonValueKind.Object && body.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            return _mediator.Send(new AddContributorTopicDetailRequestModel
            {
                ContributorId = Me(), TopicId = id, Outline = Text("outline"), WhyItMatters = Text("whyItMatters"),
                ConditionSlug = Text("conditionSlug"), References = Text("references"), Provided = provided,
            });
        }, "adding topic detail");

        private SaveContributorArticleRequestModel ToSave(string? slug, ArticleBody? b) => new()
        {
            ContributorId = Me(), Slug = slug, Type = b?.Type, Title = b?.Title, Description = b?.Description, Content = b?.Content,
            CoverImageUrl = b?.CoverImageUrl, CoverImageAlt = b?.CoverImageAlt, RelatedConditionSlug = b?.RelatedConditionSlug,
            Disclosure = b?.Disclosure, References = b?.References,
        };
    }
}
