using EasyHMSAPI.Application.Common;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>Shared by the match and register steps: the verified ABHA behind a request, and the
    /// patients of the hospital it is on or may be.</summary>
    internal static class AbhaPatientLookup
    {
        public static async Task<(VerifiedAbhaProfile? Profile, string Source)> VerifiedAsync(
            AppDbContext context, IAbhaLinkProofStore proofs, string? linkToken, Guid? profileShareId, Guid caller, Guid hospitalId, CancellationToken ct)
        {
            var verified = proofs.Peek(linkToken, caller, hospitalId);
            if (verified != null) return (verified, "Login");
            if (profileShareId is Guid shareId && shareId != Guid.Empty)
            {
                var share = await context.AbdmProfileShares.AsNoTracking().FirstOrDefaultAsync(
                    s => s.ProfileShareId == shareId && s.HospitalId == hospitalId, ct);
                if (share != null && !string.IsNullOrWhiteSpace(share.AbhaNumber))
                    return (new VerifiedAbhaProfile(share.AbhaNumber!, share.AbhaAddress, share.FullName, share.Gender, share.DateOfBirth, share.Mobile), "ScanShare");
            }
            return (null, string.Empty);
        }

        /// <summary>The live (not merged) patient of this hospital that already holds this ABHA number.</summary>
        public static Task<PatientRegistration?> HolderAsync(AppDbContext context, Guid hospitalId, string? abhaNumber, CancellationToken ct) =>
            AbhaPatientRules.Digits(abhaNumber).Length == 14
                ? HolderAsync(context, hospitalId, new VerifiedAbhaProfile(abhaNumber!, null, null, null, null, null), ct)
                : Task.FromResult<PatientRegistration?>(null);

        /// <summary>The live (not merged) patient of this hospital that already holds this ABHA.</summary>
        public static async Task<PatientRegistration?> HolderAsync(AppDbContext context, Guid hospitalId, VerifiedAbhaProfile abha, CancellationToken ct)
        {
            var digits = AbhaPatientRules.Digits(abha.AbhaNumber);
            var last4 = digits.Length >= 4 ? digits[^4..] : digits;
            var address = abha.AbhaAddress?.Trim().ToLower();
            var near = await context.PatientRegistrations
                .Where(p => p.HospitalId == hospitalId && p.MergedIntoPatientId == null && p.PatientId != null && p.AbhaId != null
                            && ((last4.Length == 4 && p.AbhaId.EndsWith(last4)) || (address != null && p.AbhaId.ToLower() == address)))
                .ToListAsync(ct);
            return near.Where(p => AbhaPatientRules.SameAbha(p.AbhaId, abha.AbhaNumber, abha.AbhaAddress))
                       .OrderBy(p => p.RegisteredAt ?? DateTime.MaxValue)
                       .FirstOrDefault();
        }
    }

    /// <summary>Before registering from an ABHA: is it already on a patient here (VRFY_ABHA_308), and
    /// which patients may be this person (CRT_ABHA_116). Reads the link proof without using it up.</summary>
    public class MatchAbhaPatientHandler : IRequestHandler<MatchAbhaPatientRequestModel, MatchAbhaPatientResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IAbhaLinkProofStore _proofs;

        public MatchAbhaPatientHandler(AppDbContext context, IAbhaLinkProofStore proofs)
        {
            _context = context;
            _proofs = proofs;
        }

        public async Task<MatchAbhaPatientResponseModel> Handle(MatchAbhaPatientRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty || request.CallerUserId == Guid.Empty)
                return new MatchAbhaPatientResponseModel { Success = false, Message = "Hospital and signed-in user are required." };
            if (!await CallerGuards.IsHospitalMemberAsync(_context, request.CallerUserId, request.HospitalId, cancellationToken))
                return new MatchAbhaPatientResponseModel { Success = false, Message = "You don't have access to this hospital." };

            var (abha, _) = await AbhaPatientLookup.VerifiedAsync(_context, _proofs, request.LinkToken, request.ProfileShareId, request.CallerUserId, request.HospitalId, cancellationToken);
            if (abha == null)
                return new MatchAbhaPatientResponseModel { Success = false, Message = "Verify the ABHA with an OTP first. The verification has expired or was already used." };

            var holder = await AbhaPatientLookup.HolderAsync(_context, request.HospitalId, abha, cancellationToken);
            var response = new MatchAbhaPatientResponseModel
            {
                Success = true,
                AlreadyLinked = holder == null ? null : AbhaPatientRules.Summary(holder, new[] { "ABHA" }),
                Message = holder == null ? null : AbhaPatientRules.AlreadyLinkedMessage
            };

            // Narrow in SQL on the mobile or the start of the name, then score.
            var mobile = AbhaPatientRules.Digits(abha.Mobile);
            mobile = mobile.Length > 10 ? mobile[^10..] : mobile;
            var prefix = FuzzyMatch.Normalize(abha.FullName);
            prefix = prefix.Length >= 3 ? prefix[..3] : prefix;
            var pool = await _context.PatientRegistrations.AsNoTracking()
                .Where(p => p.HospitalId == request.HospitalId && p.MergedIntoPatientId == null && p.PatientId != null
                            && (holder == null || p.PatientId != holder.PatientId)
                            && ((mobile.Length == 10 && p.Mobile != null && p.Mobile.EndsWith(mobile))
                                || (prefix.Length > 0 && p.FullName != null && p.FullName.ToLower().StartsWith(prefix))))
                .Take(200)
                .ToListAsync(cancellationToken);
            var today = DateTime.UtcNow.Date;
            response.Candidates = pool
                .Select(p => (p, why: AbhaPatientRules.MatchReasons(p, abha, today)))
                .Where(x => x.why != null)
                .OrderByDescending(x => x.why!.Count)
                .ThenByDescending(x => FuzzyMatch.JaroWinkler(abha.FullName, x.p.FullName))
                .Take(10)
                .Select(x => AbhaPatientRules.Summary(x.p, x.why))
                .ToList();
            return response;
        }
    }

    /// <summary>
    /// Registers a patient from a verified ABHA, or puts it on an existing patient, and records the
    /// ABHA account against that patient. One ABHA per patient and one patient per ABHA: an ABHA
    /// already on another patient is refused with that patient's ID and nothing is saved (VRFY_ABHA_308,
    /// TAGGING_UNIQUEPATIENTID_UNIQUEABHANUMBER). The link proof is used up only when the save succeeds.
    /// </summary>
    public class RegisterAbhaPatientHandler : IRequestHandler<RegisterAbhaPatientRequestModel, RegisterAbhaPatientResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IAbhaLinkProofStore _proofs;

        public RegisterAbhaPatientHandler(AppDbContext context, IAbhaLinkProofStore proofs)
        {
            _context = context;
            _proofs = proofs;
        }

        public async Task<RegisterAbhaPatientResponseModel> Handle(RegisterAbhaPatientRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty || request.CallerUserId == Guid.Empty)
                return Fail("Hospital and signed-in user are required.");
            if (!await CallerGuards.IsHospitalMemberAsync(_context, request.CallerUserId, request.HospitalId, cancellationToken))
                return Fail("You don't have access to this hospital.");

            var (abha, source) = await AbhaPatientLookup.VerifiedAsync(_context, _proofs, request.LinkToken, request.ProfileShareId, request.CallerUserId, request.HospitalId, cancellationToken);
            if (abha == null)
                return Fail("Verify the ABHA with an OTP first. The verification has expired or was already used.");
            if (AbhaPatientRules.Digits(abha.AbhaNumber).Length != 14)
                return Fail("This ABHA has no 14 digit ABHA number, so it cannot be put on a patient record.");

            var mobile = AbhaPatientRules.Digits(request.Mobile);
            if (mobile.Length > 0 && !System.Text.RegularExpressions.Regex.IsMatch(mobile.Length > 10 ? mobile[^10..] : mobile, "^[6-9][0-9]{9}$"))
                return Fail("Please enter a valid mobile number.");

            var holder = await AbhaPatientLookup.HolderAsync(_context, request.HospitalId, abha, cancellationToken);
            var wanted = string.IsNullOrWhiteSpace(request.PatientId) ? null : request.PatientId.Trim();
            if (holder != null && !string.Equals(holder.PatientId, wanted, StringComparison.OrdinalIgnoreCase))
                return new RegisterAbhaPatientResponseModel
                {
                    Success = false,
                    Message = $"{AbhaPatientRules.AlreadyLinkedMessage} ({holder.PatientId})",
                    AlreadyLinkedPatientId = holder.PatientId
                };

            var today = DateTime.UtcNow.Date;
            PatientRegistration patient;
            var created = false;
            if (wanted != null)
            {
                var found = await _context.PatientRegistrations.FirstOrDefaultAsync(
                    p => p.HospitalId == request.HospitalId && p.PatientId == wanted && p.MergedIntoPatientId == null, cancellationToken);
                if (found == null)
                    return Fail($"Patient {wanted} was not found in this hospital.");
                if (!string.IsNullOrWhiteSpace(found.AbhaId) && !AbhaPatientRules.SameAbha(found.AbhaId, abha.AbhaNumber, abha.AbhaAddress))
                    return Fail($"Patient {wanted} already holds another ABHA. One patient can hold only one ABHA number.");
                patient = found;
            }
            else
            {
                patient = new PatientRegistration
                {
                    RegistrationId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    PatientId = await AppointmentBookingHelpers.GenerateNewPatientIdAsync(_context),
                    RegisteredAt = DateTime.UtcNow,
                    RegisteredBy = request.CallerUserId,
                    AgeUnit = "Y",
                    Age = 0,
                    Country = "India"
                };
                _context.PatientRegistrations.Add(patient);
                created = true;
            }

            // A new record starts from what the ABHA holds; staff may have changed any editable field.
            static string? Or(string? typed, string? fromAbha) => string.IsNullOrWhiteSpace(typed) ? fromAbha : typed;
            var form = created
                ? new AbhaPatientRules.EditableFields(Or(request.Mobile, abha.Mobile), Or(request.Email, abha.Email), Or(request.AddressLine, abha.Address),
                    Or(request.City, abha.City), Or(request.District, abha.District), Or(request.State, abha.State), Or(request.Pincode, abha.Pincode))
                : new AbhaPatientRules.EditableFields(request.Mobile, request.Email, request.AddressLine, request.City, request.District, request.State, request.Pincode);
            AbhaPatientRules.Apply(patient, abha, form, today);

            var digits = AbhaPatientRules.Digits(abha.AbhaNumber);
            var accounts = await _context.AbhaAccount.Where(a => a.HospitalId == request.HospitalId).ToListAsync(cancellationToken);
            var account = accounts.FirstOrDefault(a => AbhaPatientRules.Digits(a.AbhaNumber) == digits);
            if (account == null)
            {
                account = new AbhaAccount
                {
                    AbhaAccountId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    AbhaNumber = abha.AbhaNumber,
                    Source = source,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = request.LoggedInUserName
                };
                _context.AbhaAccount.Add(account);
            }
            static string? Keep(string? now, string? before) => string.IsNullOrWhiteSpace(now) ? before : now;
            account.AbhaAddress = Keep(abha.AbhaAddress, account.AbhaAddress);
            account.FullName = Keep(abha.FullName, account.FullName);
            account.Gender = Keep(abha.Gender, account.Gender);
            account.DateOfBirth = Keep(abha.DateOfBirth, account.DateOfBirth);
            account.Mobile = Keep(abha.Mobile, account.Mobile);
            account.Email = Keep(abha.Email, account.Email);
            account.LinkedPatientId = patient.PatientId;

            await _context.SaveChangesAsync(cancellationToken);
            _proofs.Consume(request.LinkToken, request.CallerUserId, request.HospitalId);

            return new RegisterAbhaPatientResponseModel
            {
                Success = true,
                Created = created,
                PatientId = patient.PatientId,
                AbhaAccountId = account.AbhaAccountId,
                Message = created
                    ? $"Patient registered as {patient.PatientId} with this ABHA."
                    : holder != null ? $"Patient {patient.PatientId} already holds this ABHA. Details refreshed." : $"ABHA linked to patient {patient.PatientId}."
            };
        }

        private static RegisterAbhaPatientResponseModel Fail(string message) => new() { Success = false, Message = message };
    }
}
