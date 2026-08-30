using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// The hybrid auto-pull side of APACHE II input capture — pulls the admission's latest
    /// VitalReading (temp/HR/RR/GCS/MAP-computed-from-BP), the patient's age, and (when the
    /// patient has one) their most recently approved pathology report's sodium/potassium/
    /// creatinine/hematocrit/WBC (see PathologyLabValueResolver). ArterialPh/FiO2/PaO2 stay null —
    /// ABG isn't one of the seeded catalog panels. Nothing here is persisted — this is a pure
    /// draft, same "compose fresh, never save" contract as GetDischargeSummaryDraftHandler.
    /// </summary>
    public class GetApacheIIAutoFillHandler : IRequestHandler<GetApacheIIAutoFillRequestModel, GetApacheIIAutoFillResponseModel>
    {
        private readonly AppDbContext _context;

        public GetApacheIIAutoFillHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetApacheIIAutoFillResponseModel> Handle(GetApacheIIAutoFillRequestModel request, CancellationToken cancellationToken)
        {
            var admission = await _context.Admission
                .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
            if (admission == null)
                return new GetApacheIIAutoFillResponseModel();

            var latestVital = await _context.VitalReading
                .Where(v => v.HospitalId == request.HospitalId && v.AdmissionId == request.AdmissionId)
                .OrderByDescending(v => v.RecordedAt)
                .FirstOrDefaultAsync(cancellationToken);

            int? mapValue = null;
            if (latestVital?.SystolicBP != null && latestVital.DiastolicBP != null)
                mapValue = (int)Math.Round(latestVital.DiastolicBP.Value + (latestVital.SystolicBP.Value - latestVital.DiastolicBP.Value) / 3.0);

            var patient = admission.PatientId == null ? null : await _context.PatientRegistrations
                .FirstOrDefaultAsync(p => p.HospitalId == request.HospitalId && p.PatientId == admission.PatientId, cancellationToken);

            var (labValues, labApprovedAt) = await PathologyLabValueResolver.GetLatestApprovedValuesAsync(
                _context, request.HospitalId, admission.PatientId, cancellationToken);

            return new GetApacheIIAutoFillResponseModel
            {
                Temperature = latestVital?.Temperature,
                MapValue = mapValue,
                HeartRate = latestVital?.Pulse,
                RespiratoryRate = latestVital?.RespiratoryRate,
                GcsTotal = latestVital?.GcsTotal,
                AgeYears = patient?.Age.HasValue == true ? (int)patient.Age!.Value : null,
                SourceVitalRecordedAt = latestVital?.RecordedAt,

                SerumSodium = PathologyLabValueResolver.TryGet(labValues, "Serum Sodium (Na+)"),
                SerumPotassium = PathologyLabValueResolver.TryGet(labValues, "Serum Potassium (K+)"),
                SerumCreatinine = PathologyLabValueResolver.TryGet(labValues, "Serum Creatinine"),
                Hematocrit = PathologyLabValueResolver.TryGet(labValues, "PCV / Hematocrit"),
                Wbc = PathologyLabValueResolver.TryGet(labValues, "Total WBC Count (TLC)"),
                SourceLabReportApprovedAt = labApprovedAt,
            };
        }
    }
}
