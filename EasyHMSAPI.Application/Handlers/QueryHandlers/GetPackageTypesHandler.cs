using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetPackageTypesHandler : IRequestHandler<GetPackageTypesRequestModel, GetPackageTypesResponseModel>
    {
        private readonly AppDbContext _context;

        public GetPackageTypesHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetPackageTypesResponseModel> Handle(GetPackageTypesRequestModel request, CancellationToken cancellationToken)
        {
            GetPackageTypesResponseModel response = new() { Success = false };
            try
            {
                var query = _context.PackageTypes
                    .Where(p => p.HospitalId == request.HospitalId);

                if (!request.IncludeInactive)
                    query = query.Where(p => p.IsActive);

                var packageTypes = await query
                    .OrderBy(p => p.Name)
                    .ToListAsync(cancellationToken);

                response.PackageTypes = packageTypes.Select(p => new PackageTypeDataModel
                {
                    PackageTypeId = p.PackageTypeId,
                    Name = p.Name,
                    Price = p.Price,
                    Components = string.IsNullOrWhiteSpace(p.ComponentsJson)
                        ? new List<string>()
                        : JsonSerializer.Deserialize<List<string>>(p.ComponentsJson) ?? new List<string>(),
                    IsActive = p.IsActive,
                    UpdatedAt = p.UpdatedAt,
                    UpdatedBy = p.UpdatedBy,
                }).ToList();
                response.Success = true;
            }
            catch (Exception ex)
            {
                response.Message = "An error occurred: " + ex.Message + ex.InnerException + ex.StackTrace;
            }

            return response;
        }
    }
}
