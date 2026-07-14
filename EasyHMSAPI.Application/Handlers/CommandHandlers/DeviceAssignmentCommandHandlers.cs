using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// Invasive device tracking (central line/catheter/ETT) for CLABSI/CAUTI/VAP risk.
    /// ACTIVE/REMOVED lifecycle mirrors RestraintOrderCommandHandlers, but unlike restraints
    /// a patient can hold multiple concurrent device types -- only one ACTIVE row per
    /// (admission, device type) at a time (backstopped by UX_DA_AdmissionDeviceTypeActive,
    /// same DbUpdateException-catch pattern as BedAssignmentCommandHandlers/RestraintOrderCommandHandlers).
    /// </summary>
    public class DeviceAssignmentCommandHandlers :
        IRequestHandler<InsertDeviceRequestModel, InsertDeviceResponseModel>,
        IRequestHandler<RemoveDeviceRequestModel, RemoveDeviceResponseModel>
    {
        private readonly AppDbContext _context;

        public DeviceAssignmentCommandHandlers(AppDbContext context)
        {
            _context = context;
        }

        public async Task<InsertDeviceResponseModel> Handle(InsertDeviceRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new InsertDeviceResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };
                if (string.IsNullOrWhiteSpace(request.DeviceType) || !IpdConstants.IcuDeviceType.All.Contains(request.DeviceType))
                    return new InsertDeviceResponseModel { Success = false, Message = "A valid device type is required." };
                if (string.IsNullOrWhiteSpace(request.InsertedByDoctorName))
                    return new InsertDeviceResponseModel { Success = false, Message = "Inserted-by doctor name is required." };

                var admission = await _context.Admission
                    .FirstOrDefaultAsync(a => a.AdmissionId == request.AdmissionId && a.HospitalId == request.HospitalId, cancellationToken);
                if (admission == null)
                    return new InsertDeviceResponseModel { Success = false, Message = "Admission not found." };
                if (!IpdConstants.AdmissionStatus.Active.Contains(admission.StatusCode))
                    return new InsertDeviceResponseModel { Success = false, Message = "Admission is not active." };

                var alreadyActive = await _context.DeviceAssignment
                    .AnyAsync(d => d.AdmissionId == admission.AdmissionId && d.HospitalId == request.HospitalId
                        && d.DeviceType == request.DeviceType && d.StatusCode == IpdConstants.DeviceStatus.Active, cancellationToken);
                if (alreadyActive)
                    return new InsertDeviceResponseModel { Success = false, Message = "This admission already has an active device of this type -- remove it before inserting a new one." };

                var now = DateTime.UtcNow;
                var device = new DeviceAssignment
                {
                    DeviceAssignmentId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    AdmissionId = admission.AdmissionId,
                    EncounterId = admission.EncounterId,
                    PatientId = admission.PatientId,
                    DeviceType = request.DeviceType,
                    InsertionSite = string.IsNullOrWhiteSpace(request.InsertionSite) ? null : request.InsertionSite.Trim(),
                    Indication = string.IsNullOrWhiteSpace(request.Indication) ? null : request.Indication.Trim(),
                    InsertedByDoctorName = request.InsertedByDoctorName.Trim(),
                    InsertedAt = now,
                    StatusCode = IpdConstants.DeviceStatus.Active,
                    Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                    CreatedAt = now,
                    CreatedBy = request.LoggedInUserName,
                    UpdatedAt = now,
                    UpdatedBy = request.LoggedInUserName,
                };
                _context.DeviceAssignment.Add(device);

                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException)
                {
                    // Concurrency backstop: two requests racing past the AnyAsync check above hit
                    // UX_DA_AdmissionDeviceTypeActive -- same pattern as RestraintOrderCommandHandlers.
                    return new InsertDeviceResponseModel { Success = false, Message = "This admission already has an active device of this type." };
                }

                return new InsertDeviceResponseModel { Success = true, Message = "Device inserted.", DeviceAssignmentId = device.DeviceAssignmentId };
            }
            catch (Exception)
            {
                return new InsertDeviceResponseModel { Success = false, Message = "Error inserting device." };
            }
        }

        public async Task<RemoveDeviceResponseModel> Handle(RemoveDeviceRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.DeviceAssignmentId == Guid.Empty)
                    return new RemoveDeviceResponseModel { Success = false, Message = "HospitalId and DeviceAssignmentId are required." };

                var device = await _context.DeviceAssignment
                    .FirstOrDefaultAsync(d => d.DeviceAssignmentId == request.DeviceAssignmentId && d.HospitalId == request.HospitalId, cancellationToken);
                if (device == null)
                    return new RemoveDeviceResponseModel { Success = false, Message = "Device assignment not found." };
                if (device.StatusCode != IpdConstants.DeviceStatus.Active)
                    return new RemoveDeviceResponseModel { Success = false, Message = "This device is already removed." };

                var now = DateTime.UtcNow;
                device.StatusCode = IpdConstants.DeviceStatus.Removed;
                device.RemovedAt = now;
                device.RemovedBy = request.LoggedInUserName;
                device.RemovedByUserId = request.LoggedInUserId;
                device.RemovalReason = string.IsNullOrWhiteSpace(request.RemovalReason) ? null : request.RemovalReason.Trim();
                device.UpdatedAt = now;
                device.UpdatedBy = request.LoggedInUserName;

                await _context.SaveChangesAsync(cancellationToken);

                return new RemoveDeviceResponseModel { Success = true, Message = "Device removed.", DeviceAssignmentId = device.DeviceAssignmentId };
            }
            catch (Exception)
            {
                return new RemoveDeviceResponseModel { Success = false, Message = "Error removing device." };
            }
        }
    }
}
