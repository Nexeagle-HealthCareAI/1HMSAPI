using EasyHMSAPI.Application.Common;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class RecordAbhaConsentHandler : IRequestHandler<RecordAbhaConsentRequestModel, RecordAbhaConsentResponseModel>
    {
        private readonly AppDbContext _context;

        public RecordAbhaConsentHandler(AppDbContext context) => _context = context;

        public async Task<RecordAbhaConsentResponseModel> Handle(RecordAbhaConsentRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty || request.CallerUserId == Guid.Empty)
                return new RecordAbhaConsentResponseModel { Success = false, Message = "Hospital and signed-in user are required." };

            // The API filter checks membership when a hospitalId is present; this is the inline guarantee for this record.
            if (!await CallerGuards.IsHospitalMemberAsync(_context, request.CallerUserId, request.HospitalId, cancellationToken))
                return new RecordAbhaConsentResponseModel { Success = false, Message = "You don't have access to this hospital." };

            if (!string.Equals(request.ConsentVersion, AbhaConsentRules.Version, StringComparison.Ordinal))
                return new RecordAbhaConsentResponseModel { Success = false, Message = "The consent wording on screen is out of date. Reload and read the current wording." };

            var givenBy = request.GivenBy?.Trim().ToUpperInvariant();
            if (givenBy != "PATIENT" && givenBy != "GUARDIAN")
                return new RecordAbhaConsentResponseModel { Success = false, Message = "State whether the consent was given by the patient or a guardian." };

            var subject = string.IsNullOrWhiteSpace(request.SubjectName) ? null : request.SubjectName.Trim();
            if (subject is { Length: > 200 }) subject = subject[..200];

            // Nothing is sent until the declarations and both confirmations are in (CRT_ABHA_102).
            var ticked = request.Declarations ?? Array.Empty<bool>();
            if (ticked.Length != AbhaConsentRules.Declarations.Length)
                return new RecordAbhaConsentResponseModel { Success = false, Message = "Tick each declaration of the consent." };
            if (ticked[1])
                return new RecordAbhaConsentResponseModel { Success = false, Message = "Creating an ABHA with a document other than Aadhaar is not offered here. Untick the second declaration to go on with Aadhaar." };
            if (!ticked.Where((t, i) => AbhaConsentRules.AadhaarFlowDeclarations[i]).All(t => t))
                return new RecordAbhaConsentResponseModel { Success = false, Message = "Tick each declaration of the consent." };
            if (!request.WorkerConfirmed || !request.BeneficiaryConfirmed)
                return new RecordAbhaConsentResponseModel { Success = false, Message = "Both confirmations are needed: the healthcare worker's and the beneficiary's." };

            var now = DateTime.UtcNow;
            var consent = new AbhaConsent
            {
                AbhaConsentId = Guid.NewGuid(),
                HospitalId = request.HospitalId,
                GrantedByUserId = request.CallerUserId,
                GrantedByName = request.LoggedInUserName,
                PurposeCode = AbhaConsentRules.PurposeEnrolment,
                ConsentCode = AbhaConsentRules.Code,
                ConsentVersion = AbhaConsentRules.Version,
                ConsentTextSha256 = AbhaConsentRules.TextSha256(),
                ConsentTextSnapshot = AbhaConsentRules.Record(ticked, request.LoggedInUserName, subject, now),
                GivenBy = givenBy,
                SubjectName = subject,
                CreatedAt = now,
            };
            _context.AbhaConsent.Add(consent);
            await _context.SaveChangesAsync(cancellationToken);

            return new RecordAbhaConsentResponseModel
            {
                Success = true,
                Message = "Consent recorded.",
                ConsentId = consent.AbhaConsentId,
                ExpiresAt = now + AbhaConsentRules.Lifetime,
                RecordedAt = now,
                RecordedBy = request.LoggedInUserName,
            };
        }
    }
}
