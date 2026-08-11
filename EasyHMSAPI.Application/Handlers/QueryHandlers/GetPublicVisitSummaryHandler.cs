using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetPublicVisitSummaryHandler : IRequestHandler<GetPublicVisitSummaryRequestModel, GetPublicVisitSummaryResponseModel>
    {
        private readonly AppDbContext _context;

        public GetPublicVisitSummaryHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetPublicVisitSummaryResponseModel> Handle(GetPublicVisitSummaryRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.AppointmentId == Guid.Empty)
                    return new GetPublicVisitSummaryResponseModel { Success = false, Message = "Invalid link." };

                var appointment = await _context.Appointments
                    .FirstOrDefaultAsync(a => a.ApptId == request.AppointmentId, cancellationToken);
                if (appointment == null || string.IsNullOrEmpty(appointment.PdfUrl))
                    return new GetPublicVisitSummaryResponseModel { Success = false, Message = "No prescription is available for this appointment." };

                return new GetPublicVisitSummaryResponseModel
                {
                    Success = true,
                    RedirectUrl = appointment.PdfUrl,
                    FileName = $"Prescription_{appointment.ApptId}.pdf",
                };
            }
            catch (Exception)
            {
                return new GetPublicVisitSummaryResponseModel { Success = false, Message = "Error loading the prescription." };
            }
        }
    }
}
