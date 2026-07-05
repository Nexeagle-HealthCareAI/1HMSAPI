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
    /// Wires the dead-scaffolded BloodBag/TransfusionEvent schema. Hospital-wide — any admission
    /// can reserve/receive a transfusion, not gated behind an OT/surgery case.
    /// </summary>
    public class BloodBankCommandHandlers :
        IRequestHandler<ReceiveBloodBagRequestModel, ReceiveBloodBagResponseModel>,
        IRequestHandler<ReserveBloodBagRequestModel, ReserveBloodBagResponseModel>,
        IRequestHandler<DiscardBloodBagRequestModel, DiscardBloodBagResponseModel>,
        IRequestHandler<RecordTransfusionRequestModel, RecordTransfusionResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IMediator _mediator;

        public BloodBankCommandHandlers(AppDbContext context, IMediator mediator)
        {
            _context = context;
            _mediator = mediator;
        }

        public async Task<ReceiveBloodBagResponseModel> Handle(ReceiveBloodBagRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || string.IsNullOrWhiteSpace(request.BagNumber))
                    return new ReceiveBloodBagResponseModel { Success = false, Message = "HospitalId and BagNumber are required." };

                var component = request.Component?.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(component) || !IpdConstants.BloodComponent.All.Contains(component))
                    return new ReceiveBloodBagResponseModel { Success = false, Message = "Invalid component." };

                var bloodGroup = request.BloodGroup?.Trim().ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(bloodGroup) || !IpdConstants.BloodGroup.All.Contains(bloodGroup))
                    return new ReceiveBloodBagResponseModel { Success = false, Message = "Invalid blood group." };

                if (request.ExpiresAt <= request.CollectedAt)
                    return new ReceiveBloodBagResponseModel { Success = false, Message = "ExpiresAt must be after CollectedAt." };

                var exists = await _context.BloodBag.AnyAsync(
                    b => b.HospitalId == request.HospitalId && b.BagNumber == request.BagNumber.Trim(), cancellationToken);
                if (exists)
                    return new ReceiveBloodBagResponseModel { Success = false, Message = "A bag with this number already exists." };

                var now = DateTime.UtcNow;
                var bag = new BloodBag
                {
                    BloodBagId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    BagNumber = request.BagNumber.Trim(),
                    Component = component,
                    BloodGroup = bloodGroup,
                    VolumeMl = request.VolumeMl,
                    DonorRef = string.IsNullOrWhiteSpace(request.DonorRef) ? null : request.DonorRef.Trim(),
                    CollectedAt = request.CollectedAt,
                    ExpiresAt = request.ExpiresAt,
                    StorageLocation = string.IsNullOrWhiteSpace(request.StorageLocation) ? null : request.StorageLocation.Trim(),
                    StoreId = request.StoreId,
                    Status = IpdConstants.BloodBagStatus.Available,
                    ChargeId = request.ChargeId,
                    UnitRate = request.UnitRate,
                    IsTaxable = false,
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName,
                };
                _context.BloodBag.Add(bag);
                await _context.SaveChangesAsync(cancellationToken);

                return new ReceiveBloodBagResponseModel { Success = true, Message = "Bag received.", BloodBagId = bag.BloodBagId };
            }
            catch (Exception)
            {
                return new ReceiveBloodBagResponseModel { Success = false, Message = "Error receiving blood bag." };
            }
        }

        public async Task<ReserveBloodBagResponseModel> Handle(ReserveBloodBagRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.BloodBagId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new ReserveBloodBagResponseModel { Success = false, Message = "HospitalId, BloodBagId, and AdmissionId are required." };

                var crossmatchResult = request.CrossmatchResult?.Trim().ToUpperInvariant() ?? IpdConstants.CrossmatchResult.NotDone;
                if (crossmatchResult != IpdConstants.CrossmatchResult.Compatible
                    && crossmatchResult != IpdConstants.CrossmatchResult.Incompatible
                    && crossmatchResult != IpdConstants.CrossmatchResult.NotDone)
                    return new ReserveBloodBagResponseModel { Success = false, Message = "Invalid crossmatch result." };

                var bag = await _context.BloodBag
                    .FirstOrDefaultAsync(b => b.BloodBagId == request.BloodBagId && b.HospitalId == request.HospitalId, cancellationToken);
                if (bag == null)
                    return new ReserveBloodBagResponseModel { Success = false, Message = "Blood bag not found." };
                if (bag.Status != IpdConstants.BloodBagStatus.Available)
                    return new ReserveBloodBagResponseModel { Success = false, Message = $"Bag is {bag.Status.ToLowerInvariant()}, not available to reserve." };

                var admission = await _context.Admission
                    .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
                if (admission == null)
                    return new ReserveBloodBagResponseModel { Success = false, Message = "Admission not found." };

                var now = DateTime.UtcNow;
                bag.Status = IpdConstants.BloodBagStatus.Reserved;
                bag.ReservedForAdmissionId = admission.AdmissionId;
                bag.ReservedForEncounterId = admission.EncounterId;
                bag.ReservedForPatientId = admission.PatientId;
                bag.CrossmatchResult = crossmatchResult;
                bag.CrossmatchBy = request.LoggedInUserName;
                bag.ReservedAt = now;
                bag.ReservedBy = request.LoggedInUserName;
                bag.UpdatedAt = now;
                bag.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);

                return new ReserveBloodBagResponseModel { Success = true, Message = "Bag reserved." };
            }
            catch (Exception)
            {
                return new ReserveBloodBagResponseModel { Success = false, Message = "Error reserving blood bag." };
            }
        }

        public async Task<DiscardBloodBagResponseModel> Handle(DiscardBloodBagRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.BloodBagId == Guid.Empty || string.IsNullOrWhiteSpace(request.DiscardReason))
                    return new DiscardBloodBagResponseModel { Success = false, Message = "HospitalId, BloodBagId, and DiscardReason are required." };

                var bag = await _context.BloodBag
                    .FirstOrDefaultAsync(b => b.BloodBagId == request.BloodBagId && b.HospitalId == request.HospitalId, cancellationToken);
                if (bag == null)
                    return new DiscardBloodBagResponseModel { Success = false, Message = "Blood bag not found." };
                if (bag.Status == IpdConstants.BloodBagStatus.Transfused || bag.Status == IpdConstants.BloodBagStatus.Discarded)
                    return new DiscardBloodBagResponseModel { Success = false, Message = $"Bag is already {bag.Status.ToLowerInvariant()}." };

                var now = DateTime.UtcNow;
                bag.Status = IpdConstants.BloodBagStatus.Discarded;
                bag.DiscardedAt = now;
                bag.DiscardedBy = request.LoggedInUserName;
                bag.DiscardReason = request.DiscardReason.Trim();
                bag.UpdatedAt = now;
                bag.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);

                return new DiscardBloodBagResponseModel { Success = true, Message = "Bag discarded." };
            }
            catch (Exception)
            {
                return new DiscardBloodBagResponseModel { Success = false, Message = "Error discarding blood bag." };
            }
        }

        public async Task<RecordTransfusionResponseModel> Handle(RecordTransfusionRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.BloodBagId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new RecordTransfusionResponseModel { Success = false, Message = "HospitalId, BloodBagId, and AdmissionId are required." };

                var reaction = request.Reaction?.Trim().ToUpperInvariant() ?? IpdConstants.TransfusionReaction.None;
                if (!IpdConstants.TransfusionReaction.All.Contains(reaction))
                    return new RecordTransfusionResponseModel { Success = false, Message = "Invalid reaction value." };
                if (reaction != IpdConstants.TransfusionReaction.None && string.IsNullOrWhiteSpace(request.ReactionNotes))
                    return new RecordTransfusionResponseModel { Success = false, Message = "Reaction notes are required when a reaction is recorded." };
                if (string.IsNullOrWhiteSpace(request.WitnessName))
                    return new RecordTransfusionResponseModel { Success = false, Message = "A witness name is required." };

                var bag = await _context.BloodBag
                    .FirstOrDefaultAsync(b => b.BloodBagId == request.BloodBagId && b.HospitalId == request.HospitalId, cancellationToken);
                if (bag == null)
                    return new RecordTransfusionResponseModel { Success = false, Message = "Blood bag not found." };
                if (bag.Status != IpdConstants.BloodBagStatus.Available && bag.Status != IpdConstants.BloodBagStatus.Reserved)
                    return new RecordTransfusionResponseModel { Success = false, Message = $"Bag is {bag.Status.ToLowerInvariant()}, cannot transfuse." };

                var admission = await _context.Admission
                    .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
                if (admission == null)
                    return new RecordTransfusionResponseModel { Success = false, Message = "Admission not found." };

                var strategy = _context.Database.CreateExecutionStrategy();
                return await strategy.ExecuteAsync(async () =>
                {
                    await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
                    try
                    {
                        var now = DateTime.UtcNow;
                        var transfusion = new TransfusionEvent
                        {
                            TransfusionEventId = Guid.NewGuid(),
                            HospitalId = request.HospitalId,
                            BloodBagId = bag.BloodBagId,
                            AdmissionId = admission.AdmissionId,
                            EncounterId = admission.EncounterId,
                            PatientId = admission.PatientId,
                            StartedAt = request.StartedAt,
                            EndedAt = request.EndedAt,
                            VolumeGivenMl = request.VolumeGivenMl,
                            VitalsBefore = string.IsNullOrWhiteSpace(request.VitalsBefore) ? null : request.VitalsBefore.Trim(),
                            VitalsAfter = string.IsNullOrWhiteSpace(request.VitalsAfter) ? null : request.VitalsAfter.Trim(),
                            Reaction = reaction,
                            ReactionNotes = string.IsNullOrWhiteSpace(request.ReactionNotes) ? null : request.ReactionNotes.Trim(),
                            AdministeredBy = request.LoggedInUserName ?? "Unknown",
                            AdministeredByUserId = request.LoggedInUserId,
                            WitnessName = request.WitnessName.Trim(),
                            WitnessUserId = request.WitnessUserId,
                            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                            CreatedAt = now,
                            CreatedBy = request.LoggedInUserName,
                        };
                        _context.TransfusionEvent.Add(transfusion);

                        bag.Status = IpdConstants.BloodBagStatus.Transfused;
                        bag.UpdatedAt = now;
                        bag.UpdatedBy = request.LoggedInUserName;

                        // ── Optional charge-on-event, same guard CPOE uses ──────────────────────
                        if (request.ChargeId.HasValue && admission.EncounterId.HasValue)
                        {
                            var chargeResponse = await _mediator.Send(new AddChargeEventRequestModel
                            {
                                HospitalId = request.HospitalId,
                                PatientId = admission.PatientId,
                                EncounterId = admission.EncounterId.Value,
                                Charges = new List<ChargeDetail>
                                {
                                    new ChargeDetail
                                    {
                                        ChargeId = request.ChargeId,
                                        DisplayName = $"Blood transfusion — {bag.Component} ({bag.BagNumber})",
                                        Qty = 1,
                                        Rate = request.Rate ?? bag.UnitRate ?? 0,
                                        DiscountPercent = 0,
                                        CategoryCode = "BLOOD_BANK",
                                    },
                                },
                                LoggedInUserName = request.LoggedInUserName,
                                LoggedInUserId = request.LoggedInUserId,
                            }, cancellationToken);

                            if (chargeResponse.Success != true || chargeResponse.Data?.ChargeEvents == null || chargeResponse.Data.ChargeEvents.Count == 0)
                            {
                                await tx.RollbackAsync(cancellationToken);
                                return new RecordTransfusionResponseModel
                                {
                                    Success = false,
                                    Message = chargeResponse.Message ?? "Could not post the transfusion charge.",
                                };
                            }

                            transfusion.ChargeEventId = chargeResponse.Data.ChargeEvents[0].ChargeEventId;
                            bag.ChargeId = request.ChargeId;
                        }

                        await _context.SaveChangesAsync(cancellationToken);
                        await tx.CommitAsync(cancellationToken);

                        return new RecordTransfusionResponseModel
                        {
                            Success = true,
                            Message = "Transfusion recorded.",
                            TransfusionEventId = transfusion.TransfusionEventId,
                            ChargeEventId = transfusion.ChargeEventId,
                        };
                    }
                    catch (Exception)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return new RecordTransfusionResponseModel { Success = false, Message = "Error recording transfusion." };
                    }
                });
            }
            catch (Exception)
            {
                return new RecordTransfusionResponseModel { Success = false, Message = "Error recording transfusion." };
            }
        }
    }
}
