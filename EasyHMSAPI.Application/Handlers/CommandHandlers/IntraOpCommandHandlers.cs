using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class IntraOpCommandHandlers :
        IRequestHandler<SaveIntraOpRecordRequestModel, SaveIntraOpRecordResponseModel>,
        IRequestHandler<RecordIntraOpItemUsageRequestModel, RecordIntraOpItemUsageResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IMediator _mediator;

        public IntraOpCommandHandlers(AppDbContext context, IMediator mediator)
        {
            _context = context;
            _mediator = mediator;
        }

        public async Task<SaveIntraOpRecordResponseModel> Handle(SaveIntraOpRecordRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.SurgeryCaseId == Guid.Empty)
                    return new SaveIntraOpRecordResponseModel { Success = false, Message = "HospitalId and SurgeryCaseId are required." };

                var anaesthesiaType = request.AnaesthesiaType?.Trim().ToUpperInvariant();
                if (!string.IsNullOrWhiteSpace(anaesthesiaType) && !IpdConstants.AnaesthesiaType.All.Contains(anaesthesiaType))
                    return new SaveIntraOpRecordResponseModel { Success = false, Message = "Invalid anaesthesia type." };

                var surgeryCaseExists = await _context.SurgeryCase
                    .AnyAsync(s => s.SurgeryCaseId == request.SurgeryCaseId && s.HospitalId == request.HospitalId, cancellationToken);
                if (!surgeryCaseExists)
                    return new SaveIntraOpRecordResponseModel { Success = false, Message = "Surgery case not found." };

                var now = DateTime.UtcNow;
                var record = await _context.IntraOpRecord
                    .FirstOrDefaultAsync(r => r.SurgeryCaseId == request.SurgeryCaseId && r.HospitalId == request.HospitalId, cancellationToken);
                if (record == null)
                {
                    record = new IntraOpRecord
                    {
                        IntraOpRecordId = Guid.NewGuid(),
                        HospitalId = request.HospitalId,
                        SurgeryCaseId = request.SurgeryCaseId,
                        CreatedAt = now,
                        CreatedBy = request.LoggedInUserName,
                    };
                    _context.IntraOpRecord.Add(record);
                }

                record.AnaesthesiaType = string.IsNullOrWhiteSpace(anaesthesiaType) ? null : anaesthesiaType;
                record.AnaesthesiaStartAt = request.AnaesthesiaStartAt;
                record.AnaesthesiaEndAt = request.AnaesthesiaEndAt;
                record.SurgeryStartAt = request.SurgeryStartAt;
                record.SurgeryEndAt = request.SurgeryEndAt;
                record.EstimatedBloodLossMl = request.EstimatedBloodLossMl;
                record.Findings = string.IsNullOrWhiteSpace(request.Findings) ? null : request.Findings.Trim();
                record.ProcedurePerformed = string.IsNullOrWhiteSpace(request.ProcedurePerformed) ? null : request.ProcedurePerformed.Trim();
                record.SurgicalTeam = string.IsNullOrWhiteSpace(request.SurgicalTeam) ? null : request.SurgicalTeam.Trim();
                record.ComplicationsNotes = string.IsNullOrWhiteSpace(request.ComplicationsNotes) ? null : request.ComplicationsNotes.Trim();
                record.RecordedBy = request.LoggedInUserName ?? "Unknown";
                record.RecordedAt = now;
                record.UpdatedAt = now;
                record.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);

                return new SaveIntraOpRecordResponseModel { Success = true, Message = "Intra-op record saved.", IntraOpRecordId = record.IntraOpRecordId };
            }
            catch (Exception)
            {
                return new SaveIntraOpRecordResponseModel { Success = false, Message = "Error saving intra-op record." };
            }
        }

        public async Task<RecordIntraOpItemUsageResponseModel> Handle(RecordIntraOpItemUsageRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.SurgeryCaseId == Guid.Empty || string.IsNullOrWhiteSpace(request.ItemName))
                    return new RecordIntraOpItemUsageResponseModel { Success = false, Message = "HospitalId, SurgeryCaseId, and ItemName are required." };

                var category = request.Category?.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(category) || !IpdConstants.IntraOpItemCategory.All.Contains(category))
                    return new RecordIntraOpItemUsageResponseModel { Success = false, Message = "Invalid category." };

                if (request.Qty <= 0)
                    return new RecordIntraOpItemUsageResponseModel { Success = false, Message = "Qty must be greater than zero." };

                var surgeryCase = await _context.SurgeryCase
                    .FirstOrDefaultAsync(s => s.SurgeryCaseId == request.SurgeryCaseId && s.HospitalId == request.HospitalId, cancellationToken);
                if (surgeryCase == null)
                    return new RecordIntraOpItemUsageResponseModel { Success = false, Message = "Surgery case not found." };

                var strategy = _context.Database.CreateExecutionStrategy();
                return await strategy.ExecuteAsync(async () =>
                {
                    await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
                    try
                    {
                        var now = DateTime.UtcNow;
                        var usage = new IntraOpItemUsage
                        {
                            IntraOpItemUsageId = Guid.NewGuid(),
                            HospitalId = request.HospitalId,
                            SurgeryCaseId = surgeryCase.SurgeryCaseId,
                            InventoryItemId = request.InventoryItemId,
                            ItemName = request.ItemName.Trim(),
                            Category = category,
                            Qty = request.Qty,
                            LotNumber = string.IsNullOrWhiteSpace(request.LotNumber) ? null : request.LotNumber.Trim(),
                            SerialNumber = string.IsNullOrWhiteSpace(request.SerialNumber) ? null : request.SerialNumber.Trim(),
                            ChargeId = request.ChargeId,
                            UnitRate = request.UnitRate,
                            RecordedBy = request.LoggedInUserName ?? "Unknown",
                            RecordedAt = now,
                        };
                        _context.IntraOpItemUsage.Add(usage);

                        // ── Pharmacy/implant auto-deduct ────────────────────────────────────────
                        if (request.InventoryItemId.HasValue)
                        {
                            var movementResponse = await _mediator.Send(new RecordInventoryMovementRequestModel
                            {
                                HospitalId = request.HospitalId,
                                InventoryItemId = request.InventoryItemId.Value,
                                MovementType = IpdConstants.InventoryMovementType.Issue,
                                Qty = request.Qty,
                                EncounterId = surgeryCase.EncounterId,
                                PatientId = surgeryCase.PatientId,
                                SourceModule = "OT",
                                SourceRefId = surgeryCase.SurgeryCaseId.ToString(),
                                LoggedInUserName = request.LoggedInUserName,
                                LoggedInUserId = request.LoggedInUserId,
                            }, cancellationToken);

                            if (!movementResponse.Success)
                            {
                                await tx.RollbackAsync(cancellationToken);
                                return new RecordIntraOpItemUsageResponseModel { Success = false, Message = movementResponse.Message ?? "Could not deduct stock for this item." };
                            }

                            usage.InventoryMovementId = movementResponse.InventoryMovementId;
                        }

                        // ── Optional billing charge-event, same guard CPOE uses ─────────────────
                        if (request.ChargeId.HasValue && surgeryCase.EncounterId.HasValue)
                        {
                            var chargeResponse = await _mediator.Send(new AddChargeEventRequestModel
                            {
                                HospitalId = request.HospitalId,
                                PatientId = surgeryCase.PatientId,
                                EncounterId = surgeryCase.EncounterId.Value,
                                Charges = new List<ChargeDetail>
                                {
                                    new ChargeDetail
                                    {
                                        ChargeId = request.ChargeId,
                                        DisplayName = usage.ItemName,
                                        Qty = usage.Qty,
                                        Rate = request.UnitRate ?? 0,
                                        DiscountPercent = 0,
                                        CategoryCode = category == IpdConstants.IntraOpItemCategory.Implant ? "IMPLANT" : "SURGICAL",
                                    },
                                },
                                LoggedInUserName = request.LoggedInUserName,
                                LoggedInUserId = request.LoggedInUserId,
                            }, cancellationToken);

                            if (chargeResponse.Success != true || chargeResponse.Data?.ChargeEvents == null || chargeResponse.Data.ChargeEvents.Count == 0)
                            {
                                await tx.RollbackAsync(cancellationToken);
                                return new RecordIntraOpItemUsageResponseModel { Success = false, Message = chargeResponse.Message ?? "Could not post the charge for this item." };
                            }

                            usage.ChargeEventId = chargeResponse.Data.ChargeEvents[0].ChargeEventId;
                        }

                        await _context.SaveChangesAsync(cancellationToken);
                        await tx.CommitAsync(cancellationToken);

                        return new RecordIntraOpItemUsageResponseModel
                        {
                            Success = true,
                            Message = "Item usage recorded.",
                            IntraOpItemUsageId = usage.IntraOpItemUsageId,
                            ChargeEventId = usage.ChargeEventId,
                            InventoryMovementId = usage.InventoryMovementId,
                        };
                    }
                    catch (Exception)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return new RecordIntraOpItemUsageResponseModel { Success = false, Message = "Error recording item usage." };
                    }
                });
            }
            catch (Exception)
            {
                return new RecordIntraOpItemUsageResponseModel { Success = false, Message = "Error recording item usage." };
            }
        }
    }
}
