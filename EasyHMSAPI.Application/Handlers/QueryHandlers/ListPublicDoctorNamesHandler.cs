using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.Extensions.Caching.Memory;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Names only, for Vita's name index -- served from the same cached PublicDoctorPool the
    /// search uses, so it costs nothing extra and lists exactly the doctors a search can return.
    /// </summary>
    public class ListPublicDoctorNamesHandler : IRequestHandler<ListPublicDoctorNamesRequestModel, ListPublicDoctorNamesResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;

        public ListPublicDoctorNamesHandler(AppDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        public async Task<ListPublicDoctorNamesResponseModel> Handle(ListPublicDoctorNamesRequestModel request, CancellationToken cancellationToken)
        {
            var pool = await PublicDoctorPool.LoadAsync(_context, _cache, null, cancellationToken);
            var city = request.City?.Trim();

            var doctors = pool
                .Where(e => string.IsNullOrEmpty(city) || string.Equals(e.HospitalCity?.Trim(), city, StringComparison.OrdinalIgnoreCase))
                .OrderBy(e => e.FullName, StringComparer.OrdinalIgnoreCase)
                .Select(e => new PublicDoctorName { DoctorId = e.DoctorId, FullName = e.FullName, City = e.HospitalCity })
                .ToList();

            return new ListPublicDoctorNamesResponseModel { Success = true, Doctors = doctors };
        }
    }
}
