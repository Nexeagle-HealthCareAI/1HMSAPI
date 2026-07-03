using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class RateCardCommandHandlers :
        IRequestHandler<UpsertChargeMasterPayerRateRequestModel, UpsertChargeMasterPayerRateResponseModel>,
        IRequestHandler<UpsertRoomClassRateMultiplierRequestModel, UpsertRoomClassRateMultiplierResponseModel>
    {
        private readonly AppDbContext _context;

        public RateCardCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UpsertChargeMasterPayerRateResponseModel> Handle(UpsertChargeMasterPayerRateRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.ChargeId == Guid.Empty)
                    return new UpsertChargeMasterPayerRateResponseModel { Success = false, Message = "HospitalId and ChargeId are required." };

                var payerType = request.PayerType?.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(payerType) || !IpdConstants.PayerType.All.Contains(payerType))
                    return new UpsertChargeMasterPayerRateResponseModel { Success = false, Message = "Invalid payer type." };

                if (request.OverrideRate < 0)
                    return new UpsertChargeMasterPayerRateResponseModel { Success = false, Message = "OverrideRate cannot be negative." };

                var chargeExists = await _context.ChargeMaster.AnyAsync(c => c.ChargeId == request.ChargeId && c.HospitalId == request.HospitalId, cancellationToken);
                if (!chargeExists)
                    return new UpsertChargeMasterPayerRateResponseModel { Success = false, Message = "Charge item not found." };

                var now = DateTime.UtcNow;
                var existing = await _context.ChargeMasterPayerRate.FirstOrDefaultAsync(
                    r => r.HospitalId == request.HospitalId && r.ChargeId == request.ChargeId && r.PayerType == payerType, cancellationToken);

                if (existing != null)
                {
                    existing.OverrideRate = request.OverrideRate;
                    existing.IsActive = request.IsActive;
                    existing.UpdatedAt = now;
                    existing.UpdatedBy = request.LoggedInUserName;
                }
                else
                {
                    existing = new ChargeMasterPayerRate
                    {
                        ChargeMasterPayerRateId = Guid.NewGuid(),
                        HospitalId = request.HospitalId,
                        ChargeId = request.ChargeId,
                        PayerType = payerType,
                        OverrideRate = request.OverrideRate,
                        IsActive = request.IsActive,
                        CreatedAt = now,
                        CreatedBy = request.LoggedInUserName,
                        UpdatedAt = now,
                        UpdatedBy = request.LoggedInUserName,
                    };
                    _context.ChargeMasterPayerRate.Add(existing);
                }

                await _context.SaveChangesAsync(cancellationToken);
                return new UpsertChargeMasterPayerRateResponseModel { Success = true, Message = "Payer rate saved.", ChargeMasterPayerRateId = existing.ChargeMasterPayerRateId };
            }
            catch (Exception)
            {
                return new UpsertChargeMasterPayerRateResponseModel { Success = false, Message = "Error saving payer rate." };
            }
        }

        public async Task<UpsertRoomClassRateMultiplierResponseModel> Handle(UpsertRoomClassRateMultiplierRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || string.IsNullOrWhiteSpace(request.RoomType))
                    return new UpsertRoomClassRateMultiplierResponseModel { Success = false, Message = "HospitalId and RoomType are required." };

                if (request.MultiplierPercent <= 0)
                    return new UpsertRoomClassRateMultiplierResponseModel { Success = false, Message = "MultiplierPercent must be greater than zero." };

                var roomType = request.RoomType.Trim().ToUpperInvariant();
                var now = DateTime.UtcNow;
                var existing = await _context.RoomClassRateMultiplier.FirstOrDefaultAsync(
                    r => r.HospitalId == request.HospitalId && r.RoomType == roomType, cancellationToken);

                if (existing != null)
                {
                    existing.MultiplierPercent = request.MultiplierPercent;
                    existing.UpdatedAt = now;
                    existing.UpdatedBy = request.LoggedInUserName;
                }
                else
                {
                    existing = new RoomClassRateMultiplier
                    {
                        RoomClassRateMultiplierId = Guid.NewGuid(),
                        HospitalId = request.HospitalId,
                        RoomType = roomType,
                        MultiplierPercent = request.MultiplierPercent,
                        CreatedAt = now,
                        CreatedBy = request.LoggedInUserName,
                        UpdatedAt = now,
                        UpdatedBy = request.LoggedInUserName,
                    };
                    _context.RoomClassRateMultiplier.Add(existing);
                }

                await _context.SaveChangesAsync(cancellationToken);
                return new UpsertRoomClassRateMultiplierResponseModel { Success = true, Message = "Room-class multiplier saved.", RoomClassRateMultiplierId = existing.RoomClassRateMultiplierId };
            }
            catch (Exception)
            {
                return new UpsertRoomClassRateMultiplierResponseModel { Success = false, Message = "Error saving room-class multiplier." };
            }
        }
    }
}
