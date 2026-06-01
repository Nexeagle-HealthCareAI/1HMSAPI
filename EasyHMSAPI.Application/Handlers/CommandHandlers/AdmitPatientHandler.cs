using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// Minimal admit: creates an Admission for a billing encounter so day-wise interim billing
    /// has an admission anchor (AdmittedAt). Idempotent — if the encounter already has an active
    /// admission it is returned instead of creating a duplicate.
    /// </summary>
    public class AdmitPatientHandler : IRequestHandler<AdmitPatientRequestModel, AdmitPatientResponseModel>
    {
        private const string StatusAdmitted = "ADMITTED";
        private readonly AppDbContext _context;

        public AdmitPatientHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<AdmitPatientResponseModel> Handle(AdmitPatientRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.EncounterId == Guid.Empty || string.IsNullOrWhiteSpace(request.PatientId))
                    return new AdmitPatientResponseModel { Success = false, Message = "HospitalId, PatientId and EncounterId are required." };

                var existing = await _context.Admission
                    .Where(a => a.EncounterId == request.EncounterId && a.HospitalId == request.HospitalId && a.StatusCode == StatusAdmitted)
                    .OrderByDescending(a => a.AdmittedAt)
                    .FirstOrDefaultAsync(cancellationToken);
                if (existing != null)
                {
                    return new AdmitPatientResponseModel
                    {
                        Success = true,
                        Message = "Encounter is already admitted.",
                        AdmissionId = existing.AdmissionId,
                        AdmissionNo = existing.AdmissionNo,
                        AdmittedAt = existing.AdmittedAt,
                        WasExisting = true,
                    };
                }

                var now = DateTime.UtcNow;

                var numberSeries = await NumberSeriesDefaults.GetOrCreateAsync(
                    _context, request.HospitalId, BillingConstants.NumberSeriesCode.Admission, request.LoggedInUserName, cancellationToken);
                numberSeries.CurrentValue++;
                var admissionNo = NumberSeriesFormatter.Format(
                    numberSeries.Prefix, numberSeries.YearFormat, numberSeries.Separator, numberSeries.PadLength, numberSeries.CurrentValue);
                numberSeries.UpdatedAt = now;
                numberSeries.UpdatedBy = request.LoggedInUserName;

                var admittedAt = request.AdmittedAt ?? now;
                var admission = new Admission
                {
                    AdmissionId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    PatientId = request.PatientId!,
                    EncounterId = request.EncounterId,
                    PrimaryDoctorId = request.PrimaryDoctorId,
                    AdmissionNo = admissionNo,
                    AdmittedAt = admittedAt,
                    AdmittedBy = request.LoggedInUserName,
                    ExpectedDischargeAt = request.ExpectedDischargeAt,
                    StatusCode = StatusAdmitted,
                    AdmissionReason = request.AdmissionReason,
                    Diagnosis = request.Diagnosis,
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName,
                };
                _context.Admission.Add(admission);
                await _context.SaveChangesAsync(cancellationToken);

                return new AdmitPatientResponseModel
                {
                    Success = true,
                    Message = $"Admitted. {admissionNo}",
                    AdmissionId = admission.AdmissionId,
                    AdmissionNo = admissionNo,
                    AdmittedAt = admittedAt,
                    WasExisting = false,
                };
            }
            catch (Exception)
            {
                return new AdmitPatientResponseModel { Success = false, Message = "Error admitting patient." };
            }
        }
    }
}
