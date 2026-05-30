using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetHospitalBillingDashboardHandler : IRequestHandler<GetHospitalBillingDashboardRequestModel, GetHospitalBillingDashboardResponseModel>
    {
        private readonly AppDbContext _context;

        public GetHospitalBillingDashboardHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetHospitalBillingDashboardResponseModel> Handle(GetHospitalBillingDashboardRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                var encounters = await _context.Encounter
                    .Where(e => e.HospitalId == request.HospitalId)
                    .ToListAsync(cancellationToken);

                if (encounters.Count == 0)
                {
                    return new GetHospitalBillingDashboardResponseModel
                    {
                        Success = true,
                        Message = "No billing data found for this hospital.",
                        Data = new List<HospitalBillingDashboardData>()
                    };
                }

                var encounterIds = encounters.Select(e => e.EncounterId).ToList();

                var invoices = await _context.BillingInvoice
                    .Where(bi => encounterIds.Contains(bi.EncounterId))
                    .ToListAsync(cancellationToken);

                var groupedByPatient = encounters.GroupBy(e => e.PatientId);

                var dashboardData = new List<HospitalBillingDashboardData>();

                foreach (var patientGroup in groupedByPatient)
                {
                    var patientId = patientGroup.Key;
                    var patientEncounters = patientGroup.ToList();

                    var encounterDetails = new List<DashboardEncounterDetail>();

                    foreach (var encounter in patientEncounters)
                    {
                        var invoice = invoices.FirstOrDefault(bi => bi.EncounterId == encounter.EncounterId);

                        if (invoice != null)
                        {
                            var doctorName = await _context.Doctors
                                .Where(d => d.DoctorID == encounter.PrimaryDoctorId)
                                .Join(_context.UserProfiles,
                                      d => d.UserID,
                                      u => u.UserID,
                                      (d, u) => u.FullName)
                                .FirstOrDefaultAsync(cancellationToken);

                            var totalPaid = await _context.BillingPaymentAllocation
                                .Where(bpa => bpa.InvoiceId == invoice.InvoiceId)
                                .SumAsync(bpa => bpa.AllocatedAmount, cancellationToken);

                            decimal netAmount = invoice.NetAmount ?? 0;
                            decimal dueAmount = netAmount - totalPaid;

                            bool isCancelled = invoice.StatusCode == BillingConstants.InvoiceStatus.Cancelled;

                            encounterDetails.Add(new DashboardEncounterDetail
                            {
                                EncounterId = encounter.EncounterId,
                                VisitType = encounter.EncounterTypeCode,
                                InvoiceNo = invoice.InvoiceNo,
                                InvoiceId = invoice.InvoiceId,
                                InvoiceDate = invoice.InvoiceDate,
                                DoctorName = doctorName,
                                NetAmount = netAmount,
                                DueAmount = dueAmount,
                                PaidAmount = totalPaid,
                                Status = invoice.StatusCode,
                                UpdatedAt = invoice.UpdatedAt,
                                UpdatedBy = invoice.UpdatedBy,
                                IsCancelled = isCancelled,
                                CancelReason = isCancelled ? invoice.CancelReason : null
                            });
                        }
                    }

                    if (encounterDetails.Count > 0)
                    {
                        dashboardData.Add(new HospitalBillingDashboardData
                        {
                            PatientId = patientId,
                            PatientName = "",
                            Encounters = encounterDetails
                        });
                    }
                }

                return new GetHospitalBillingDashboardResponseModel
                {
                    Success = true,
                    Message = "Hospital billing dashboard data retrieved successfully.",
                    Data = dashboardData
                };
            }
            catch (Exception)
            {
                return new GetHospitalBillingDashboardResponseModel
                {
                    Success = false,
                    Message = "Error retrieving dashboard data."
                };
            }
        }
    }
}
