using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Pulls the latest VitalReading into a draft EWS input set (RR/SpO2/SystolicBP/Pulse/Temp) --
    /// same "auto-pull + nothing persisted" shape as GetSofaAutoFillHandler. SupplementalOxygen and
    /// ConsciousnessLevel are left for the nurse to confirm; neither is reliably inferable from the
    /// existing vitals record.
    /// </summary>
    public class GetEarlyWarningAutoFillHandler : IRequestHandler<GetEarlyWarningAutoFillRequestModel, GetEarlyWarningAutoFillResponseModel>
    {
        private readonly AppDbContext _context;

        public GetEarlyWarningAutoFillHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetEarlyWarningAutoFillResponseModel> Handle(GetEarlyWarningAutoFillRequestModel request, CancellationToken cancellationToken)
        {
            var latestVital = await _context.VitalReading
                .Where(v => v.HospitalId == request.HospitalId && v.AdmissionId == request.AdmissionId)
                .OrderByDescending(v => v.RecordedAt)
                .FirstOrDefaultAsync(cancellationToken);

            decimal? temperatureC = null;
            if (latestVital?.Temperature != null)
            {
                temperatureC = string.Equals(latestVital.TemperatureUnit, "F", StringComparison.OrdinalIgnoreCase)
                    ? Math.Round((latestVital.Temperature.Value - 32) * 5 / 9, 1)
                    : latestVital.Temperature.Value;
            }

            return new GetEarlyWarningAutoFillResponseModel
            {
                RespiratoryRate = latestVital?.RespiratoryRate,
                Spo2 = latestVital?.SpO2,
                SystolicBp = latestVital?.SystolicBP,
                Pulse = latestVital?.Pulse,
                TemperatureC = temperatureC,
                SourceVitalRecordedAt = latestVital?.RecordedAt,
            };
        }
    }
}
