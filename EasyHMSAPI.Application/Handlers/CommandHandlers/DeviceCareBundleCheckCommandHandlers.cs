using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// Logs a single CLABSI/CAUTI/VAP care-bundle compliance check against an active device.
    /// Insert-only (real bundles are checked every shift, not once a day). Item keys are
    /// validated against IpdConstants.CareBundleItems for the device's type; compliance
    /// counts are computed here, never trusted from the client.
    /// </summary>
    public class DeviceCareBundleCheckCommandHandlers : IRequestHandler<SubmitDeviceCareBundleCheckRequestModel, SubmitDeviceCareBundleCheckResponseModel>
    {
        private readonly AppDbContext _context;

        public DeviceCareBundleCheckCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<SubmitDeviceCareBundleCheckResponseModel> Handle(SubmitDeviceCareBundleCheckRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.DeviceAssignmentId == Guid.Empty)
                    return new SubmitDeviceCareBundleCheckResponseModel { Success = false, Message = "HospitalId and DeviceAssignmentId are required." };

                var device = await _context.DeviceAssignment
                    .FirstOrDefaultAsync(d => d.DeviceAssignmentId == request.DeviceAssignmentId && d.HospitalId == request.HospitalId, cancellationToken);
                if (device == null)
                    return new SubmitDeviceCareBundleCheckResponseModel { Success = false, Message = "Device assignment not found." };
                if (device.StatusCode != IpdConstants.DeviceStatus.Active)
                    return new SubmitDeviceCareBundleCheckResponseModel { Success = false, Message = "Cannot log a bundle check against a removed device." };

                if (!IpdConstants.CareBundleItems.All.TryGetValue(device.DeviceType, out var canonicalItems))
                    return new SubmitDeviceCareBundleCheckResponseModel { Success = false, Message = "No care-bundle checklist is defined for this device type." };

                var canonicalKeys = canonicalItems.Select(i => i.Key).ToHashSet();
                var submittedKeys = request.Items.Select(i => i.Key).ToHashSet();
                if (!canonicalKeys.SetEquals(submittedKeys))
                    return new SubmitDeviceCareBundleCheckResponseModel { Success = false, Message = "Submitted checklist items do not match the expected list for this device type." };

                var compliantCount = request.Items.Count(i => i.Compliant);
                var totalItems = canonicalItems.Length;

                var now = DateTime.UtcNow;
                var check = new DeviceCareBundleCheck
                {
                    CheckId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    AdmissionId = device.AdmissionId,
                    DeviceAssignmentId = device.DeviceAssignmentId,
                    DeviceType = device.DeviceType,
                    ItemsJson = JsonSerializer.Serialize(request.Items),
                    CompliantCount = compliantCount,
                    TotalItems = totalItems,
                    AllCompliant = compliantCount == totalItems,
                    Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                    CheckedBy = request.LoggedInUserName ?? "Unknown",
                    CheckedByUserId = request.LoggedInUserId,
                    CheckedAt = now,
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                };
                _context.DeviceCareBundleCheck.Add(check);
                await _context.SaveChangesAsync(cancellationToken);

                return new SubmitDeviceCareBundleCheckResponseModel
                {
                    Success = true,
                    Message = "Bundle check recorded.",
                    CheckId = check.CheckId,
                    CompliantCount = check.CompliantCount,
                    TotalItems = check.TotalItems,
                    AllCompliant = check.AllCompliant,
                };
            }
            catch (Exception)
            {
                return new SubmitDeviceCareBundleCheckResponseModel { Success = false, Message = "Error recording bundle check." };
            }
        }
    }
}
