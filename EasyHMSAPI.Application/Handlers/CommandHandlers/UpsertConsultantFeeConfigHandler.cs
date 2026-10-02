using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UpsertConsultantFeeConfigHandler : IRequestHandler<UpsertConsultantFeeConfigRequestModel, UpsertConsultantFeeConfigResponseModel>
    {
        private readonly AppDbContext _context;

        public UpsertConsultantFeeConfigHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UpsertConsultantFeeConfigResponseModel> Handle(UpsertConsultantFeeConfigRequestModel request, CancellationToken cancellationToken)
        {
            if (request.HrEmployeeId == Guid.Empty)
                return new UpsertConsultantFeeConfigResponseModel { IsSuccess = false, Message = "HrEmployeeId is required." };
            if (request.MonthlyRetainer < 0 || request.IpdVisitFee < 0 || request.AdminSurcharge < 0)
                return new UpsertConsultantFeeConfigResponseModel { IsSuccess = false, Message = "Fee amounts cannot be negative." };
            if (request.OpdSharePercent < 0 || request.OpdSharePercent > 100)
                return new UpsertConsultantFeeConfigResponseModel { IsSuccess = false, Message = "OPD share percent must be between 0 and 100." };

            var employeeExists = await _context.HrEmployee
                .AnyAsync(e => e.HrEmployeeId == request.HrEmployeeId, cancellationToken);
            if (!employeeExists)
                return new UpsertConsultantFeeConfigResponseModel { IsSuccess = false, Message = "Employee not found." };

            var existing = await _context.Set<HrConsultantFeeConfig>()
                .FirstOrDefaultAsync(c => c.HrEmployeeId == request.HrEmployeeId && c.EffectiveFrom == request.EffectiveFrom, cancellationToken);

            var now = DateTime.UtcNow;

            if (existing == null)
            {
                _context.Add(new HrConsultantFeeConfig
                {
                    HrConsultantFeeConfigId = Guid.NewGuid(),
                    HrEmployeeId = request.HrEmployeeId,
                    EffectiveFrom = request.EffectiveFrom,
                    MonthlyRetainer = request.MonthlyRetainer,
                    OpdSharePercent = request.OpdSharePercent,
                    IpdVisitFee = request.IpdVisitFee,
                    AdminSurcharge = request.AdminSurcharge,
                    SurgeryShareConfigJson = request.SurgeryShareConfigJson,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now
                });
            }
            else
            {
                existing.MonthlyRetainer = request.MonthlyRetainer;
                existing.OpdSharePercent = request.OpdSharePercent;
                existing.IpdVisitFee = request.IpdVisitFee;
                existing.AdminSurcharge = request.AdminSurcharge;
                existing.SurgeryShareConfigJson = request.SurgeryShareConfigJson;
                existing.IsActive = true;
                existing.UpdatedAt = now;
            }

            await _context.SaveChangesAsync(cancellationToken);

            return new UpsertConsultantFeeConfigResponseModel { IsSuccess = true, Message = "Consultant fee config saved." };
        }
    }
}
