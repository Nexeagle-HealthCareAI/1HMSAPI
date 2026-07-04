using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UpsertDoctorFeeHandler : IRequestHandler<UpsertDoctorFeeRequestModel, UpsertDoctorFeeResponseModel>
    {
        private const string OpdConsult = "OPD_CONSULT";
        private const string IpdVisit = "IPD_VISIT";
        private const string Emergency = "EMERGENCY";

        private readonly AppDbContext _context;

        public UpsertDoctorFeeHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UpsertDoctorFeeResponseModel> Handle(UpsertDoctorFeeRequestModel request, CancellationToken cancellationToken)
        {
            if (request.DoctorId == Guid.Empty)
                return new UpsertDoctorFeeResponseModel { IsSuccess = false, Message = "DoctorId is required." };
            if (request.OpdConsultFee < 0 || request.IpdVisitFee < 0 || request.EmergencyFee < 0)
                return new UpsertDoctorFeeResponseModel { IsSuccess = false, Message = "Fees cannot be negative." };

            var existing = await _context.DoctorFees
                .Where(f => f.HospitalId == request.HospitalId && f.DoctorId == request.DoctorId)
                .ToListAsync(cancellationToken);

            var now = DateTime.UtcNow;

            void Apply(string feeType, decimal amount)
            {
                var row = existing.FirstOrDefault(f => f.FeeType == feeType);
                if (row == null)
                {
                    _context.DoctorFees.Add(new DoctorFee
                    {
                        DoctorFeeId = Guid.NewGuid(),
                        HospitalId = request.HospitalId,
                        DoctorId = request.DoctorId,
                        FeeType = feeType,
                        Amount = amount,
                        IsActive = true,
                        CreatedAt = now,
                        CreatedBy = request.LoggedInUserName,
                        UpdatedAt = now,
                        UpdatedBy = request.LoggedInUserName
                    });
                }
                else
                {
                    row.Amount = amount;
                    row.IsActive = true;
                    row.UpdatedAt = now;
                    row.UpdatedBy = request.LoggedInUserName;
                }
            }

            Apply(OpdConsult, request.OpdConsultFee);
            Apply(IpdVisit, request.IpdVisitFee);
            Apply(Emergency, request.EmergencyFee);

            await _context.SaveChangesAsync(cancellationToken);

            return new UpsertDoctorFeeResponseModel { IsSuccess = true, Message = "Doctor fees saved." };
        }
    }
}
