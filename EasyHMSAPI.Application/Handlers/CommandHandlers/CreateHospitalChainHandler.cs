using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// Creates a chain owned by the caller and links the caller's existing standalone hospitals
    /// (the ones they created, not already in a chain) as the first members.
    /// </summary>
    public class CreateHospitalChainHandler : IRequestHandler<CreateHospitalChainRequestModel, CreateHospitalChainResponseModel>
    {
        private readonly AppDbContext _context;

        public CreateHospitalChainHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<CreateHospitalChainResponseModel> Handle(CreateHospitalChainRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.OwnerUserId == Guid.Empty || string.IsNullOrWhiteSpace(request.Name))
                    return new CreateHospitalChainResponseModel { Success = false, Message = "Chain name and owner are required." };

                // One chain per owner — prevents creating a second, empty chain.
                var existingChain = await _context.HospitalChains
                    .FirstOrDefaultAsync(c => c.OwnerUserId == request.OwnerUserId, cancellationToken);
                if (existingChain != null)
                    return new CreateHospitalChainResponseModel
                    {
                        Success = false,
                        Message = $"You already own the chain \"{existingChain.Name}\". Add hospitals to it instead.",
                        ChainId = existingChain.ChainId,
                    };

                // The caller must own at least one hospital to start a chain.
                var ownedHospitals = await _context.Hospitals
                    .Where(h => h.CreatedByUserID == request.OwnerUserId)
                    .ToListAsync(cancellationToken);
                if (ownedHospitals.Count == 0)
                    return new CreateHospitalChainResponseModel { Success = false, Message = "You must own a hospital before creating a chain." };

                var chain = new HospitalChain
                {
                    ChainId = Guid.NewGuid(),
                    Name = request.Name.Trim(),
                    OwnerUserId = request.OwnerUserId,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = request.LoggedInUserName,
                };
                _context.HospitalChains.Add(chain);

                // Link the owner's not-yet-chained hospitals into the new chain.
                int linked = 0;
                foreach (var h in ownedHospitals.Where(h => h.ChainId == null))
                {
                    h.ChainId = chain.ChainId;
                    h.LastUpdatedAt = DateTime.UtcNow;
                    linked++;
                }

                await _context.SaveChangesAsync(cancellationToken);

                return new CreateHospitalChainResponseModel
                {
                    Success = true,
                    Message = $"Chain created. {linked} hospital(s) linked.",
                    ChainId = chain.ChainId,
                    HospitalsLinked = linked,
                };
            }
            catch (Exception)
            {
                return new CreateHospitalChainResponseModel { Success = false, Message = "Error creating chain." };
            }
        }
    }
}
