using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class NarcoticComplianceCommandHandlers :
        IRequestHandler<DispenseNarcoticRequestModel, DispenseNarcoticResponseModel>,
        IRequestHandler<RecordColdChainReadingRequestModel, RecordColdChainReadingResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IMediator _mediator;

        public NarcoticComplianceCommandHandlers(AppDbContext context, IMediator mediator)
        {
            _context = context;
            _mediator = mediator;
        }

        public async Task<DispenseNarcoticResponseModel> Handle(DispenseNarcoticRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.InventoryItemId == Guid.Empty || request.StoreId == Guid.Empty)
                    return new DispenseNarcoticResponseModel { Success = false, Message = "HospitalId, InventoryItemId, and StoreId are required." };
                if (request.Qty <= 0)
                    return new DispenseNarcoticResponseModel { Success = false, Message = "Qty must be greater than zero." };
                if (string.IsNullOrWhiteSpace(request.PrescriberRef))
                    return new DispenseNarcoticResponseModel { Success = false, Message = "A prescriber reference is required." };
                if (string.IsNullOrWhiteSpace(request.WitnessBy))
                    return new DispenseNarcoticResponseModel { Success = false, Message = "A witness is required to dispense a narcotic." };

                var item = await _context.InventoryItem.FirstOrDefaultAsync(
                    i => i.InventoryItemId == request.InventoryItemId && i.HospitalId == request.HospitalId, cancellationToken);
                if (item == null)
                    return new DispenseNarcoticResponseModel { Success = false, Message = "Item not found." };
                if (item.ScheduleClass != IpdConstants.DrugScheduleClass.Narcotic)
                    return new DispenseNarcoticResponseModel { Success = false, Message = "This item is not a narcotic-scheduled drug." };

                var strategy = _context.Database.CreateExecutionStrategy();
                return await strategy.ExecuteAsync(async () =>
                {
                    await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
                    try
                    {
                        var movementResponse = await _mediator.Send(new RecordInventoryMovementRequestModel
                        {
                            HospitalId = request.HospitalId,
                            InventoryItemId = request.InventoryItemId,
                            MovementType = IpdConstants.InventoryMovementType.Issue,
                            Qty = request.Qty,
                            BatchId = request.BatchId,
                            StoreId = request.StoreId,
                            PatientId = request.PatientId,
                            EncounterId = request.EncounterId,
                            PrescriberRef = request.PrescriberRef,
                            WitnessBy = request.WitnessBy,
                            WitnessByUserId = request.WitnessByUserId,
                            SourceModule = "NARCOTIC_DISPENSE",
                            LoggedInUserName = request.LoggedInUserName,
                            LoggedInUserId = request.LoggedInUserId,
                            IsNarcoticDispenseContext = true,
                        }, cancellationToken);

                        if (!movementResponse.Success)
                        {
                            await tx.RollbackAsync(cancellationToken);
                            return new DispenseNarcoticResponseModel { Success = false, Message = movementResponse.Message ?? "Could not dispense this narcotic." };
                        }

                        await tx.CommitAsync(cancellationToken);
                        return new DispenseNarcoticResponseModel { Success = true, Message = "Narcotic dispensed.", NewCurrentStock = movementResponse.NewCurrentStock };
                    }
                    catch (Exception)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return new DispenseNarcoticResponseModel { Success = false, Message = "Error dispensing narcotic." };
                    }
                });
            }
            catch (Exception)
            {
                return new DispenseNarcoticResponseModel { Success = false, Message = "Error dispensing narcotic." };
            }
        }

        public async Task<RecordColdChainReadingResponseModel> Handle(RecordColdChainReadingRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.StoreId == Guid.Empty)
                    return new RecordColdChainReadingResponseModel { Success = false, Message = "HospitalId and StoreId are required." };

                var store = await _context.Store.FirstOrDefaultAsync(
                    s => s.StoreId == request.StoreId && s.HospitalId == request.HospitalId, cancellationToken);
                if (store == null)
                    return new RecordColdChainReadingResponseModel { Success = false, Message = "Store not found." };

                var breach = (store.MinTempCelsius.HasValue && request.TempCelsius < store.MinTempCelsius.Value)
                    || (store.MaxTempCelsius.HasValue && request.TempCelsius > store.MaxTempCelsius.Value);

                var log = new ColdChainTempLog
                {
                    LogId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    StoreId = request.StoreId,
                    RecordedAt = request.RecordedAt ?? DateTime.UtcNow,
                    TempCelsius = request.TempCelsius,
                    RecordedBy = request.LoggedInUserName,
                    BreachFlag = breach,
                };
                _context.ColdChainTempLog.Add(log);
                await _context.SaveChangesAsync(cancellationToken);

                return new RecordColdChainReadingResponseModel
                {
                    Success = true,
                    Message = breach ? "Reading recorded — temperature breach flagged." : "Reading recorded.",
                    BreachFlag = breach,
                };
            }
            catch (Exception)
            {
                return new RecordColdChainReadingResponseModel { Success = false, Message = "Error recording reading." };
            }
        }
    }
}
