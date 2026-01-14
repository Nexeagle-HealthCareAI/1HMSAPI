using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetPatientAppointmentDetailsHandler : IRequestHandler<GetPatientAppointmentDetailsRequestModel, GetPatientAppointmentDetailsResponseModel>
    {
        private readonly AppDbContext _context;
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

        public GetPatientAppointmentDetailsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetPatientAppointmentDetailsResponseModel> Handle(GetPatientAppointmentDetailsRequestModel request, CancellationToken cancellationToken)
        {
            var response = new GetPatientAppointmentDetailsResponseModel();

            var query = _context.Appointments.AsQueryable();

            query = query.Where(a => a.HospitalId == request.HospitalId);

            if (!string.IsNullOrWhiteSpace(request.Status) && !string.Equals(request.Status, "All", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(a => a.CurrentStatusCode == request.Status);
            }

            if (request.StartDate.HasValue)
            {
                query = query.Where(a => a.ApptDate >= request.StartDate.Value.Date);
            }
            if (request.EndDate.HasValue)
            {
                query = query.Where(a => a.ApptDate <= request.EndDate.Value.Date);
            }

            if (request.DoctorId.HasValue && request.DoctorId != Guid.Empty)
            {
                query = query.Where(a => a.DoctorId == request.DoctorId.Value);
            }

            var appts = await query
                .OrderBy(a => a.ApptDate)
                .ToListAsync(cancellationToken);

            var patientIds = appts.Select(a => a.PatientId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
            var patients = await _context.PatientRegistrations
                .Where(p => p.PatientId != null && patientIds.Contains(p.PatientId!))
                .ToDictionaryAsync(p => p.PatientId!, p => p, cancellationToken);

            var apptIds = appts.Select(a => a.ApptId).ToList();
            var tokens = await _context.AppointmentTokens
                .Where(t => apptIds.Contains(t.ApptId))
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync(cancellationToken);

            var doctorNames = await (from a in _context.Appointments
                                    join d in _context.Doctors on a.DoctorId equals d.DoctorID
                                    join u in _context.UserProfiles on d.UserID equals u.UserID
                                    join dp in _context.Departments on d.PrimaryDepartmentID equals dp.DepartmentID into deptJoin
                                    from dept in deptJoin.DefaultIfEmpty()
                                    where appts.Select(x => x.ApptId).Contains(a.ApptId)
                                    select new { a.ApptId, DoctorName = u.FullName, DepartmentId = d.PrimaryDepartmentID, DepartmentName = dept.Name }).ToListAsync(cancellationToken);

            foreach (var a in appts)
            {
                PatientRegistration? p = null;
                if (!string.IsNullOrEmpty(a.PatientId))
                {
                    patients.TryGetValue(a.PatientId, out p);
                }
                var token = tokens.FirstOrDefault(t => t.ApptId == a.ApptId);
                var doctorInfo = doctorNames.FirstOrDefault(x => x.ApptId == a.ApptId);
                string? doctorName = doctorInfo?.DoctorName;
                Guid? departmentId = doctorInfo?.DepartmentId;
                string? departmentName = doctorInfo?.DepartmentName;

                response.Items.Add(new AppointmentDetail
                {
                    AppointmentId = a.ApptId,
                    PatientId = a.PatientId,
                    PatientFullName = p?.FullName,
                    PatientMobile = p?.Mobile,
                    PatientSex = p?.Sex,
                    PatientAgeYears = p?.AgeYears,
                    DoctorId = a.DoctorId,
                    DoctorName = doctorName,
                    DepartmentId = departmentId ?? Guid.Empty,
                    DepartmentName = departmentName,
                    AppointmentDate = a.ApptDate,
                    StartAt = a.StartAt,
                    EndAt = a.EndAt,
                    FinalStatusCode = a.CurrentStatusCode,
                    Reason = a.Reason,
                    InsuranceId = a.InsuranceId,
                    PaymentMode = a.PaymentMode,
                    LastStatusAt = a.LastStatusCodeAt,
                    CreatedAt = a.CreatedAt,
                    AppointmentType = a.AppointmentType,
                    Token = token == null ? null : new TokenDetail
                    {
                        TokenId = token.TokenId,
                        TokenNumber = token.TokenNo,
                        CreatedAt = token.CreatedAt
                    },
                    StatusJsonHistory = string.IsNullOrWhiteSpace(a.StatusHistoryJson) ? null :
                        JsonSerializer.Deserialize<List<StatusHistoryModel>>(a.StatusHistoryJson, JsonOptions)

                });
            }

            return response;
        }
    }
}
