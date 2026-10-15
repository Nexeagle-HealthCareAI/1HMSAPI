using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>The patient saves their own details on Doctor Dekho. Written to their account profile only; hospital registrations are untouched.</summary>
    public class UpdatePublicPatientProfileHandler : IRequestHandler<UpdatePublicPatientProfileRequestModel, GetPublicPatientProfileResponseModel>
    {
        private readonly AppDbContext _context;

        public UpdatePublicPatientProfileHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetPublicPatientProfileResponseModel> Handle(UpdatePublicPatientProfileRequestModel r, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(r.Mobile))
                return new GetPublicPatientProfileResponseModel { Success = false, StatusCode = 401, Message = "Please log in again." };

            var problem = PatientProfileRules.Validate(r.FullName, r.Age, r.AgeUnit, r.Sex, r.Email, r.GuardianName, r.GuardianRelation);
            if (problem != null) return new GetPublicPatientProfileResponseModel { Success = false, StatusCode = 400, Message = problem };

            var age = r.Age.HasValue ? (short?)r.Age.Value : null;
            var unit = age.HasValue ? (PatientProfileRules.Clean(r.AgeUnit)?.ToUpperInvariant() ?? "Y") : null;

            var row = await _context.PatientAccountProfiles.FirstOrDefaultAsync(p => p.Mobile == r.Mobile, ct);
            if (row == null)
            {
                row = new PatientAccountProfile { Mobile = r.Mobile };
                _context.PatientAccountProfiles.Add(row);
            }
            row.FullName = PatientProfileRules.Clean(r.FullName)!;
            row.Age = age;
            row.AgeUnit = unit;
            row.Sex = PatientProfileRules.NormalizeSex(r.Sex);
            row.Email = PatientProfileRules.Clean(r.Email);
            row.GuardianName = PatientProfileRules.Clean(r.GuardianName);
            row.GuardianRelation = PatientProfileRules.Clean(r.GuardianRelation);
            row.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            var saved = GetPublicPatientProfileHandler.FromAccount(row);
            saved.Message = "Saved.";
            return saved;
        }
    }
}
