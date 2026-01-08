using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetPatientVisitSummaryPdfHandler : IRequestHandler<GetPatientVisitSummaryPdfRequestModel, GetPatientVisitSummaryPdfResponseModel>
    {
        private readonly AppDbContext _context;

        public GetPatientVisitSummaryPdfHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetPatientVisitSummaryPdfResponseModel> Handle(GetPatientVisitSummaryPdfRequestModel request, CancellationToken cancellationToken)
        {
            GetPatientVisitSummaryPdfResponseModel response = new()
            {
                Success = false
            };

            try
            {
                var appointment = await _context.Appointments
                    .Where(a => a.ApptId == request.AppointmentId && a.CurrentStatusCode == AppConstants.AppointmentStatus_Completed)
                    .Select(x => new
                    {
                        x.ApptId,
                        x.PdfUrl,
                        x.PatientId
                    })
                    .FirstOrDefaultAsync(cancellationToken);
                if (appointment is not null)
                {
                    if (!string.IsNullOrEmpty(appointment.PdfUrl))
                    {
                        response.Success = true;
                        response.PdfUrl = appointment.PdfUrl;
                        response.Message = "PDF retrieved successfully.";
                    }
                    else
                    {
                        response.Message = "No PDF available for this appointment.";
                    }
                }
                else
                {
                    response.Message = "Invalid appointment or appointment not completed.";
                }
            }
            catch (Exception ex)
            {
                response.Message = ex.Message + ex.InnerException + ex.StackTrace;
            }

            return response;
        }
    }
}
