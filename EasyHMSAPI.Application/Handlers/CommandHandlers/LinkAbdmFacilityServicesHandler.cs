using EasyHMSAPI.Application.Common;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>Links the hospital's saved facility ID to our bridge as a HIP and an HIU, so ABDM
    /// accepts it in X-HIP-ID (M2) and X-HIU-ID (M3). Without the link every M2 call is refused with
    /// ABDM-1035 "Invalid HIP ID".</summary>
    public class LinkAbdmFacilityServicesHandler : IRequestHandler<LinkAbdmFacilityServicesRequestModel, LinkAbdmFacilityServicesResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IAbdmProfileShareService _abdm;

        public LinkAbdmFacilityServicesHandler(AppDbContext context, IAbdmProfileShareService abdm)
        {
            _context = context;
            _abdm = abdm;
        }

        public async Task<LinkAbdmFacilityServicesResponseModel> Handle(LinkAbdmFacilityServicesRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty || request.CallerUserId == Guid.Empty)
                return Fail("Hospital and signed-in user are required.");
            if (!await CallerGuards.IsHospitalMemberAsync(_context, request.CallerUserId, request.HospitalId, cancellationToken))
                return Fail("You don't have access to this hospital.");

            var facilityId = await _context.AbdmFacilities.AsNoTracking()
                .Where(f => f.HospitalId == request.HospitalId).Select(f => f.HipId).FirstOrDefaultAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(facilityId))
                return Fail("Save the facility (HIP / HFR) ID first.");

            var hospitalName = await _context.Hospitals.AsNoTracking()
                .Where(h => h.HospitalID == request.HospitalId).Select(h => h.Name).FirstOrDefaultAsync(cancellationToken);
            var hipName = string.IsNullOrWhiteSpace(request.HipName) ? AbdmFacilityRules.HipName(hospitalName) : request.HipName.Trim();
            if (!AbdmFacilityRules.IsValidHipName(hipName))
                return Fail("The name patients see may have letters, digits and spaces only, at most 15 characters.");

            try
            {
                var answer = await _abdm.LinkFacilityServicesAsync(facilityId, AbdmFacilityRules.FacilityName(hospitalName), hipName, cancellationToken);
                // ABDM says HTTP 200 even when it refuses; the refusal is inside the answer.
                var refusal = AbdmFacilityRules.LinkError(answer);
                if (refusal != null)
                    return new LinkAbdmFacilityServicesResponseModel
                    {
                        Success = false,
                        Message = $"ABDM refused to link facility {facilityId}: {refusal.Value.Message}" +
                            (refusal.Value.Code == null ? "." : $" (code {refusal.Value.Code}).") +
                            " Check that this is the facility ID registered in HFR for your ABDM client.",
                        FacilityId = facilityId,
                        HipName = hipName,
                        AbdmResponse = answer
                    };
                return new LinkAbdmFacilityServicesResponseModel
                {
                    Success = true,
                    Message = $"Facility {facilityId} linked to this software as HIP and HIU, shown to patients as \"{hipName}\".",
                    FacilityId = facilityId,
                    HipName = hipName,
                    AbdmResponse = answer
                };
            }
            catch (InvalidOperationException ex)
            {
                return Fail(ex.Message);
            }
        }

        private static LinkAbdmFacilityServicesResponseModel Fail(string message) => new() { Success = false, Message = message };
    }
}
