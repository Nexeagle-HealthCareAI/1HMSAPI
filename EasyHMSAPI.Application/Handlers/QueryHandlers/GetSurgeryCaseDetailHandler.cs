using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetSurgeryCaseDetailHandler : IRequestHandler<GetSurgeryCaseDetailRequestModel, GetSurgeryCaseDetailResponseModel>
    {
        private readonly AppDbContext _context;

        public GetSurgeryCaseDetailHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetSurgeryCaseDetailResponseModel> Handle(GetSurgeryCaseDetailRequestModel request, CancellationToken cancellationToken)
        {
            var surgeryCase = await _context.SurgeryCase
                .FirstOrDefaultAsync(s => s.SurgeryCaseId == request.SurgeryCaseId && s.HospitalId == request.HospitalId, cancellationToken);
            if (surgeryCase == null)
                return new GetSurgeryCaseDetailResponseModel { Success = false, Message = "Surgery case not found." };

            var booking = await _context.OTBooking
                .Where(b => b.SurgeryCaseId == surgeryCase.SurgeryCaseId && IpdConstants.OTBookingStatus.Active.Contains(b.StatusCode))
                .FirstOrDefaultAsync(cancellationToken);
            OTBookingDetailModel? bookingModel = null;
            if (booking != null)
            {
                var theatre = await _context.OperationTheatre.FirstOrDefaultAsync(t => t.TheatreId == booking.TheatreId, cancellationToken);
                bookingModel = new OTBookingDetailModel
                {
                    OTBookingId = booking.OTBookingId,
                    TheatreId = booking.TheatreId,
                    TheatreCode = theatre?.TheatreCode,
                    TheatreName = theatre?.TheatreName,
                    ScheduledStart = booking.ScheduledStart,
                    ScheduledEnd = booking.ScheduledEnd,
                    StatusCode = booking.StatusCode,
                };
            }

            var latestPreOp = await _context.PreOpAssessment
                .Where(p => p.SurgeryCaseId == surgeryCase.SurgeryCaseId)
                .OrderByDescending(p => p.AssessedAt)
                .FirstOrDefaultAsync(cancellationToken);
            var preOpModel = latestPreOp == null ? null : new PreOpAssessmentDetailModel
            {
                PreOpAssessmentId = latestPreOp.PreOpAssessmentId,
                AsaGrade = latestPreOp.AsaGrade,
                NpoConfirmed = latestPreOp.NpoConfirmed,
                AllergiesReviewed = latestPreOp.AllergiesReviewed,
                InvestigationsReviewed = latestPreOp.InvestigationsReviewed,
                ConsentConfirmed = latestPreOp.ConsentConfirmed,
                Notes = latestPreOp.Notes,
                AssessedBy = latestPreOp.AssessedBy,
                AssessedAt = latestPreOp.AssessedAt,
            };

            var checklist = await _context.SurgicalSafetyChecklist
                .FirstOrDefaultAsync(c => c.SurgeryCaseId == surgeryCase.SurgeryCaseId, cancellationToken);
            var checklistModel = checklist == null ? null : new SurgicalSafetyChecklistDetailModel
            {
                SignInCompletedAt = checklist.SignInCompletedAt,
                SignInCompletedBy = checklist.SignInCompletedBy,
                SignInItems = DeserializeItems(checklist.SignInItemsJson),
                SignInNotes = checklist.SignInNotes,
                TimeOutCompletedAt = checklist.TimeOutCompletedAt,
                TimeOutCompletedBy = checklist.TimeOutCompletedBy,
                TimeOutItems = DeserializeItems(checklist.TimeOutItemsJson),
                TimeOutNotes = checklist.TimeOutNotes,
                SignOutCompletedAt = checklist.SignOutCompletedAt,
                SignOutCompletedBy = checklist.SignOutCompletedBy,
                SignOutItems = DeserializeItems(checklist.SignOutItemsJson),
                SignOutNotes = checklist.SignOutNotes,
            };

            var intraOp = await _context.IntraOpRecord
                .FirstOrDefaultAsync(r => r.SurgeryCaseId == surgeryCase.SurgeryCaseId, cancellationToken);
            var intraOpModel = intraOp == null ? null : new IntraOpRecordDetailModel
            {
                IntraOpRecordId = intraOp.IntraOpRecordId,
                AnaesthesiaType = intraOp.AnaesthesiaType,
                AnaesthesiaStartAt = intraOp.AnaesthesiaStartAt,
                AnaesthesiaEndAt = intraOp.AnaesthesiaEndAt,
                SurgeryStartAt = intraOp.SurgeryStartAt,
                SurgeryEndAt = intraOp.SurgeryEndAt,
                EstimatedBloodLossMl = intraOp.EstimatedBloodLossMl,
                Findings = intraOp.Findings,
                ProcedurePerformed = intraOp.ProcedurePerformed,
                SurgicalTeam = intraOp.SurgicalTeam,
                ComplicationsNotes = intraOp.ComplicationsNotes,
                RecordedBy = intraOp.RecordedBy,
                RecordedAt = intraOp.RecordedAt,
            };

            var itemsUsed = await _context.IntraOpItemUsage
                .Where(u => u.SurgeryCaseId == surgeryCase.SurgeryCaseId)
                .OrderByDescending(u => u.RecordedAt)
                .Select(u => new IntraOpItemUsageDetailModel
                {
                    IntraOpItemUsageId = u.IntraOpItemUsageId,
                    ItemName = u.ItemName,
                    Category = u.Category,
                    Qty = u.Qty,
                    LotNumber = u.LotNumber,
                    SerialNumber = u.SerialNumber,
                    IsBilled = u.ChargeEventId != null,
                    IsStockDeducted = u.InventoryMovementId != null,
                    RecordedBy = u.RecordedBy,
                    RecordedAt = u.RecordedAt,
                })
                .ToListAsync(cancellationToken);

            return new GetSurgeryCaseDetailResponseModel
            {
                Success = true,
                SurgeryCaseId = surgeryCase.SurgeryCaseId,
                AdmissionId = surgeryCase.AdmissionId,
                ProcedureName = surgeryCase.ProcedureName,
                SurgeryType = surgeryCase.SurgeryType,
                Urgency = surgeryCase.Urgency,
                StatusCode = surgeryCase.StatusCode,
                SurgeonName = surgeryCase.SurgeonName,
                AnaesthetistName = surgeryCase.AnaesthetistName,
                CancelledReason = surgeryCase.CancelledReason,
                Booking = bookingModel,
                LatestPreOpAssessment = preOpModel,
                Checklist = checklistModel,
                IntraOpRecord = intraOpModel,
                ItemsUsed = itemsUsed,
            };
        }

        private static Dictionary<string, bool>? DeserializeItems(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try { return JsonSerializer.Deserialize<Dictionary<string, bool>>(json); }
            catch { return null; }
        }
    }
}
