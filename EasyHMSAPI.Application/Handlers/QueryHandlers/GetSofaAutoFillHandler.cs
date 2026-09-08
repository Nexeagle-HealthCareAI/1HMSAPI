using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// The hybrid auto-pull side of SOFA input capture — latest VitalReading for GCS/MAP-computed,
    /// the last 24h of FluidEntry(Direction=OUT, Subtype=Urine) summed for urine output, and (when
    /// the patient has one) their most recently approved pathology report's platelets/bilirubin/
    /// creatinine (see PathologyLabValueResolver). Pure draft, nothing persisted here.
    /// </summary>
    public class GetSofaAutoFillHandler : IRequestHandler<GetSofaAutoFillRequestModel, GetSofaAutoFillResponseModel>
    {
        private readonly AppDbContext _context;

        public GetSofaAutoFillHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetSofaAutoFillResponseModel> Handle(GetSofaAutoFillRequestModel request, CancellationToken cancellationToken)
        {
            var latestVital = await _context.VitalReading
                .Where(v => v.HospitalId == request.HospitalId && v.AdmissionId == request.AdmissionId)
                .OrderByDescending(v => v.RecordedAt)
                .FirstOrDefaultAsync(cancellationToken);

            int? mapValue = null;
            if (latestVital?.SystolicBP != null && latestVital.DiastolicBP != null)
                mapValue = (int)Math.Round(latestVital.DiastolicBP.Value + (latestVital.SystolicBP.Value - latestVital.DiastolicBP.Value) / 3.0);

            var windowStart = DateTime.UtcNow.AddHours(-24);
            var urineOutput = await _context.FluidEntry
                .Where(f => f.HospitalId == request.HospitalId && f.AdmissionId == request.AdmissionId
                    && f.Direction == IpdConstants.FluidDirection.Out && f.Subtype == IpdConstants.FluidSubtype.Urine
                    && f.RecordedAt >= windowStart)
                .SumAsync(f => (decimal?)f.VolumeMl, cancellationToken);

            var patientId = await _context.Admission
                .Where(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId)
                .Select(a => a.PatientId)
                .FirstOrDefaultAsync(cancellationToken);
            var (labValues, labApprovedAt) = await PathologyLabValueResolver.GetLatestApprovedValuesAsync(
                _context, request.HospitalId, patientId, cancellationToken);

            return new GetSofaAutoFillResponseModel
            {
                MapValue = mapValue,
                GcsTotal = latestVital?.GcsTotal,
                UrineOutputMlPerDay = urineOutput,
                SourceVitalRecordedAt = latestVital?.RecordedAt,

                PlateletsCount = PathologyLabValueResolver.TryGet(labValues, "Platelet Count"),
                BilirubinMgDl = PathologyLabValueResolver.TryGet(labValues, "Bilirubin - Total"),
                CreatinineMgDl = PathologyLabValueResolver.TryGet(labValues, "Serum Creatinine"),
                SourceLabReportApprovedAt = labApprovedAt,
            };
        }
    }
}
