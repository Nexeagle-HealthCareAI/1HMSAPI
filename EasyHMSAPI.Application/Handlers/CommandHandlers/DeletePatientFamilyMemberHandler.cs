using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>Removes a family member and the records kept for them (files first, so nothing is left orphaned in storage).</summary>
    public class DeletePatientFamilyMemberHandler : IRequestHandler<DeletePatientFamilyMemberRequestModel, PatientFamilyMembersResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IBlobStorageService _blobStorageService;
        private readonly string _containerName;

        public DeletePatientFamilyMemberHandler(AppDbContext context, IBlobStorageService blobStorageService, IConfiguration configuration)
        {
            _context = context;
            _blobStorageService = blobStorageService;
            _containerName = configuration["BlobStorage:HealthLockerContainer"] ?? "healthlocker";
        }

        public async Task<PatientFamilyMembersResponseModel> Handle(DeletePatientFamilyMemberRequestModel r, CancellationToken ct)
        {
            var member = await _context.PatientFamilyMembers.FirstOrDefaultAsync(m => m.MemberId == r.MemberId && m.OwnerMobile == r.Mobile, ct);
            if (member == null)
                return new PatientFamilyMembersResponseModel { Success = false, StatusCode = 404, Message = "Family member not found." };

            var documents = await _context.PatientHealthLockerDocuments.Where(d => d.Mobile == r.Mobile && d.MemberId == r.MemberId).ToListAsync(ct);
            foreach (var doc in documents)
            {
                if (!await _blobStorageService.DeleteAsync(doc.DocumentId.ToString(), _containerName, ct))
                    return new PatientFamilyMembersResponseModel { Success = false, StatusCode = 500, Message = "Could not remove their records from storage. Nothing was removed; please try again." };
                _context.PatientHealthLockerDocuments.Remove(doc);
            }
            _context.PatientFamilyMembers.Remove(member);
            await _context.SaveChangesAsync(ct);
            return new PatientFamilyMembersResponseModel { Success = true, Message = "Family member removed." };
        }
    }
}
