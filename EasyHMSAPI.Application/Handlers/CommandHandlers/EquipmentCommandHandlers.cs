using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class EquipmentCommandHandlers :
        IRequestHandler<UpsertEquipmentRequestModel, UpsertEquipmentResponseModel>,
        IRequestHandler<RecordMaintenanceLogRequestModel, RecordMaintenanceLogResponseModel>
    {
        private readonly AppDbContext _context;

        public EquipmentCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UpsertEquipmentResponseModel> Handle(UpsertEquipmentRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || string.IsNullOrWhiteSpace(request.AssetCode) || string.IsNullOrWhiteSpace(request.Name))
                    return new UpsertEquipmentResponseModel { Success = false, Message = "HospitalId, AssetCode, and Name are required." };

                var category = request.Category?.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(category) || !IpdConstants.EquipmentCategory.All.Contains(category))
                    return new UpsertEquipmentResponseModel { Success = false, Message = "Invalid category." };

                var status = string.IsNullOrWhiteSpace(request.Status) ? IpdConstants.EquipmentStatus.Active : request.Status.Trim().ToUpperInvariant();
                if (!IpdConstants.EquipmentStatus.All.Contains(status))
                    return new UpsertEquipmentResponseModel { Success = false, Message = "Invalid status." };

                if (request.PmIntervalDays.HasValue && request.PmIntervalDays <= 0)
                    return new UpsertEquipmentResponseModel { Success = false, Message = "PM interval must be greater than zero." };

                var now = DateTime.UtcNow;

                if (request.EquipmentId.HasValue && request.EquipmentId != Guid.Empty)
                {
                    var existing = await _context.Equipment
                        .FirstOrDefaultAsync(e => e.EquipmentId == request.EquipmentId && e.HospitalId == request.HospitalId, cancellationToken);
                    if (existing == null)
                        return new UpsertEquipmentResponseModel { Success = false, Message = "Equipment not found." };

                    var codeTaken = await _context.Equipment.AnyAsync(
                        e => e.HospitalId == request.HospitalId && e.AssetCode == request.AssetCode.Trim() && e.EquipmentId != existing.EquipmentId, cancellationToken);
                    if (codeTaken)
                        return new UpsertEquipmentResponseModel { Success = false, Message = "An asset with this code already exists." };

                    existing.AssetCode = request.AssetCode.Trim();
                    existing.Name = request.Name.Trim();
                    existing.Model = request.Model;
                    existing.SerialNumber = request.SerialNumber;
                    existing.Manufacturer = request.Manufacturer;
                    existing.Category = category;
                    existing.Location = request.Location;
                    existing.Department = request.Department;
                    existing.AmcVendor = request.AmcVendor;
                    existing.InstalledAt = request.InstalledAt;
                    existing.WarrantyEndAt = request.WarrantyEndAt;
                    existing.AmcEndAt = request.AmcEndAt;
                    existing.PmIntervalDays = request.PmIntervalDays;
                    existing.Status = status;
                    existing.Notes = request.Notes;
                    existing.UpdatedAt = now;
                    existing.UpdatedBy = request.LoggedInUserName;

                    await _context.SaveChangesAsync(cancellationToken);
                    return new UpsertEquipmentResponseModel { Success = true, Message = "Asset updated.", EquipmentId = existing.EquipmentId };
                }

                var exists = await _context.Equipment.AnyAsync(
                    e => e.HospitalId == request.HospitalId && e.AssetCode == request.AssetCode.Trim(), cancellationToken);
                if (exists)
                    return new UpsertEquipmentResponseModel { Success = false, Message = "An asset with this code already exists." };

                var equipment = new Equipment
                {
                    EquipmentId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    AssetCode = request.AssetCode.Trim(),
                    Name = request.Name.Trim(),
                    Model = request.Model,
                    SerialNumber = request.SerialNumber,
                    Manufacturer = request.Manufacturer,
                    Category = category,
                    Location = request.Location,
                    Department = request.Department,
                    AmcVendor = request.AmcVendor,
                    InstalledAt = request.InstalledAt,
                    WarrantyEndAt = request.WarrantyEndAt,
                    AmcEndAt = request.AmcEndAt,
                    PmIntervalDays = request.PmIntervalDays,
                    NextDueAt = request.PmIntervalDays.HasValue ? (request.InstalledAt ?? now).AddDays(request.PmIntervalDays.Value) : null,
                    Status = status,
                    Notes = request.Notes,
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName,
                };
                _context.Equipment.Add(equipment);
                await _context.SaveChangesAsync(cancellationToken);

                return new UpsertEquipmentResponseModel { Success = true, Message = "Asset created.", EquipmentId = equipment.EquipmentId };
            }
            catch (Exception)
            {
                return new UpsertEquipmentResponseModel { Success = false, Message = "Error saving asset." };
            }
        }

        public async Task<RecordMaintenanceLogResponseModel> Handle(RecordMaintenanceLogRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.EquipmentId == Guid.Empty)
                    return new RecordMaintenanceLogResponseModel { Success = false, Message = "HospitalId and EquipmentId are required." };

                var activityType = request.ActivityType?.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(activityType) || !IpdConstants.MaintenanceActivityType.All.Contains(activityType))
                    return new RecordMaintenanceLogResponseModel { Success = false, Message = "Invalid activity type." };

                var outcome = string.IsNullOrWhiteSpace(request.Outcome) ? null : request.Outcome.Trim().ToUpperInvariant();
                if (outcome != null && !IpdConstants.MaintenanceOutcome.All.Contains(outcome))
                    return new RecordMaintenanceLogResponseModel { Success = false, Message = "Invalid outcome." };

                var equipment = await _context.Equipment
                    .FirstOrDefaultAsync(e => e.EquipmentId == request.EquipmentId && e.HospitalId == request.HospitalId, cancellationToken);
                if (equipment == null)
                    return new RecordMaintenanceLogResponseModel { Success = false, Message = "Equipment not found." };

                var now = DateTime.UtcNow;
                var performedAt = request.PerformedAt ?? now;

                var log = new MaintenanceLog
                {
                    MaintenanceLogId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    EquipmentId = equipment.EquipmentId,
                    ActivityType = activityType,
                    PerformedAt = performedAt,
                    PerformedBy = request.LoggedInUserName ?? "Unknown",
                    PerformedByUserId = request.LoggedInUserId,
                    VendorName = request.VendorName,
                    Cost = request.Cost,
                    PartsReplaced = request.PartsReplaced,
                    Findings = request.Findings,
                    ActionTaken = request.ActionTaken,
                    Outcome = outcome,
                    NextDueAtOverride = request.NextDueAtOverride,
                    Notes = request.Notes,
                    Attachments = request.Attachments,
                    CreatedAt = now,
                };
                _context.MaintenanceLog.Add(log);

                // Recompute the denormalized scheduling/status fields — the same "single source of
                // truth via the audited action" discipline used for InventoryItem.CurrentStock.
                equipment.LastServiceAt = performedAt;
                equipment.NextDueAt = request.NextDueAtOverride
                    ?? (equipment.PmIntervalDays.HasValue ? performedAt.AddDays(equipment.PmIntervalDays.Value) : equipment.NextDueAt);
                equipment.Status = (activityType == IpdConstants.MaintenanceActivityType.Breakdown || outcome == IpdConstants.MaintenanceOutcome.Fail)
                    ? IpdConstants.EquipmentStatus.UnderMaintenance
                    : IpdConstants.EquipmentStatus.Active;
                equipment.UpdatedAt = now;
                equipment.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);

                return new RecordMaintenanceLogResponseModel
                {
                    Success = true,
                    Message = "Maintenance log recorded.",
                    MaintenanceLogId = log.MaintenanceLogId,
                    NewNextDueAt = equipment.NextDueAt,
                };
            }
            catch (Exception)
            {
                return new RecordMaintenanceLogResponseModel { Success = false, Message = "Error recording maintenance log." };
            }
        }
    }
}
