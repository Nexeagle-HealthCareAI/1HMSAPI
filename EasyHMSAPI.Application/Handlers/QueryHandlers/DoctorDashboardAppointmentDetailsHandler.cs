using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.Data.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class DoctorDashboardAppointmentDetailsHandler : IRequestHandler<DoctorDashboardAppointmentDetailsRequestModel, DoctorDashboardAppointmentDetailsResponseModel>
    {
        private readonly AppDbContext _context;
        public DoctorDashboardAppointmentDetailsHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<DoctorDashboardAppointmentDetailsResponseModel> Handle(DoctorDashboardAppointmentDetailsRequestModel request, CancellationToken cancellationToken)
        {
            var response = new DoctorDashboardAppointmentDetailsResponseModel();
            var query = _context.Appointments.AsQueryable();
            query = query.Where(a => a.HospitalId == request.HospitalId && a.DoctorId == request.DoctorId);
            // Add doctor status check
            var doctorActive = await (from d in _context.Doctors
                 join u in _context.Users on d.UserID equals u.UserID
                 where d.DoctorID == request.DoctorId && u.UserStatusId != (int)UserStatusEnum.Revoked
                 select d.DoctorID).AnyAsync(cancellationToken);
            if (!doctorActive)
            {
                return response;
            }
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
            var appts = await query.OrderBy(a => a.ApptDate).ToListAsync(cancellationToken);
            var patientIds = appts.Select(a => a.PatientId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToList();
            var patients = await _context.PatientRegistrations
                .Where(p => p.PatientId != null && patientIds.Contains(p.PatientId!))
                .ToDictionaryAsync(p => p.PatientId!, p => p, cancellationToken);
            var apptIds = appts.Select(a => a.ApptId).ToList();
            var tokens = await _context.AppointmentTokens
                .Where(t => apptIds.Contains(t.ApptId))
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync(cancellationToken);
            foreach (var a in appts)
            {
                PatientRegistration? p = null;
                if (!string.IsNullOrEmpty(a.PatientId))
                {
                    patients.TryGetValue(a.PatientId, out p);
                }
                var token = tokens.FirstOrDefault(t => t.ApptId == a.ApptId);
                response.Items.Add(new DoctorDashboardAppointmentDetail
                {
                    PatientId = a.PatientId,
                    PatientFullName = p?.FullName,
                    PatientMobile = p?.Mobile,
                    PatientSex = p?.Sex,
                    PatientAgeYears = p?.AgeYears,
                    AppointmentId = a.ApptId,
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
                    TokenDetails = token == null ? null : new TokenDetailsDataModel
                    {
                        TokenId = token.TokenId,
                        TokenNumber = token.TokenNo,
                        CreatedAt = token.CreatedAt
                    }
                });
            }
            return response;
        }
    }
}
