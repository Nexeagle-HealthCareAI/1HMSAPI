using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetPatientBillingEventsHandler : IRequestHandler<GetPatientBillingEventsRequestModel, GetPatientBillingEventsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetPatientBillingEventsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetPatientBillingEventsResponseModel> Handle(GetPatientBillingEventsRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                var encounters = await _context.Encounter
                    .Where(e => e.PatientId == request.PatientId && e.HospitalId == request.HospitalId)
                    .ToListAsync(cancellationToken);

                if (encounters.Count == 0)
                {
                    return new GetPatientBillingEventsResponseModel
                    {
                        Success = false,
                        Message = $"No encounters found for patient {request.PatientId}."
                    };
                }

                var encounterIds = encounters.Select(e => e.EncounterId).ToList();

                var invoices = await _context.BillingInvoice
                    .Where(bi => encounterIds.Contains(bi.EncounterId))
                    .Join(_context.Encounter,
                          bi => bi.EncounterId,
                          e => e.EncounterId,
                          (bi, e) => new { Invoice = bi, Encounter = e })
                    .ToListAsync(cancellationToken);

                var encounterInvoiceDetails = new List<PatientEncounterInvoiceDetail>();

                foreach (var invoiceGroup in invoices)
                {
                    var doctorName = await _context.Doctors
                        .Where(d => d.DoctorID == invoiceGroup.Encounter.PrimaryDoctorId)
                        .Join(_context.UserProfiles,
                              d => d.UserID,
                              u => u.UserID,
                              (d, u) => u.FullName)
                        .FirstOrDefaultAsync(cancellationToken);

                    bool isCancelled = invoiceGroup.Invoice.StatusCode == BillingConstants.InvoiceStatus.Cancelled;

                    encounterInvoiceDetails.Add(new PatientEncounterInvoiceDetail
                    {
                        EncounterId = invoiceGroup.Invoice.EncounterId,
                        InvoiceNo = invoiceGroup.Invoice.InvoiceNo,
                        InvoiceId = invoiceGroup.Invoice.InvoiceId,
                        InvoiceDate = invoiceGroup.Invoice.InvoiceDate,
                        DoctorName = doctorName,
                        Status = invoiceGroup.Invoice.StatusCode,
                        UpdatedAt = invoiceGroup.Invoice.UpdatedAt,
                        UpdatedBy = invoiceGroup.Invoice.UpdatedBy,
                        IsCancelled = isCancelled,
                        CancelReason = isCancelled ? invoiceGroup.Invoice.CancelReason : null
                    });
                }

                return new GetPatientBillingEventsResponseModel
                {
                    Success = true,
                    Message = "Patient billing events retrieved successfully.",
                    Data = new GetPatientBillingEventsData
                    {
                        PatientId = request.PatientId,
                        Encounters = encounterInvoiceDetails
                    }
                };
            }
            catch (Exception)
            {
                return new GetPatientBillingEventsResponseModel
                {
                    Success = false,
                    Message = "Error retrieving patient billing events."
                };
            }
        }
    }
}
