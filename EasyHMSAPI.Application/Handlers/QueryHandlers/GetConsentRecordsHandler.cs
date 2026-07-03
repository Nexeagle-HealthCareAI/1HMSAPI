using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetConsentRecordsHandler : IRequestHandler<GetConsentRecordsRequestModel, GetConsentRecordsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetConsentRecordsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetConsentRecordsResponseModel> Handle(GetConsentRecordsRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new GetConsentRecordsResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var records = await _context.ConsentRecord
                    .Where(c => c.HospitalId == request.HospitalId && c.AdmissionId == request.AdmissionId)
                    .OrderByDescending(c => c.SignedAt)
                    .Select(c => new ConsentRecordItem
                    {
                        ConsentRecordId = c.ConsentRecordId,
                        TemplateTypeCode = c.TemplateTypeCode,
                        TemplateTitle = c.TemplateTitle,
                        TemplateVersion = c.TemplateVersion,
                        ProcedureName = c.ProcedureName,
                        SignedByName = c.SignedByName,
                        SignerRelation = c.SignerRelation,
                        WitnessName = c.WitnessName,
                        WitnessRole = c.WitnessRole,
                        SignedAt = c.SignedAt,
                    })
                    .ToListAsync(cancellationToken);

                return new GetConsentRecordsResponseModel { Success = true, Records = records };
            }
            catch (Exception)
            {
                return new GetConsentRecordsResponseModel { Success = false, Message = "Error loading consent records." };
            }
        }
    }
}
