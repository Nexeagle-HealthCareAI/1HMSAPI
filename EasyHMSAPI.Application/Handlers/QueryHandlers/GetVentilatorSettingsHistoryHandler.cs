using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetVentilatorSettingsHistoryHandler : IRequestHandler<GetVentilatorSettingsHistoryRequestModel, GetVentilatorSettingsHistoryResponseModel>
    {
        private readonly AppDbContext _context;

        public GetVentilatorSettingsHistoryHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetVentilatorSettingsHistoryResponseModel> Handle(GetVentilatorSettingsHistoryRequestModel request, CancellationToken cancellationToken)
        {
            var rows = await _context.VentilatorSettings.AsNoTracking()
                .Where(v => v.HospitalId == request.HospitalId && v.AdmissionId == request.AdmissionId)
                .OrderByDescending(v => v.ScoredAt)
                .ToListAsync(cancellationToken);

            return new GetVentilatorSettingsHistoryResponseModel
            {
                Settings = rows.Select(v => new VentilatorSettingsDataModel
                {
                    VentilatorSettingsId = v.VentilatorSettingsId,
                    Mode = v.Mode,
                    FiO2Percent = v.FiO2Percent,
                    PeepCmH2o = v.PeepCmH2o,
                    TidalVolumeMl = v.TidalVolumeMl,
                    RespiratoryRateSet = v.RespiratoryRateSet,
                    PeakInspiratoryPressure = v.PeakInspiratoryPressure,
                    PlateauPressure = v.PlateauPressure,
                    ScoredBy = v.ScoredBy,
                    ScoredAt = v.ScoredAt,
                    Notes = v.Notes,
                }).ToList(),
            };
        }
    }
}
