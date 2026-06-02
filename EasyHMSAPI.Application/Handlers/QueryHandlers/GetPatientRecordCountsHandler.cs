using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>Counts a patient's linked records (admissions, appointments, bills, etc.) for the merge preview.</summary>
    public class GetPatientRecordCountsHandler : IRequestHandler<GetPatientRecordCountsRequestModel, GetPatientRecordCountsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetPatientRecordCountsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetPatientRecordCountsResponseModel> Handle(GetPatientRecordCountsRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || string.IsNullOrWhiteSpace(request.PatientId))
                    return new GetPatientRecordCountsResponseModel { Success = false, Message = "HospitalId and PatientId are required." };

                var id = request.PatientId.Trim();
                var p = await _context.PatientRegistrations
                    .FirstOrDefaultAsync(x => x.PatientId == id && x.HospitalId == request.HospitalId, cancellationToken);
                if (p == null)
                    return new GetPatientRecordCountsResponseModel { Success = false, Message = "Patient not found." };

                var res = new GetPatientRecordCountsResponseModel
                {
                    Success = true,
                    PatientId = id,
                    FullName = p.FullName,
                    Mobile = p.Mobile,
                    IsMerged = !string.IsNullOrWhiteSpace(p.MergedIntoPatientId),
                    MergedIntoPatientId = p.MergedIntoPatientId,
                    Admissions = await _context.Admission.CountAsync(x => x.PatientId == id, cancellationToken),
                    Appointments = await _context.Appointments.CountAsync(x => x.PatientId == id, cancellationToken),
                    Invoices = await _context.BillingInvoice.CountAsync(x => x.PatientId == id, cancellationToken),
                    Payments = await _context.BillingPayment.CountAsync(x => x.PatientId == id, cancellationToken),
                    Prescriptions = await _context.Prescription.CountAsync(x => x.PatientId == id, cancellationToken),
                    Encounters = await _context.Encounter.CountAsync(x => x.PatientId == id, cancellationToken),
                    Alerts = await _context.Alert.CountAsync(x => x.PatientId == id, cancellationToken),
                };
                res.Total = res.Admissions + res.Appointments + res.Invoices + res.Payments + res.Prescriptions + res.Encounters + res.Alerts;
                return res;
            }
            catch (Exception)
            {
                return new GetPatientRecordCountsResponseModel { Success = false, Message = "Error loading record counts." };
            }
        }
    }
}
