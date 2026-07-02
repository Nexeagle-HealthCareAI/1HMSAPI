using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// CSSD — set/tray master + the issue-to-OT -> return -> wash -> pack -> sterilize -> store
    /// loop. InstrumentSet.CurrentStatus/CurrentLocation are denormalized from the movement log
    /// (mirrors InventoryItem.CurrentStock relative to InventoryMovement).
    /// </summary>
    public class CssdCommandHandlers :
        IRequestHandler<CreateInstrumentSetRequestModel, CreateInstrumentSetResponseModel>,
        IRequestHandler<RecordInstrumentSetMovementRequestModel, RecordInstrumentSetMovementResponseModel>,
        IRequestHandler<RecordSterilizationCycleRequestModel, RecordSterilizationCycleResponseModel>
    {
        // Fixed MovementType -> resulting CurrentStatus mapping. RECEIVE_STERILE closes the loop
        // back to AVAILABLE (set is back on the shelf, ready to reissue) rather than staying
        // STERILE indefinitely.
        private static readonly Dictionary<string, string> StatusForMovement = new()
        {
            [IpdConstants.InstrumentSetMovementType.IssueToOt] = IpdConstants.InstrumentSetStatus.InUse,
            [IpdConstants.InstrumentSetMovementType.Return] = IpdConstants.InstrumentSetStatus.ReturnedSoiled,
            [IpdConstants.InstrumentSetMovementType.SendToWash] = IpdConstants.InstrumentSetStatus.Washing,
            [IpdConstants.InstrumentSetMovementType.Pack] = IpdConstants.InstrumentSetStatus.Packed,
            [IpdConstants.InstrumentSetMovementType.Quarantine] = IpdConstants.InstrumentSetStatus.Quarantined,
            [IpdConstants.InstrumentSetMovementType.Discard] = IpdConstants.InstrumentSetStatus.Retired,
            [IpdConstants.InstrumentSetMovementType.ReceiveSterile] = IpdConstants.InstrumentSetStatus.Available,
        };

        private readonly AppDbContext _context;

        public CssdCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<CreateInstrumentSetResponseModel> Handle(CreateInstrumentSetRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || string.IsNullOrWhiteSpace(request.SetCode) || string.IsNullOrWhiteSpace(request.SetName))
                    return new CreateInstrumentSetResponseModel { Success = false, Message = "HospitalId, SetCode, and SetName are required." };

                var exists = await _context.InstrumentSet.AnyAsync(
                    s => s.HospitalId == request.HospitalId && s.SetCode == request.SetCode.Trim(), cancellationToken);
                if (exists)
                    return new CreateInstrumentSetResponseModel { Success = false, Message = "A set with this code already exists." };

                var now = DateTime.UtcNow;
                var set = new InstrumentSet
                {
                    InstrumentSetId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    SetCode = request.SetCode.Trim(),
                    SetName = request.SetName.Trim(),
                    Category = string.IsNullOrWhiteSpace(request.Category) ? null : request.Category.Trim(),
                    ItemComposition = string.IsNullOrWhiteSpace(request.ItemComposition) ? null : request.ItemComposition.Trim(),
                    CurrentStatus = IpdConstants.InstrumentSetStatus.Available,
                    CurrentLocation = string.IsNullOrWhiteSpace(request.CurrentLocation) ? null : request.CurrentLocation.Trim(),
                    IsActive = true,
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName,
                };
                _context.InstrumentSet.Add(set);
                await _context.SaveChangesAsync(cancellationToken);

                return new CreateInstrumentSetResponseModel { Success = true, Message = "Set created.", InstrumentSetId = set.InstrumentSetId };
            }
            catch (Exception)
            {
                return new CreateInstrumentSetResponseModel { Success = false, Message = "Error creating instrument set." };
            }
        }

        public async Task<RecordInstrumentSetMovementResponseModel> Handle(RecordInstrumentSetMovementRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.InstrumentSetId == Guid.Empty)
                    return new RecordInstrumentSetMovementResponseModel { Success = false, Message = "HospitalId and InstrumentSetId are required." };

                var movementType = request.MovementType?.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(movementType) || !StatusForMovement.TryGetValue(movementType, out var newStatus))
                    return new RecordInstrumentSetMovementResponseModel { Success = false, Message = "Invalid movement type." };

                var set = await _context.InstrumentSet
                    .FirstOrDefaultAsync(s => s.InstrumentSetId == request.InstrumentSetId && s.HospitalId == request.HospitalId, cancellationToken);
                if (set == null)
                    return new RecordInstrumentSetMovementResponseModel { Success = false, Message = "Instrument set not found." };
                if (set.CurrentStatus == IpdConstants.InstrumentSetStatus.Retired)
                    return new RecordInstrumentSetMovementResponseModel { Success = false, Message = "This set has been retired." };

                var now = DateTime.UtcNow;
                var movement = new InstrumentSetMovement
                {
                    InstrumentSetMovementId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    InstrumentSetId = set.InstrumentSetId,
                    MovementType = movementType,
                    SurgeryCaseId = request.SurgeryCaseId,
                    MovedAt = now,
                    MovedBy = request.LoggedInUserName,
                    MovedByUserId = request.LoggedInUserId,
                    Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                    CreatedAt = now,
                };
                _context.InstrumentSetMovement.Add(movement);

                set.CurrentStatus = newStatus;
                if (!string.IsNullOrWhiteSpace(request.Location))
                    set.CurrentLocation = request.Location.Trim();
                set.UpdatedAt = now;
                set.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);

                return new RecordInstrumentSetMovementResponseModel { Success = true, Message = "Movement recorded.", NewStatus = newStatus };
            }
            catch (Exception)
            {
                return new RecordInstrumentSetMovementResponseModel { Success = false, Message = "Error recording movement." };
            }
        }

        public async Task<RecordSterilizationCycleResponseModel> Handle(RecordSterilizationCycleRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || string.IsNullOrWhiteSpace(request.CycleNumber))
                    return new RecordSterilizationCycleResponseModel { Success = false, Message = "HospitalId and CycleNumber are required." };

                var cycleType = request.CycleType?.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(cycleType) || !IpdConstants.SterilizationCycleType.All.Contains(cycleType))
                    return new RecordSterilizationCycleResponseModel { Success = false, Message = "Invalid cycle type." };

                var bioResult = request.BiologicalIndicatorResult?.Trim().ToUpperInvariant() ?? IpdConstants.IndicatorResult.Pending;
                if (bioResult != IpdConstants.IndicatorResult.Pass && bioResult != IpdConstants.IndicatorResult.Fail && bioResult != IpdConstants.IndicatorResult.Pending)
                    return new RecordSterilizationCycleResponseModel { Success = false, Message = "Invalid biological indicator result." };

                if (request.InstrumentSetIds == null || request.InstrumentSetIds.Count == 0)
                    return new RecordSterilizationCycleResponseModel { Success = false, Message = "At least one instrument set is required." };

                var exists = await _context.SterilizationCycle.AnyAsync(
                    c => c.HospitalId == request.HospitalId && c.CycleNumber == request.CycleNumber.Trim(), cancellationToken);
                if (exists)
                    return new RecordSterilizationCycleResponseModel { Success = false, Message = "A cycle with this number already exists." };

                var sets = await _context.InstrumentSet
                    .Where(s => request.InstrumentSetIds.Contains(s.InstrumentSetId) && s.HospitalId == request.HospitalId)
                    .ToListAsync(cancellationToken);
                if (sets.Count != request.InstrumentSetIds.Distinct().Count())
                    return new RecordSterilizationCycleResponseModel { Success = false, Message = "One or more instrument sets were not found." };

                var now = DateTime.UtcNow;
                var cycle = new SterilizationCycle
                {
                    SterilizationCycleId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    CycleNumber = request.CycleNumber.Trim(),
                    AutoclaveLabel = string.IsNullOrWhiteSpace(request.AutoclaveLabel) ? null : request.AutoclaveLabel.Trim(),
                    CycleType = cycleType,
                    StartedAt = request.StartedAt,
                    EndedAt = request.EndedAt,
                    BiologicalIndicatorResult = bioResult,
                    ChemicalIndicatorResult = string.IsNullOrWhiteSpace(request.ChemicalIndicatorResult) ? null : request.ChemicalIndicatorResult.Trim().ToUpperInvariant(),
                    OperatorName = request.LoggedInUserName ?? "Unknown",
                    OperatorByUserId = request.LoggedInUserId,
                    Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                };
                _context.SterilizationCycle.Add(cycle);

                foreach (var set in sets)
                {
                    _context.SterilizationCycleItem.Add(new SterilizationCycleItem
                    {
                        SterilizationCycleItemId = Guid.NewGuid(),
                        HospitalId = request.HospitalId,
                        SterilizationCycleId = cycle.SterilizationCycleId,
                        InstrumentSetId = set.InstrumentSetId,
                    });

                    // Every set loaded into a cycle moves to STERILIZING; a PASS/FAIL result then
                    // immediately resolves it to STERILE/QUARANTINED. PENDING leaves it at
                    // STERILIZING until a follow-up cycle record (same CycleNumber pattern, new
                    // row) supplies the result — this phase re-records rather than updates in place.
                    set.CurrentStatus = bioResult switch
                    {
                        IpdConstants.IndicatorResult.Pass => IpdConstants.InstrumentSetStatus.Sterile,
                        IpdConstants.IndicatorResult.Fail => IpdConstants.InstrumentSetStatus.Quarantined,
                        _ => IpdConstants.InstrumentSetStatus.Sterilizing,
                    };
                    set.UpdatedAt = now;
                    set.UpdatedBy = request.LoggedInUserName;
                }

                await _context.SaveChangesAsync(cancellationToken);

                return new RecordSterilizationCycleResponseModel { Success = true, Message = "Cycle recorded.", SterilizationCycleId = cycle.SterilizationCycleId };
            }
            catch (Exception)
            {
                return new RecordSterilizationCycleResponseModel { Success = false, Message = "Error recording sterilization cycle." };
            }
        }
    }
}
