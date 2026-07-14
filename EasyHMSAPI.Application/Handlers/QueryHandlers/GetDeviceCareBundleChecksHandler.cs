using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetDeviceCareBundleChecksHandler : IRequestHandler<GetDeviceCareBundleChecksRequestModel, GetDeviceCareBundleChecksResponseModel>
    {
        private readonly AppDbContext _context;

        public GetDeviceCareBundleChecksHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetDeviceCareBundleChecksResponseModel> Handle(GetDeviceCareBundleChecksRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.DeviceAssignmentId == Guid.Empty)
                    return new GetDeviceCareBundleChecksResponseModel { Success = false, Message = "HospitalId and DeviceAssignmentId are required." };

                var device = await _context.DeviceAssignment
                    .FirstOrDefaultAsync(d => d.DeviceAssignmentId == request.DeviceAssignmentId && d.HospitalId == request.HospitalId, cancellationToken);
                if (device == null)
                    return new GetDeviceCareBundleChecksResponseModel { Success = false, Message = "Device assignment not found." };

                var canonicalItems = IpdConstants.CareBundleItems.All.TryGetValue(device.DeviceType, out var items)
                    ? items.Select(i => new CareBundleItemDefItem { Key = i.Key, Label = i.Label }).ToList()
                    : new List<CareBundleItemDefItem>();

                var rawChecks = await _context.DeviceCareBundleCheck
                    .Where(c => c.DeviceAssignmentId == request.DeviceAssignmentId)
                    .OrderByDescending(c => c.CheckedAt)
                    .ToListAsync(cancellationToken);

                var checks = rawChecks.Select(c => new DeviceCareBundleCheckItem
                {
                    CheckId = c.CheckId,
                    Items = JsonSerializer.Deserialize<List<CareBundleItemResultItem>>(c.ItemsJson) ?? new List<CareBundleItemResultItem>(),
                    CompliantCount = c.CompliantCount,
                    TotalItems = c.TotalItems,
                    AllCompliant = c.AllCompliant,
                    Notes = c.Notes,
                    CheckedBy = c.CheckedBy,
                    CheckedAt = c.CheckedAt,
                }).ToList();

                return new GetDeviceCareBundleChecksResponseModel { Success = true, CanonicalItems = canonicalItems, Checks = checks };
            }
            catch (Exception)
            {
                return new GetDeviceCareBundleChecksResponseModel { Success = false, Message = "Error loading bundle checks." };
            }
        }
    }
}
