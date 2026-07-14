using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetDeviceAssignmentsHandler : IRequestHandler<GetDeviceAssignmentsRequestModel, GetDeviceAssignmentsResponseModel>
    {
        private readonly AppDbContext _context;

        public GetDeviceAssignmentsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetDeviceAssignmentsResponseModel> Handle(GetDeviceAssignmentsRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || request.AdmissionId == Guid.Empty)
                    return new GetDeviceAssignmentsResponseModel { Success = false, Message = "HospitalId and AdmissionId are required." };

                var now = DateTime.UtcNow;
                var devices = await _context.DeviceAssignment
                    .Where(d => d.HospitalId == request.HospitalId && d.AdmissionId == request.AdmissionId)
                    .OrderBy(d => d.StatusCode == IpdConstants.DeviceStatus.Active ? 0 : 1)
                    .ThenByDescending(d => d.InsertedAt)
                    .Select(d => new DeviceAssignmentItem
                    {
                        DeviceAssignmentId = d.DeviceAssignmentId,
                        DeviceType = d.DeviceType,
                        InsertionSite = d.InsertionSite,
                        Indication = d.Indication,
                        InsertedByDoctorName = d.InsertedByDoctorName,
                        InsertedAt = d.InsertedAt,
                        RemovedAt = d.RemovedAt,
                        RemovedBy = d.RemovedBy,
                        RemovalReason = d.RemovalReason,
                        StatusCode = d.StatusCode,
                        Notes = d.Notes,
                    })
                    .ToListAsync(cancellationToken);

                foreach (var device in devices)
                    device.DaysInSitu = (int)((device.RemovedAt ?? now) - device.InsertedAt).TotalDays;

                return new GetDeviceAssignmentsResponseModel { Success = true, Devices = devices };
            }
            catch (Exception)
            {
                return new GetDeviceAssignmentsResponseModel { Success = false, Message = "Error loading device assignments." };
            }
        }
    }
}
