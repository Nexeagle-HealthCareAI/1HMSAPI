using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetGlucoseReadingsHandler : IRequestHandler<GetGlucoseReadingsRequestModel, GetGlucoseReadingsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetGlucoseReadingsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetGlucoseReadingsResponseModel> Handle(GetGlucoseReadingsRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new GetGlucoseReadingsResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var toUtc = request.ToUtc ?? DateTime.UtcNow;
                var fromUtc = request.FromUtc ?? toUtc.AddDays(-7);

                var readings = await _context.GlucoseReading
                    .Where(g => g.HospitalId == request.HospitalId && g.AdmissionId == request.AdmissionId
                        && g.RecordedAt >= fromUtc && g.RecordedAt <= toUtc)
                    .OrderByDescending(g => g.RecordedAt)
                    .Select(g => new GlucoseReadingItem
                    {
                        GlucoseReadingId = g.GlucoseReadingId,
                        Value = g.Value,
                        Unit = g.Unit,
                        ValueMgDl = g.ValueMgDl,
                        Method = g.Method,
                        MealTag = g.MealTag,
                        InsulinGiven = g.InsulinGiven,
                        InsulinUnits = g.InsulinUnits,
                        InsulinType = g.InsulinType,
                        InsulinRoute = g.InsulinRoute,
                        IsHypo = g.IsHypo,
                        IsHyper = g.IsHyper,
                        RecordedAt = g.RecordedAt,
                        RecordedBy = g.RecordedBy,
                        Notes = g.Notes,
                    })
                    .ToListAsync(cancellationToken);

                return new GetGlucoseReadingsResponseModel { Success = true, Readings = readings };
            }
            catch (Exception)
            {
                return new GetGlucoseReadingsResponseModel { Success = false, Message = "Error loading glucose readings." };
            }
        }
    }
}
