using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// Reopens the most recent closed interim bill for an admission (audit reason required).
    /// The snapshot lines are removed so their charges become billable again, and the next
    /// close re-captures them. Only the latest day can be reopened to keep day numbering and
    /// the cumulative running total consistent.
    /// </summary>
    public class ReopenAdmissionDayHandler : IRequestHandler<ReopenAdmissionDayRequestModel, ReopenAdmissionDayResponseModel>
    {
        private readonly AppDbContext _context;

        public ReopenAdmissionDayHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<ReopenAdmissionDayResponseModel> Handle(ReopenAdmissionDayRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionDayBillId == Guid.Empty)
                    return new ReopenAdmissionDayResponseModel { Success = false, Message = "HospitalId and AdmissionDayBillId are required." };

                if (string.IsNullOrWhiteSpace(request.Reason))
                    return new ReopenAdmissionDayResponseModel { Success = false, Message = "Reason is required to reopen." };

                var bill = await _context.AdmissionDayBill
                    .FirstOrDefaultAsync(b => b.AdmissionDayBillId == request.AdmissionDayBillId && b.HospitalId == request.HospitalId, cancellationToken);
                if (bill == null)
                    return new ReopenAdmissionDayResponseModel { Success = false, Message = "Interim bill not found." };

                if (bill.StatusCode != BillingConstants.DayBillStatus.Closed)
                    return new ReopenAdmissionDayResponseModel { Success = false, Message = "This interim bill is not closed." };

                var maxClosedDay = await _context.AdmissionDayBill
                    .Where(b => b.EncounterId == bill.EncounterId && b.HospitalId == request.HospitalId
                                && b.StatusCode == BillingConstants.DayBillStatus.Closed)
                    .MaxAsync(b => (int?)b.DayNumber, cancellationToken) ?? 0;

                if (bill.DayNumber != maxClosedDay)
                    return new ReopenAdmissionDayResponseModel { Success = false, Message = "Only the most recent interim bill can be reopened." };

                var now = DateTime.UtcNow;

                var lines = await _context.AdmissionDayBillLine
                    .Where(l => l.AdmissionDayBillId == bill.AdmissionDayBillId)
                    .ToListAsync(cancellationToken);
                _context.AdmissionDayBillLine.RemoveRange(lines);

                bill.StatusCode = BillingConstants.DayBillStatus.Reopened;
                bill.ReopenedAt = now;
                bill.ReopenedBy = request.LoggedInUserName;
                bill.ReopenReason = request.Reason;
                bill.UpdatedAt = now;
                bill.UpdatedBy = request.LoggedInUserName;
                _context.AdmissionDayBill.Update(bill);

                await _context.SaveChangesAsync(cancellationToken);

                return new ReopenAdmissionDayResponseModel { Success = true, Message = "Interim bill reopened. Its charges are billable again." };
            }
            catch (Exception)
            {
                return new ReopenAdmissionDayResponseModel { Success = false, Message = "Error reopening interim bill." };
            }
        }
    }
}
