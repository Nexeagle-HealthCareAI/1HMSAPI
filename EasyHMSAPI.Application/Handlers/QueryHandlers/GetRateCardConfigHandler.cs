using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetRateCardConfigHandler : IRequestHandler<GetRateCardConfigRequestModel, GetRateCardConfigResponseModel>
    {
        private readonly AppDbContext _context;

        public GetRateCardConfigHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetRateCardConfigResponseModel> Handle(GetRateCardConfigRequestModel request, CancellationToken cancellationToken)
        {
            var payerRates = await (
                from r in _context.ChargeMasterPayerRate
                join c in _context.ChargeMaster on r.ChargeId equals c.ChargeId
                where r.HospitalId == request.HospitalId
                orderby c.DisplayName, r.PayerType
                select new ChargeMasterPayerRateDataModel
                {
                    ChargeMasterPayerRateId = r.ChargeMasterPayerRateId,
                    ChargeId = r.ChargeId,
                    ChargeDisplayName = c.DisplayName,
                    ChargeCode = c.ChargeCode,
                    PayerType = r.PayerType,
                    OverrideRate = r.OverrideRate,
                    IsActive = r.IsActive
                }).ToListAsync(cancellationToken);

            var roomMultipliers = await _context.RoomClassRateMultiplier
                .Where(r => r.HospitalId == request.HospitalId)
                .OrderBy(r => r.RoomType)
                .Select(r => new RoomClassRateMultiplierDataModel
                {
                    RoomClassRateMultiplierId = r.RoomClassRateMultiplierId,
                    RoomType = r.RoomType,
                    MultiplierPercent = r.MultiplierPercent
                }).ToListAsync(cancellationToken);

            return new GetRateCardConfigResponseModel
            {
                PayerRates = payerRates,
                RoomMultipliers = roomMultipliers
            };
        }
    }
}
