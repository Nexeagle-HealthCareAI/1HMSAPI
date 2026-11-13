using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    internal static class HrBiometricDeviceMapper
    {
        /// <summary>A device that has spoken to us in the last 10 minutes counts as online (ZKTeco push
        /// devices poll roughly every 30-60 seconds, an on-site bridge every few minutes).</summary>
        private static readonly TimeSpan OnlineWindow = TimeSpan.FromMinutes(10);

        public static HrBiometricDeviceDto ToDto(HrBiometricDevice d) => new()
        {
            HrBiometricDeviceId = d.HrBiometricDeviceId,
            Name = d.Name,
            SerialNumber = d.SerialNumber,
            Vendor = d.Vendor,
            Model = d.Model,
            Location = d.Location,
            IsActive = d.IsActive,
            LastSeenAt = d.LastSeenAt,
            LastSeenIp = d.LastSeenIp,
            LastPunchTime = d.LastPunchTime,
            IsOnline = d.LastSeenAt.HasValue && d.LastSeenAt.Value >= DateTime.UtcNow - OnlineWindow,
        };
    }

    public class GetHrBiometricDevicesHandler : IRequestHandler<GetHrBiometricDevicesRequestModel, GetHrBiometricDevicesResponseModel>
    {
        private readonly AppDbContext _context;

        public GetHrBiometricDevicesHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetHrBiometricDevicesResponseModel> Handle(GetHrBiometricDevicesRequestModel request, CancellationToken cancellationToken)
        {
            var devices = await _context.HrBiometricDevice
                .AsNoTracking()
                .Where(d => d.HospitalId == request.HospitalId)
                .OrderBy(d => d.Name)
                .ToListAsync(cancellationToken);

            return new GetHrBiometricDevicesResponseModel
            {
                Success = true,
                Devices = devices.Select(HrBiometricDeviceMapper.ToDto).ToList()
            };
        }
    }

    public class GetUnmappedDeviceUsersHandler : IRequestHandler<GetUnmappedDeviceUsersRequestModel, GetUnmappedDeviceUsersResponseModel>
    {
        private const int MaxRows = 200;
        private readonly AppDbContext _context;

        public GetUnmappedDeviceUsersHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetUnmappedDeviceUsersResponseModel> Handle(GetUnmappedDeviceUsersRequestModel request, CancellationToken cancellationToken)
        {
            var users = await _context.HrBiometricPunch
                .AsNoTracking()
                .Where(p => p.HospitalId == request.HospitalId && p.HrEmployeeId == null)
                .GroupBy(p => p.DeviceUserId)
                .Select(g => new UnmappedDeviceUserDto
                {
                    DeviceUserId = g.Key,
                    ScanCount = g.Count(),
                    FirstSeen = g.Min(p => p.PunchTime),
                    LastSeen = g.Max(p => p.PunchTime),
                })
                .OrderByDescending(u => u.LastSeen)
                .Take(MaxRows)
                .ToListAsync(cancellationToken);

            return new GetUnmappedDeviceUsersResponseModel { Success = true, Users = users };
        }
    }

    public class GetEmployeeDeviceUsersHandler : IRequestHandler<GetEmployeeDeviceUsersRequestModel, GetEmployeeDeviceUsersResponseModel>
    {
        private readonly AppDbContext _context;

        public GetEmployeeDeviceUsersHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetEmployeeDeviceUsersResponseModel> Handle(GetEmployeeDeviceUsersRequestModel request, CancellationToken cancellationToken)
        {
            var links = await (
                from m in _context.HrEmployeeDeviceUser.AsNoTracking()
                join e in _context.HrEmployee.AsNoTracking() on m.HrEmployeeId equals e.HrEmployeeId
                where m.HospitalId == request.HospitalId && e.HospitalId == request.HospitalId
                orderby e.FirstName, e.LastName
                select new EmployeeDeviceUserDto
                {
                    HrEmployeeId = e.HrEmployeeId,
                    EmployeeCode = e.EmployeeCode,
                    EmployeeName = e.FirstName + " " + e.LastName,
                    DeviceUserId = m.DeviceUserId,
                }).ToListAsync(cancellationToken);

            return new GetEmployeeDeviceUsersResponseModel { Success = true, Links = links };
        }
    }
}
