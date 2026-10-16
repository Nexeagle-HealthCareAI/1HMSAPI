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
    /// <summary>The patient adds, or changes, someone they look after. Only ever within their own account.</summary>
    public class SavePatientFamilyMemberHandler : IRequestHandler<SavePatientFamilyMemberRequestModel, PatientFamilyMembersResponseModel>
    {
        private readonly AppDbContext _context;

        public SavePatientFamilyMemberHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PatientFamilyMembersResponseModel> Handle(SavePatientFamilyMemberRequestModel r, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(r.Mobile))
                return Fail(401, "Please log in again.");

            var problem = PatientProfileRules.ValidateMember(r.FirstName, r.LastName, r.Relation, r.Age, r.AgeUnit, r.Sex, r.BloodGroup, r.Allergies);
            if (problem != null) return Fail(400, problem);

            PatientFamilyMember? row;
            if (r.MemberId.HasValue)
            {
                row = await _context.PatientFamilyMembers.FirstOrDefaultAsync(m => m.MemberId == r.MemberId.Value && m.OwnerMobile == r.Mobile, ct);
                if (row == null) return Fail(404, "Family member not found.");
            }
            else
            {
                if (await _context.PatientFamilyMembers.CountAsync(m => m.OwnerMobile == r.Mobile, ct) >= PatientProfileRules.MaxFamilyMembers)
                    return Fail(400, $"You can add up to {PatientProfileRules.MaxFamilyMembers} family members.");
                row = new PatientFamilyMember { MemberId = Guid.NewGuid(), OwnerMobile = r.Mobile, CreatedAt = DateTime.UtcNow };
                _context.PatientFamilyMembers.Add(row);
            }

            var age = r.Age.HasValue ? (short?)r.Age.Value : null;
            row.FirstName = PatientProfileRules.Clean(r.FirstName)!;
            row.LastName = PatientProfileRules.Clean(r.LastName);
            row.Relation = PatientProfileRules.Clean(r.Relation)!;
            row.Age = age;
            row.AgeUnit = age.HasValue ? (PatientProfileRules.Clean(r.AgeUnit)?.ToUpperInvariant() ?? "Y") : null;
            row.Sex = PatientProfileRules.NormalizeSex(r.Sex);
            row.BloodGroup = PatientProfileRules.NormalizeBloodGroup(r.BloodGroup);
            row.Allergies = PatientProfileRules.Clean(r.Allergies);
            row.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);

            var count = await _context.PatientHealthLockerDocuments.CountAsync(d => d.Mobile == r.Mobile && d.MemberId == row.MemberId, ct);
            return new PatientFamilyMembersResponseModel
            {
                Success = true,
                Message = r.MemberId.HasValue ? "Saved." : "Family member added.",
                Member = GetPatientFamilyMembersHandler.ToItem(row, count),
            };
        }

        private static PatientFamilyMembersResponseModel Fail(int status, string message) => new() { Success = false, StatusCode = status, Message = message };
    }
}
