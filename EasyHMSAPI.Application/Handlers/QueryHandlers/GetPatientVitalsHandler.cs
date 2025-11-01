using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetPatientVitalsHandler : IRequestHandler<GetPatientVitalsRequestModel, PatientVitalsResponseModel>
    {
        private readonly AppDbContext _context;
        public GetPatientVitalsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PatientVitalsResponseModel> Handle(GetPatientVitalsRequestModel request, CancellationToken cancellationToken)
        {
            var vitals = await _context.AppointmentVitals
                .AsNoTracking()
                .FirstOrDefaultAsync(v => v.ApptId == request.AppointmentId && v.PatientId == request.PatientId, cancellationToken);

            if (vitals == null || string.IsNullOrWhiteSpace(vitals.VitalsJson))
                return new PatientVitalsResponseModel { Vitals = null };

            try
            {
                var vitalsObj = JsonSerializer.Deserialize<object>(vitals.VitalsJson);
                return new PatientVitalsResponseModel { Vitals = vitalsObj };
            }
            catch
            {
                return new PatientVitalsResponseModel { Vitals = null };
            }
        }
    }
}
