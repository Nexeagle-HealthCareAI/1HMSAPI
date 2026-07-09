using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class UpsertPackageTypeHandler : IRequestHandler<UpsertPackageTypeRequestModel, UpsertPackageTypeResponseModel>
    {
        private readonly AppDbContext _context;

        public UpsertPackageTypeHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<UpsertPackageTypeResponseModel> Handle(UpsertPackageTypeRequestModel request, CancellationToken cancellationToken)
        {
            UpsertPackageTypeResponseModel response = new() { Success = false };
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    response.Message = "Name is required.";
                    return response;
                }

                // Drop blank entries typed into the free-text components field, so a stray comma
                // doesn't persist as an empty label.
                var components = (request.Components ?? new List<string>())
                    .Select(c => c?.Trim())
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .ToList();
                var componentsJson = components.Count > 0 ? JsonSerializer.Serialize(components) : null;

                if (request.PackageTypeId.HasValue && request.PackageTypeId != Guid.Empty)
                {
                    var existing = await _context.PackageTypes
                        .FirstOrDefaultAsync(x => x.PackageTypeId == request.PackageTypeId && x.HospitalId == request.HospitalId, cancellationToken);
                    if (existing == null)
                    {
                        response.Message = $"Package Type with ID {request.PackageTypeId} not found.";
                        return response;
                    }

                    existing.Name = request.Name.Trim();
                    existing.Price = request.Price;
                    existing.ComponentsJson = componentsJson;
                    existing.IsActive = request.IsActive;
                    existing.UpdatedAt = DateTime.UtcNow;
                    existing.UpdatedBy = request.LoggedInUserName;

                    await _context.SaveChangesAsync(cancellationToken);

                    response.Success = true;
                    response.Message = "Package Type updated successfully.";
                    response.PackageTypeId = existing.PackageTypeId;
                    return response;
                }

                var packageType = new PackageType
                {
                    PackageTypeId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    Name = request.Name.Trim(),
                    Price = request.Price,
                    ComponentsJson = componentsJson,
                    IsActive = request.IsActive,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = DateTime.UtcNow,
                    UpdatedBy = request.LoggedInUserName,
                };
                _context.PackageTypes.Add(packageType);
                await _context.SaveChangesAsync(cancellationToken);

                response.Success = true;
                response.Message = "Package Type created successfully.";
                response.PackageTypeId = packageType.PackageTypeId;
            }
            catch (Exception ex)
            {
                response.Message = "An error occurred: " + ex.Message + ex.InnerException + ex.StackTrace;
            }

            return response;
        }
    }
}
