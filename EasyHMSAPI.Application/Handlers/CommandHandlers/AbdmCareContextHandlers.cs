using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class LinkAbdmCareContextHandler : IRequestHandler<LinkAbdmCareContextRequestModel, AbdmCareContextResponseModel>
    {
        private readonly IAbdmLinkingService _linking;

        public LinkAbdmCareContextHandler(IAbdmLinkingService linking)
        {
            _linking = linking;
        }

        public async Task<AbdmCareContextResponseModel> Handle(LinkAbdmCareContextRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty || request.SourceId == Guid.Empty)
                return new AbdmCareContextResponseModel { Success = false, Message = "Hospital and visit are required." };

            try
            {
                var careContext = await _linking.StartAsync(request.HospitalId, request.SourceType?.Trim().ToUpperInvariant() ?? string.Empty,
                    request.SourceId, request.LoggedInUserName, cancellationToken);
                // Success means the work started; LinkStatus says where it has got to.
                return new AbdmCareContextResponseModel { Success = true, CareContext = AbdmCareContextDto.From(careContext) };
            }
            catch (AbdmLinkingRefusedException ex)
            {
                return new AbdmCareContextResponseModel { Success = false, Message = ex.Message };
            }
        }
    }

    public class GetAbdmCareContextsHandler : IRequestHandler<GetAbdmCareContextsRequestModel, AbdmCareContextListResponseModel>
    {
        private readonly AppDbContext _db;

        public GetAbdmCareContextsHandler(AppDbContext db)
        {
            _db = db;
        }

        public async Task<AbdmCareContextListResponseModel> Handle(GetAbdmCareContextsRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HospitalId == Guid.Empty)
                return new AbdmCareContextListResponseModel { Success = false, Message = "Hospital is required." };

            var query = _db.AbdmCareContexts.AsNoTracking().Where(c => c.HospitalId == request.HospitalId);
            if (!string.IsNullOrWhiteSpace(request.PatientId))
                query = query.Where(c => c.PatientId == request.PatientId);

            var rows = await query.OrderByDescending(c => c.UpdatedAt).Take(200).ToListAsync(cancellationToken);
            return new AbdmCareContextListResponseModel { Success = true, CareContexts = rows.Select(AbdmCareContextDto.From).ToList() };
        }
    }
}
