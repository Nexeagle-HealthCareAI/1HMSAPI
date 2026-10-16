using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    // "My details" for the Doctor Dekho profile page. What the patient saved on their own account wins; until they save anything it is the
    // most recent hospital registration for that mobile (a number can have one per hospital, and none is authoritative, so the latest is a
    // best-effort starting point). The hospital rows themselves are never written from here: they belong to the hospital, and the patient's
    // edits go to PatientAccountProfile (see UpdatePublicPatientProfileHandler).
    public class GetPublicPatientProfileHandler : IRequestHandler<GetPublicPatientProfileRequestModel, GetPublicPatientProfileResponseModel>
    {
        private readonly AppDbContext _context;

        public GetPublicPatientProfileHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetPublicPatientProfileResponseModel> Handle(GetPublicPatientProfileRequestModel request, CancellationToken cancellationToken)
        {
            _context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

            var own = await _context.PatientAccountProfiles.FirstOrDefaultAsync(p => p.Mobile == request.Mobile, cancellationToken);
            if (own != null) return FromAccount(own);

            var patient = await _context.PatientRegistrations
                .Where(p => p.Mobile == request.Mobile)
                .OrderByDescending(p => p.RegisteredAt)
                .FirstOrDefaultAsync(cancellationToken);

            if (patient == null)
            {
                // Not an error — a number that logged in but has never actually completed a
                // booking yet (e.g. OTP-verified, then browsing before their first appointment).
                return new GetPublicPatientProfileResponseModel { Success = true, Message = "No details on file yet." };
            }

            return new GetPublicPatientProfileResponseModel
            {
                Success = true,
                FullName = patient.FullName,
                FirstName = PatientProfileRules.SplitName(null, null, patient.FullName).First,
                LastName = PatientProfileRules.SplitName(null, null, patient.FullName).Last,
                Age = patient.Age,
                AgeUnit = patient.AgeUnit,
                Sex = patient.Sex,
                Email = patient.Email,
                GuardianName = patient.GuardianName,
                GuardianRelation = patient.GuardianRelation,
                Source = "BOOKING",
            };
        }

        internal static GetPublicPatientProfileResponseModel FromAccount(EasyHMSAPI.Domain.Entities.PatientAccountProfile own) => new()
        {
            Success = true, FullName = own.FullName, FirstName = own.FirstName, LastName = own.LastName, BloodGroup = own.BloodGroup, Age = own.Age, AgeUnit = own.AgeUnit, Sex = own.Sex, Email = own.Email,
            GuardianName = own.GuardianName, GuardianRelation = own.GuardianRelation, Source = "ACCOUNT",
        };
    }
}
