using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class HospitalKpiMatrixHandler : IRequestHandler<HospitalKpiMatrixRequestModel, HospitalKpiMatrixResponseModel>
    {
        private readonly AppDbContext _context;
        public HospitalKpiMatrixHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<HospitalKpiMatrixResponseModel> Handle(HospitalKpiMatrixRequestModel request, CancellationToken cancellationToken)
        {
            var response = new HospitalKpiMatrixResponseModel
            {
                HospitalId = request.HospitalId,
                DoctorId = request.DoctorId,
                StartDate = request.StartDate,
                EndDate = request.EndDate
            };

            var statusList = await _context.StatusMasters.ToListAsync(cancellationToken);
            var appointments = _context.Appointments.AsQueryable();
            appointments = appointments.Where(a => a.HospitalId == request.HospitalId);
            if (request.DoctorId.HasValue && request.DoctorId.Value != Guid.Empty)
            {
                appointments = appointments.Where(a => a.DoctorId == request.DoctorId.Value);
            }
            if (request.StartDate.HasValue)
            {
                appointments = appointments.Where(a => a.ApptDate >= request.StartDate.Value.Date);
            }
            if (request.EndDate.HasValue)
            {
                appointments = appointments.Where(a => a.ApptDate <= request.EndDate.Value.Date);
            }

            foreach (var status in statusList)
            {
                var count = await appointments.CountAsync(a => a.CurrentStatusCode == status.StatusCode, cancellationToken);
                response.StatusKpis.Add(new StatusKpi
                {
                    StatusCode = status.StatusCode,
                    DisplayName = status.DisplayName,
                    PatientCount = count
                });
            }
            return response;
        }
    }
}
