using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetBloodBagPoolHandler : IRequestHandler<GetBloodBagPoolRequestModel, GetBloodBagPoolResponseModel>
    {
        private readonly AppDbContext _context;

        public GetBloodBagPoolHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetBloodBagPoolResponseModel> Handle(GetBloodBagPoolRequestModel request, CancellationToken cancellationToken)
        {
            var query = _context.BloodBag.Where(b => b.HospitalId == request.HospitalId && b.Status == IpdConstants.BloodBagStatus.Available);

            if (!string.IsNullOrWhiteSpace(request.Component))
                query = query.Where(b => b.Component == request.Component.Trim().ToUpperInvariant());

            if (!string.IsNullOrWhiteSpace(request.BloodGroup))
                query = query.Where(b => b.BloodGroup == request.BloodGroup.Trim().ToUpperInvariant());

            var bags = await query
                .OrderBy(b => b.ExpiresAt)
                .Select(b => new BloodBagDataModel
                {
                    BloodBagId = b.BloodBagId,
                    BagNumber = b.BagNumber,
                    Component = b.Component,
                    BloodGroup = b.BloodGroup,
                    VolumeMl = b.VolumeMl,
                    ExpiresAt = b.ExpiresAt,
                    StorageLocation = b.StorageLocation,
                    Status = b.Status,
                    ReservedForPatientId = b.ReservedForPatientId,
                    CrossmatchResult = b.CrossmatchResult,
                })
                .ToListAsync(cancellationToken);

            return new GetBloodBagPoolResponseModel { Bags = bags };
        }
    }
}
