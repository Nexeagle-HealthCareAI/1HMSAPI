using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>"Write an article" in EasyHMS: make the doctor's one-time Doctor Dekho address.</summary>
    public class CreateHealthWikiWriteHandoffHandler : IRequestHandler<CreateHealthWikiWriteHandoffRequestModel, CreateHealthWikiWriteHandoffResponseModel>
    {
        private readonly IHealthWikiStaffService _staff;
        private readonly IHealthWikiHandoffService _handoff;
        private readonly IConfiguration _configuration;

        public CreateHealthWikiWriteHandoffHandler(IHealthWikiStaffService staff, IHealthWikiHandoffService handoff, IConfiguration configuration)
        {
            _staff = staff;
            _handoff = handoff;
            _configuration = configuration;
        }

        public async Task<CreateHealthWikiWriteHandoffResponseModel> Handle(CreateHealthWikiWriteHandoffRequestModel request, CancellationToken ct)
        {
            var baseUrl = ContributorInvites.BaseUrl(_configuration);
            if (baseUrl == null) return CreateHealthWikiWriteHandoffResponseModel.Fail(503, "Writing on Doctor Dekho is not available right now.");

            var me = await _staff.EnsureContributorAsync(request.UserId, ct);
            if (me == null) return CreateHealthWikiWriteHandoffResponseModel.Fail(403, "Only doctors can use the Health Wiki.");
            if (me.Status == HealthWikiContributor.StatusRejected) return CreateHealthWikiWriteHandoffResponseModel.Fail(403, "Your Health Wiki access has been removed.");

            var code = await _handoff.CreateAsync(me.ContributorId, ct);
            if (code == null) return CreateHealthWikiWriteHandoffResponseModel.Fail(403, "You cannot write articles from here.");

            var to = string.Equals(request.To, "drafts", StringComparison.OrdinalIgnoreCase) ? "drafts" : "write";
            return new CreateHealthWikiWriteHandoffResponseModel { Success = true, StatusCode = 200, Url = $"{baseUrl}/contribute/sso?code={Uri.EscapeDataString(code)}&to={to}" };
        }
    }

    /// <summary>Doctor Dekho's server swaps the one-time code for a session (the same kind a WhatsApp sign-in gives).</summary>
    public class ContributorSsoExchangeHandler : IRequestHandler<ContributorSsoExchangeRequestModel, ContributorOtpVerifyResponseModel>
    {
        private readonly IHealthWikiHandoffService _handoff;

        public ContributorSsoExchangeHandler(IHealthWikiHandoffService handoff)
        {
            _handoff = handoff;
        }

        public async Task<ContributorOtpVerifyResponseModel> Handle(ContributorSsoExchangeRequestModel request, CancellationToken ct)
        {
            var session = await _handoff.ExchangeAsync(request.Code, ct);
            if (session == null)
                return new ContributorOtpVerifyResponseModel { Success = false, StatusCode = 401, Message = "This link has expired. Go back to EasyHMS and choose Write an article again." };
            return new ContributorOtpVerifyResponseModel { Success = true, StatusCode = 200, SessionToken = session.Token, SessionExpiresAt = session.ExpiresAt, NeedsRegistration = false };
        }
    }

    /// <summary>A hospital doctor's topic requests in EasyHMS. The same rules as the Doctor Dekho pages (IHealthWikiTopicService).</summary>
    public class GetHealthWikiStaffTopicsHandler : IRequestHandler<GetHealthWikiStaffTopicsRequestModel, GetContributorTopicsResponseModel>
    {
        private readonly IHealthWikiStaffService _staff;
        private readonly IHealthWikiTopicService _topics;

        public GetHealthWikiStaffTopicsHandler(IHealthWikiStaffService staff, IHealthWikiTopicService topics)
        {
            _staff = staff;
            _topics = topics;
        }

        public async Task<GetContributorTopicsResponseModel> Handle(GetHealthWikiStaffTopicsRequestModel request, CancellationToken ct)
        {
            var me = await _staff.EnsureContributorAsync(request.UserId, ct);
            if (me == null) return GetContributorTopicsResponseModel.Fail(403, "Only doctors can use the Health Wiki.");
            return await _topics.ListAsync(me.ContributorId, ct);
        }
    }

    public class CreateHealthWikiStaffTopicHandler : IRequestHandler<CreateHealthWikiStaffTopicRequestModel, ContributorTopicResponseModel>
    {
        private readonly IHealthWikiStaffService _staff;
        private readonly IHealthWikiTopicService _topics;

        public CreateHealthWikiStaffTopicHandler(IHealthWikiStaffService staff, IHealthWikiTopicService topics)
        {
            _staff = staff;
            _topics = topics;
        }

        public async Task<ContributorTopicResponseModel> Handle(CreateHealthWikiStaffTopicRequestModel r, CancellationToken ct)
        {
            var me = await _staff.EnsureContributorAsync(r.UserId, ct);
            if (me == null) return ContributorTopicResponseModel.Fail(403, "Only doctors can use the Health Wiki.");
            return await _topics.CreateAsync(new CreateContributorTopicRequestModel
            {
                ContributorId = me.ContributorId, Title = r.Title, Type = r.Type, Outline = r.Outline, WhyItMatters = r.WhyItMatters,
                ConditionSlug = r.ConditionSlug, References = r.References, RequestWriter = r.RequestWriter,
            }, ct);
        }
    }

    public class AddHealthWikiStaffTopicDetailHandler : IRequestHandler<AddHealthWikiStaffTopicDetailRequestModel, ContributorTopicResponseModel>
    {
        private readonly IHealthWikiStaffService _staff;
        private readonly IHealthWikiTopicService _topics;

        public AddHealthWikiStaffTopicDetailHandler(IHealthWikiStaffService staff, IHealthWikiTopicService topics)
        {
            _staff = staff;
            _topics = topics;
        }

        public async Task<ContributorTopicResponseModel> Handle(AddHealthWikiStaffTopicDetailRequestModel r, CancellationToken ct)
        {
            var me = await _staff.EnsureContributorAsync(r.UserId, ct);
            if (me == null) return ContributorTopicResponseModel.Fail(403, "Only doctors can use the Health Wiki.");
            return await _topics.AddDetailAsync(new AddContributorTopicDetailRequestModel
            {
                ContributorId = me.ContributorId, TopicId = r.TopicId, Outline = r.Outline, WhyItMatters = r.WhyItMatters,
                ConditionSlug = r.ConditionSlug, References = r.References, Provided = r.Provided,
            }, ct);
        }
    }

    /// <summary>The contributor tag for the signed-in patient's profile page.</summary>
    public class GetContributorProfileTagHandler : IRequestHandler<GetContributorProfileTagRequestModel, ContributorProfileTagResponseModel>
    {
        private readonly IHealthWikiHandoffService _people;

        public GetContributorProfileTagHandler(IHealthWikiHandoffService people)
        {
            _people = people;
        }

        public async Task<ContributorProfileTagResponseModel> Handle(GetContributorProfileTagRequestModel request, CancellationToken ct)
        {
            var c = await _people.FindForProfileAsync(request.Mobile, ct);
            return c == null
                ? new ContributorProfileTagResponseModel { IsContributor = false }
                : new ContributorProfileTagResponseModel { IsContributor = true, Role = c.Type, Status = c.Status, FullName = string.IsNullOrWhiteSpace(c.FullName) ? null : c.FullName };
        }
    }

    public class StartContributorSessionFromPatientHandler : IRequestHandler<StartContributorSessionFromPatientRequestModel, ContributorOtpVerifyResponseModel>
    {
        private readonly IHealthWikiHandoffService _people;

        public StartContributorSessionFromPatientHandler(IHealthWikiHandoffService people)
        {
            _people = people;
        }

        public async Task<ContributorOtpVerifyResponseModel> Handle(StartContributorSessionFromPatientRequestModel request, CancellationToken ct)
        {
            var session = await _people.StartSessionByVerifiedMobileAsync(request.Mobile, ct);
            return session == null
                ? new ContributorOtpVerifyResponseModel { Success = false, StatusCode = 404, Message = "You have not joined the Health Wiki as a contributor yet." }
                : new ContributorOtpVerifyResponseModel { Success = true, StatusCode = 200, SessionToken = session.Token, SessionExpiresAt = session.ExpiresAt, NeedsRegistration = false };
        }
    }
}
