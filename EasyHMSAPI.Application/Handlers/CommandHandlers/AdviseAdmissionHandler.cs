using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class AdviseAdmissionHandler : IRequestHandler<AdviseAdmissionRequestModel, AdviseAdmissionResponseModel>
    {
        private static readonly string[] ValidCaseTypes = { "EMERGENCY", "PLANNED", "URGENT" };

        private readonly AppDbContext _context;

        public AdviseAdmissionHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<AdviseAdmissionResponseModel> Handle(AdviseAdmissionRequestModel request, CancellationToken cancellationToken)
        {
            AdviseAdmissionResponseModel response = new() { Success = false };
            try
            {
                if (string.IsNullOrWhiteSpace(request.PatientId))
                {
                    response.Message = "PatientId is required.";
                    return response;
                }

                var caseType = (request.CaseType ?? string.Empty).Trim().ToUpperInvariant();
                if (!ValidCaseTypes.Contains(caseType))
                {
                    response.Message = "CaseType must be one of EMERGENCY, PLANNED, URGENT.";
                    return response;
                }

                var doctor = await _context.Doctors
                    .FirstOrDefaultAsync(d => d.DoctorID == request.ReferringDoctorId, cancellationToken);
                if (doctor == null)
                {
                    response.Message = "Referring doctor not found.";
                    return response;
                }

                var procedureName = request.ProcedureName;
                if (request.OtPlanId.HasValue && request.OtPlanId != Guid.Empty)
                {
                    var plan = await _context.OTPlans
                        .FirstOrDefaultAsync(p => p.OtPlanId == request.OtPlanId && p.HospitalId == request.HospitalId, cancellationToken);
                    if (plan == null)
                    {
                        response.Message = "Selected OT Plan not found.";
                        return response;
                    }
                    if (string.IsNullOrWhiteSpace(procedureName))
                        procedureName = plan.ProcedureName;
                }

                var referral = new AdmissionReferral
                {
                    ReferralId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    PatientId = request.PatientId.Trim(),
                    ReferringDoctorId = request.ReferringDoctorId,
                    AppointmentId = request.AppointmentId,
                    OtPlanId = request.OtPlanId,
                    PackageTypeId = request.PackageTypeId,
                    ProcedureName = procedureName,
                    ProbableAdmissionDate = request.ProbableAdmissionDate,
                    CaseType = caseType,
                    Notes = request.Notes,
                    StatusCode = "PENDING",
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = request.LoggedInUserName,
                };
                _context.AdmissionReferrals.Add(referral);

                _context.AdmissionReferralStatusHistories.Add(new AdmissionReferralStatusHistory
                {
                    HistoryId = Guid.NewGuid(),
                    ReferralId = referral.ReferralId,
                    StatusCode = "PENDING",
                    ChangedAt = DateTime.UtcNow,
                    ChangedBy = request.LoggedInUserName,
                });

                await _context.SaveChangesAsync(cancellationToken);

                response.Success = true;
                response.Message = "Admission advised successfully.";
                response.ReferralId = referral.ReferralId;
            }
            catch (Exception ex)
            {
                response.Message = "An error occurred: " + ex.Message + ex.InnerException + ex.StackTrace;
            }

            return response;
        }
    }
}
