using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Hospital-wide, date-ranged "infections per 1000 device-days" summary (NHSN standard
    /// metric) for each canonical device type / infection type pairing. Device-days sum
    /// DeviceDaysCalculator's overlap-days over every DeviceAssignment of that type against
    /// the query range; infection counts come from InfectionEvent within the same range.
    /// </summary>
    public class GetInfectionRateSummaryHandler : IRequestHandler<GetInfectionRateSummaryRequestModel, GetInfectionRateSummaryResponseModel>
    {
        private static readonly (string DeviceType, string InfectionType)[] Pairings =
        {
            (IpdConstants.IcuDeviceType.CentralLine, IpdConstants.InfectionType.Clabsi),
            (IpdConstants.IcuDeviceType.UrinaryCatheter, IpdConstants.InfectionType.Cauti),
            (IpdConstants.IcuDeviceType.Ett, IpdConstants.InfectionType.Vap),
        };

        private readonly AppDbContext _context;

        public GetInfectionRateSummaryHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetInfectionRateSummaryResponseModel> Handle(GetInfectionRateSummaryRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty)
                    return new GetInfectionRateSummaryResponseModel { Success = false, Message = "HospitalId is required." };
                if (request.ToDate < request.FromDate)
                    return new GetInfectionRateSummaryResponseModel { Success = false, Message = "ToDate must not be before FromDate." };

                var now = DateTime.UtcNow;

                var devices = await _context.DeviceAssignment
                    .Where(d => d.HospitalId == request.HospitalId && d.InsertedAt <= request.ToDate
                        && (d.RemovedAt == null || d.RemovedAt >= request.FromDate))
                    .Select(d => new { d.DeviceType, d.InsertedAt, d.RemovedAt })
                    .ToListAsync(cancellationToken);

                var infections = await _context.InfectionEvent
                    .Where(e => e.HospitalId == request.HospitalId && e.DiagnosedAt >= request.FromDate && e.DiagnosedAt <= request.ToDate)
                    .Select(e => e.InfectionType)
                    .ToListAsync(cancellationToken);

                var rates = new List<InfectionRateSummaryItem>();
                foreach (var (deviceType, infectionType) in Pairings)
                {
                    var deviceDays = devices
                        .Where(d => d.DeviceType == deviceType)
                        .Sum(d => DeviceDaysCalculator.ComputeOverlapDays(d.InsertedAt, d.RemovedAt ?? now, request.FromDate, request.ToDate));

                    var infectionCount = infections.Count(t => t == infectionType);

                    rates.Add(new InfectionRateSummaryItem
                    {
                        DeviceType = deviceType,
                        InfectionType = infectionType,
                        InfectionCount = infectionCount,
                        DeviceDays = deviceDays,
                        RatePer1000DeviceDays = deviceDays > 0 ? Math.Round(infectionCount / deviceDays * 1000, 2) : null,
                    });
                }

                return new GetInfectionRateSummaryResponseModel { Success = true, Rates = rates };
            }
            catch (Exception)
            {
                return new GetInfectionRateSummaryResponseModel { Success = false, Message = "Error computing infection rate summary." };
            }
        }
    }
}
