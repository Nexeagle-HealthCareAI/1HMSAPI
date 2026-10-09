using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// A contributor fills in their profile after the first sign-in. INVITED becomes PENDING (waiting for the team to check
    /// the registration). A VERIFIED doctor who changes the registration number, council or year goes back to PENDING,
    /// so the badge never carries a registration nobody checked.
    /// </summary>
    public class RegisterContributorHandler : IRequestHandler<RegisterContributorRequestModel, ApiResult<ContributorMeInfo>>
    {
        private readonly AppDbContext _context;

        public RegisterContributorHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResult<ContributorMeInfo>> Handle(RegisterContributorRequestModel r, CancellationToken ct)
        {
            var c = await _context.HealthWikiContributors.FirstOrDefaultAsync(x => x.ContributorId == r.ContributorId, ct);
            if (c == null) return ApiResult<ContributorMeInfo>.Fail(404, "Profile not found.");
            if (c.Status == HealthWikiContributor.StatusRejected) return ApiResult<ContributorMeInfo>.Fail(403, "This account cannot be changed.");
            if (c.Type is HealthWikiContributor.TypeHospitalDoctor or HealthWikiContributor.TypeStaff)
                return ApiResult<ContributorMeInfo>.Fail(403, "Hospital doctors and NexEagle staff use the EasyHMS app and the CMS.");

            var doctor = HealthArticleRules.IsDoctorType(c.Type);
            var name = r.FullName?.Trim();
            if (string.IsNullOrEmpty(name)) return ApiResult<ContributorMeInfo>.Fail(400, "Enter your full name.");
            if (name.Length > 200) return ApiResult<ContributorMeInfo>.Fail(400, "Your name is too long (max 200 characters).");
            if (!r.Consent) return ApiResult<ContributorMeInfo>.Fail(400, "Please confirm the consent.");
            if (!ContributorPortalRules.IsHttpsOrEmpty(r.PhotoUrl)) return ApiResult<ContributorMeInfo>.Fail(400, "The photo must be an https link.");
            var bio = Clean(r.Bio, 1000);
            if (r.Bio != null && bio == null && r.Bio.Trim().Length > 1000) return ApiResult<ContributorMeInfo>.Fail(400, "The bio is too long (max 1000 characters).");

            int? year = null;
            string? regNo = null, council = null, speciality = null, qualification = null;
            if (doctor)
            {
                regNo = Clean(r.RegistrationNumber, 50);
                council = Clean(r.RegistrationCouncil, 150);
                speciality = Clean(r.Speciality, 150);
                qualification = Clean(r.Qualification, 200);
                if (regNo == null || council == null) return ApiResult<ContributorMeInfo>.Fail(400, "Registration number and council are required for doctors.");
                if (speciality == null || qualification == null) return ApiResult<ContributorMeInfo>.Fail(400, "Enter your speciality and qualification.");
                if (!ContributorPortalRules.TryParseRegistrationYear(r.RegistrationYear, out var y))
                    return ApiResult<ContributorMeInfo>.Fail(400, $"Enter the year of registration ({ContributorPortalRules.MinRegistrationYear} to {DateTime.UtcNow.Year}).");
                year = y;
            }
            else if (Clean(r.RoleTitle, 150) == null)
            {
                return ApiResult<ContributorMeInfo>.Fail(400, "Enter your job title.");
            }

            var registrationChanged = doctor && (c.RegistrationNumber != regNo || c.RegistrationCouncil != council || c.RegistrationYear != year);
            var now = DateTime.UtcNow;

            c.FullName = name;
            c.PhotoUrl = Clean(r.PhotoUrl, 500);
            c.Bio = bio;
            c.Speciality = speciality;
            c.Qualification = qualification;
            c.RegistrationNumber = regNo;
            c.RegistrationCouncil = council;
            c.RegistrationYear = year;
            c.RoleTitle = doctor ? null : Clean(r.RoleTitle, 150);
            c.Organisation = doctor ? null : Clean(r.Organisation, 200);
            c.FieldOfWork = doctor ? null : Clean(r.FieldOfWork, 300);
            c.ConsentAt = now;
            c.ConsentVersion = ContributorPortalRules.ConsentVersion;
            c.UpdatedAt = now;

            if (c.Status == HealthWikiContributor.StatusInvited)
                c.Status = HealthWikiContributor.StatusPending;
            else if (c.Status == HealthWikiContributor.StatusVerified && registrationChanged)
            {
                c.Status = HealthWikiContributor.StatusPending;
                c.VerifiedAt = null;
                c.VerifiedBy = null;
            }

            HealthWikiAuditLog.Add(_context, HealthWikiAudit.EntityContributor, c.ContributorId, "REGISTERED", HealthWikiAudit.ActorContributor, c.FullName,
                registrationChanged && c.Status == HealthWikiContributor.StatusPending ? "Registration details changed" : null);
            await _context.SaveChangesAsync(ct);
            return ApiResult<ContributorMeInfo>.Ok(ContributorPortalMapper.ToMe(c));
        }

        private static string? Clean(string? value, int max)
        {
            var v = value?.Trim();
            return string.IsNullOrEmpty(v) || v.Length > max ? null : v;
        }
    }

    public class GetContributorMeHandler : IRequestHandler<GetContributorMeRequestModel, ApiResult<ContributorMeInfo>>
    {
        private readonly AppDbContext _context;

        public GetContributorMeHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ApiResult<ContributorMeInfo>> Handle(GetContributorMeRequestModel request, CancellationToken ct)
        {
            var c = await _context.HealthWikiContributors.AsNoTracking().FirstOrDefaultAsync(x => x.ContributorId == request.ContributorId, ct);
            return c == null ? ApiResult<ContributorMeInfo>.Fail(404, "Profile not found.") : ApiResult<ContributorMeInfo>.Ok(ContributorPortalMapper.ToMe(c));
        }
    }
}
