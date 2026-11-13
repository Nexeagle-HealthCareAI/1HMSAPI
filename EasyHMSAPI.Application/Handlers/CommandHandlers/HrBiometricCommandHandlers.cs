using EasyHMSAPI.Application.Common;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    /// <summary>
    /// A device (or on-site bridge) delivering scans. Authenticated by registered serial number + token.
    /// A wrong token, an unknown serial and a deactivated device all get the SAME answer, so the endpoint
    /// cannot be used to discover which serial numbers exist.
    /// </summary>
    public class IngestBiometricPunchesHandler : IRequestHandler<IngestBiometricPunchesRequestModel, IngestBiometricPunchesResponseModel>
    {
        public const int MaxBatchSize = 2000;

        // Compared against when the serial is unknown, so an unknown serial costs the same as a wrong token.
        private static readonly string DummyHash = new('0', 64);

        private readonly AppDbContext _context;
        private readonly IBiometricPunchIngestionService _ingestion;

        public IngestBiometricPunchesHandler(AppDbContext context, IBiometricPunchIngestionService ingestion)
        {
            _context = context;
            _ingestion = ingestion;
        }

        public async Task<IngestBiometricPunchesResponseModel> Handle(IngestBiometricPunchesRequestModel request, CancellationToken cancellationToken)
        {
            var serial = BiometricDeviceCredentials.NormalizeSerial(request.DeviceSerial);
            var device = serial.Length == 0
                ? null
                : await _context.HrBiometricDevice.FirstOrDefaultAsync(d => d.SerialNumber == serial, cancellationToken);

            var tokenOk = BiometricDeviceCredentials.Matches(request.DeviceToken, device?.TokenHash ?? DummyHash);
            if (device == null || !device.IsActive || !tokenOk)
            {
                return new IngestBiometricPunchesResponseModel { Success = false, Unauthorized = true, Message = "Invalid device credentials." };
            }

            if (request.Punches.Count > MaxBatchSize)
            {
                return new IngestBiometricPunchesResponseModel { Success = false, Message = $"Send at most {MaxBatchSize} scans per request." };
            }

            var incoming = request.Punches
                .Select(p => new IncomingPunch(
                    p.UserId ?? string.Empty,
                    p.Time,
                    p.State,
                    p.Verify,
                    $"{p.UserId}|{p.Time:yyyy-MM-dd HH:mm:ss}|{p.State}|{p.Verify}"))
                .ToList();

            var result = await _ingestion.IngestAsync(device, incoming, request.RemoteIp, cancellationToken);

            return new IngestBiometricPunchesResponseModel
            {
                Success = true,
                Message = "OK",
                Received = result.Received,
                Accepted = result.Accepted,
                Duplicates = result.Duplicates,
                Invalid = result.Invalid,
                Unmapped = result.Unmapped,
                AttendanceDaysUpdated = result.AttendanceDaysUpdated,
            };
        }
    }

    public class RegisterHrBiometricDeviceHandler : IRequestHandler<RegisterHrBiometricDeviceRequestModel, RegisterHrBiometricDeviceResponseModel>
    {
        private const string ManagePermission = "hr.manage_employees";

        // ZKTeco serials are letters and digits; allow the separators other vendors use too.
        private static readonly Regex SerialShape = new("^[A-Z0-9][A-Z0-9._-]{2,63}$", RegexOptions.Compiled);

        private readonly AppDbContext _context;

        public RegisterHrBiometricDeviceHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<RegisterHrBiometricDeviceResponseModel> Handle(RegisterHrBiometricDeviceRequestModel request, CancellationToken cancellationToken)
        {
            var name = request.Name?.Trim();
            var serial = BiometricDeviceCredentials.NormalizeSerial(request.SerialNumber);

            if (request.HospitalId == Guid.Empty || string.IsNullOrEmpty(name) || name.Length > 100)
                return Fail("Enter a name for the device (up to 100 characters).");
            if (!SerialShape.IsMatch(serial))
                return Fail("Enter the device's serial number exactly as shown on the device (letters and digits, 3 to 64 characters).");

            if (!await CallerGuards.HasPermissionAtHospitalAsync(_context, request.LoggedInUserId, request.HospitalId, ManagePermission, cancellationToken))
                return Fail("You don't have access to manage devices at this hospital.");

            // A physical device belongs to exactly one hospital, so the serial is unique across the platform.
            // The message deliberately doesn't say WHICH hospital holds it.
            if (await _context.HrBiometricDevice.AnyAsync(d => d.SerialNumber == serial, cancellationToken))
                return Fail("That serial number is already registered.");

            var (token, hash) = BiometricDeviceCredentials.Generate();
            var device = new HrBiometricDevice
            {
                HospitalId = request.HospitalId,
                Name = name,
                SerialNumber = serial,
                Model = string.IsNullOrWhiteSpace(request.Model) ? null : request.Model.Trim(),
                Location = string.IsNullOrWhiteSpace(request.Location) ? null : request.Location.Trim(),
                TokenHash = hash,
                CreatedBy = request.LoggedInUserName,
            };
            _context.HrBiometricDevice.Add(device);
            await _context.SaveChangesAsync(cancellationToken);

            return new RegisterHrBiometricDeviceResponseModel
            {
                Success = true,
                Message = "Device registered.",
                Device = HrBiometricDeviceMapper.ToDto(device),
                Token = token,
            };
        }

        private static RegisterHrBiometricDeviceResponseModel Fail(string message) => new() { Success = false, Message = message };
    }

    public class SetHrBiometricDeviceActiveHandler : IRequestHandler<SetHrBiometricDeviceActiveRequestModel, HrBiometricActionResponseModel>
    {
        private readonly AppDbContext _context;

        public SetHrBiometricDeviceActiveHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<HrBiometricActionResponseModel> Handle(SetHrBiometricDeviceActiveRequestModel request, CancellationToken cancellationToken)
        {
            var device = await _context.HrBiometricDevice.FirstOrDefaultAsync(d => d.HrBiometricDeviceId == request.HrBiometricDeviceId, cancellationToken);

            // Looked up by ID alone, so authorize against the device's own hospital; another hospital's
            // device answers "not found" exactly like a missing one.
            if (device == null || !await CallerGuards.HasPermissionAtHospitalAsync(_context, request.LoggedInUserId, device.HospitalId, "hr.manage_employees", cancellationToken))
                return new HrBiometricActionResponseModel { Success = false, Message = "Device not found." };

            device.IsActive = request.IsActive;
            await _context.SaveChangesAsync(cancellationToken);

            return new HrBiometricActionResponseModel { Success = true, Message = request.IsActive ? "Device activated." : "Device deactivated. It will no longer be accepted." };
        }
    }

    public class RotateHrBiometricDeviceTokenHandler : IRequestHandler<RotateHrBiometricDeviceTokenRequestModel, RotateHrBiometricDeviceTokenResponseModel>
    {
        private readonly AppDbContext _context;

        public RotateHrBiometricDeviceTokenHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<RotateHrBiometricDeviceTokenResponseModel> Handle(RotateHrBiometricDeviceTokenRequestModel request, CancellationToken cancellationToken)
        {
            var device = await _context.HrBiometricDevice.FirstOrDefaultAsync(d => d.HrBiometricDeviceId == request.HrBiometricDeviceId, cancellationToken);

            if (device == null || !await CallerGuards.HasPermissionAtHospitalAsync(_context, request.LoggedInUserId, device.HospitalId, "hr.manage_employees", cancellationToken))
                return new RotateHrBiometricDeviceTokenResponseModel { Success = false, Message = "Device not found." };

            var (token, hash) = BiometricDeviceCredentials.Generate();
            device.TokenHash = hash;
            await _context.SaveChangesAsync(cancellationToken);

            return new RotateHrBiometricDeviceTokenResponseModel
            {
                Success = true,
                Message = "New token issued. The previous token no longer works.",
                Token = token,
            };
        }
    }

    public class MapEmployeeDeviceUserHandler : IRequestHandler<MapEmployeeDeviceUserRequestModel, MapEmployeeDeviceUserResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IBiometricPunchIngestionService _ingestion;

        public MapEmployeeDeviceUserHandler(AppDbContext context, IBiometricPunchIngestionService ingestion)
        {
            _context = context;
            _ingestion = ingestion;
        }

        public async Task<MapEmployeeDeviceUserResponseModel> Handle(MapEmployeeDeviceUserRequestModel request, CancellationToken cancellationToken)
        {
            var pin = request.DeviceUserId?.Trim();
            if (string.IsNullOrEmpty(pin) || pin.Length > 50)
                return Fail("Enter the employee's ID exactly as registered on the device (up to 50 characters).");

            var employee = await _context.HrEmployee.FirstOrDefaultAsync(e => e.HrEmployeeId == request.HrEmployeeId, cancellationToken);
            if (employee == null || !await CallerGuards.HasPermissionAtHospitalAsync(_context, request.LoggedInUserId, employee.HospitalId, "hr.manage_employees", cancellationToken))
                return Fail("Employee not found.");

            // One PIN, one person, per hospital.
            var holder = await _context.HrEmployeeDeviceUser
                .FirstOrDefaultAsync(m => m.HospitalId == employee.HospitalId && m.DeviceUserId == pin, cancellationToken);
            if (holder != null && holder.HrEmployeeId != employee.HrEmployeeId)
            {
                var otherName = await _context.HrEmployee
                    .Where(e => e.HrEmployeeId == holder.HrEmployeeId)
                    .Select(e => e.FirstName + " " + e.LastName)
                    .FirstOrDefaultAsync(cancellationToken);
                return Fail($"Device ID {pin} is already assigned to {otherName ?? "another employee"}.");
            }

            var mine = await _context.HrEmployeeDeviceUser.FirstOrDefaultAsync(m => m.HrEmployeeId == employee.HrEmployeeId, cancellationToken);
            if (mine == null)
            {
                _context.HrEmployeeDeviceUser.Add(new HrEmployeeDeviceUser
                {
                    HospitalId = employee.HospitalId,
                    HrEmployeeId = employee.HrEmployeeId,
                    DeviceUserId = pin,
                    CreatedBy = request.LoggedInUserName,
                });
            }
            else
            {
                mine.DeviceUserId = pin;
            }

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Someone else took this PIN between our check and the save (the unique index caught it).
                return Fail($"Device ID {pin} was just assigned to someone else. Refresh and try again.");
            }

            // Scans that arrived while this PIN was unmapped can now be turned into attendance.
            var days = await _ingestion.LinkPinAndRebuildAsync(employee.HospitalId, pin, employee.HrEmployeeId, cancellationToken);

            return new MapEmployeeDeviceUserResponseModel
            {
                Success = true,
                Message = days > 0 ? $"Linked. Attendance was built for {days} day(s) from earlier scans." : "Linked.",
                AttendanceDaysUpdated = days,
            };
        }

        private static MapEmployeeDeviceUserResponseModel Fail(string message) => new() { Success = false, Message = message };
    }

    public class UnmapEmployeeDeviceUserHandler : IRequestHandler<UnmapEmployeeDeviceUserRequestModel, HrBiometricActionResponseModel>
    {
        private readonly AppDbContext _context;

        public UnmapEmployeeDeviceUserHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<HrBiometricActionResponseModel> Handle(UnmapEmployeeDeviceUserRequestModel request, CancellationToken cancellationToken)
        {
            var employee = await _context.HrEmployee.FirstOrDefaultAsync(e => e.HrEmployeeId == request.HrEmployeeId, cancellationToken);
            if (employee == null || !await CallerGuards.HasPermissionAtHospitalAsync(_context, request.LoggedInUserId, employee.HospitalId, "hr.manage_employees", cancellationToken))
                return new HrBiometricActionResponseModel { Success = false, Message = "Employee not found." };

            var link = await _context.HrEmployeeDeviceUser.FirstOrDefaultAsync(m => m.HrEmployeeId == employee.HrEmployeeId, cancellationToken);
            if (link == null)
                return new HrBiometricActionResponseModel { Success = true, Message = "Nothing to unlink." };

            // Scans already linked to this employee stay with them (that's history); only NEW scans from this
            // PIN go back to "unmapped".
            _context.HrEmployeeDeviceUser.Remove(link);
            await _context.SaveChangesAsync(cancellationToken);

            return new HrBiometricActionResponseModel { Success = true, Message = "Unlinked." };
        }
    }
}
