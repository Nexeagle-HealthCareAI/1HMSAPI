using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>The people on this account, oldest added first, each with how many records are kept for them.</summary>
    public class GetPatientFamilyMembersHandler : IRequestHandler<GetPatientFamilyMembersRequestModel, PatientFamilyMembersResponseModel>
    {
        private readonly AppDbContext _context;

        public GetPatientFamilyMembersHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PatientFamilyMembersResponseModel> Handle(GetPatientFamilyMembersRequestModel request, CancellationToken ct)
        {
            _context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
            var members = await _context.PatientFamilyMembers.Where(m => m.OwnerMobile == request.Mobile).OrderBy(m => m.CreatedAt).ToListAsync(ct);
            var counts = await _context.PatientHealthLockerDocuments
                .Where(d => d.Mobile == request.Mobile && d.MemberId != null)
                .GroupBy(d => d.MemberId!.Value)
                .Select(g => new { Id = g.Key, N = g.Count() })
                .ToDictionaryAsync(x => x.Id, x => x.N, ct);

            return new PatientFamilyMembersResponseModel
            {
                Success = true,
                Members = members.Select(m => ToItem(m, counts.GetValueOrDefault(m.MemberId))).ToList(),
            };
        }

        internal static PatientFamilyMemberItem ToItem(PatientFamilyMember m, int documentCount) => new()
        {
            MemberId = m.MemberId, FirstName = m.FirstName, LastName = m.LastName, FullName = PatientProfileRules.JoinName(m.FirstName, m.LastName),
            Relation = m.Relation, Age = m.Age, AgeUnit = m.AgeUnit, Sex = m.Sex, BloodGroup = m.BloodGroup, Allergies = m.Allergies, DocumentCount = documentCount,
        };
    }
}
