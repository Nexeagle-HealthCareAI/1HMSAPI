using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetConsultantFeeConfigHandler : IRequestHandler<GetConsultantFeeConfigRequestModel, GetConsultantFeeConfigResponseModel>
    {
        private readonly AppDbContext _context;

        public GetConsultantFeeConfigHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetConsultantFeeConfigResponseModel> Handle(GetConsultantFeeConfigRequestModel request, CancellationToken cancellationToken)
        {
            var config = await _context.Set<HrConsultantFeeConfig>()
                .AsNoTracking()
                .Where(c => c.HrEmployeeId == request.HrEmployeeId && c.IsActive)
                .OrderByDescending(c => c.EffectiveFrom)
                .Select(c => new ConsultantFeeConfigDto
                {
                    HrConsultantFeeConfigId = c.HrConsultantFeeConfigId,
                    HrEmployeeId = c.HrEmployeeId,
                    EffectiveFrom = c.EffectiveFrom,
                    MonthlyRetainer = c.MonthlyRetainer,
                    OpdSharePercent = c.OpdSharePercent,
                    IpdVisitFee = c.IpdVisitFee,
                    AdminSurcharge = c.AdminSurcharge,
                    SurgeryShareConfigJson = c.SurgeryShareConfigJson,
                    IsActive = c.IsActive
                })
                .FirstOrDefaultAsync(cancellationToken);

            return new GetConsultantFeeConfigResponseModel
            {
                Success = true,
                FeeConfig = config
            };
        }
    }
}
