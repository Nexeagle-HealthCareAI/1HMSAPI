using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class SearchPatientHandler : IRequestHandler<SearchPatientRequestModel, SearchPatientResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public SearchPatientHandler(AppDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            HttpContextHelper.Configure(_httpContextAccessor);
        }

        public async Task<SearchPatientResponseModel> Handle(SearchPatientRequestModel request, CancellationToken cancellationToken)
        {
            var response = new SearchPatientResponseModel();
            var today = DateTime.UtcNow.Date;

            IQueryable<PatientRegistration> query = _context.PatientRegistrations;
            switch (request.By?.ToLower())
            {
                case "patientid":
                    if (!string.IsNullOrEmpty(request.Q))
                        query = query.Where(p => p.PatientId != null && p.PatientId.Contains(request.Q));
                    break;
                case "name":
                    if (!string.IsNullOrEmpty(request.Q))
                        query = query.Where(p => p.FullName != null && p.FullName.Contains(request.Q));
                    break;
                case "contact":
                case "mobile":
                    if (!string.IsNullOrEmpty(request.Q))
                        query = query.Where(p => p.Mobile != null && p.Mobile.Contains(request.Q));
                    break;
                case "appointmentid":
                    var apptId = Guid.TryParse(request.Q, out var guid) ? guid : Guid.Empty;
                    query = query.Where(p => _context.Appointments.Any(a => a.ApptId == apptId && a.PatientId == p.PatientId));
                    break;
                default:
                    throw new ArgumentException("Invalid search type. Must be one of: patientId, name, contact, appointmentId");
            }

            if (request.Scope?.ToLower() == "local" && HttpContextHelper.HospitalId != null)
            {
                query = query.Where(p => p.HospitalId == HttpContextHelper.HospitalId);
            }

            var patients = await query.ToListAsync(cancellationToken);
            foreach (var p in patients)
            {
                var lastReg = p;
                var upcomingAppt = await _context.Appointments
                    .Where(a => a.PatientId == p.PatientId && a.ApptDate >= today)
                    .OrderBy(a => a.ApptDate)
                    .FirstOrDefaultAsync(cancellationToken);
                string? tokenNo = null;
                DateTime? apptDate = null;
                Guid? appointmentId = null;
                if (upcomingAppt != null)
                {
                    var token = await _context.AppointmentTokens
                        .Where(t => t.ApptId == upcomingAppt.ApptId)
                        .OrderByDescending(t => t.CreatedAt)
                        .FirstOrDefaultAsync(cancellationToken);
                    tokenNo = token?.TokenNo.ToString();
                    apptDate = upcomingAppt.ApptDate;
                    appointmentId = upcomingAppt.ApptId;
                }
                // Calculate age and date of birth
                DateTime? dateOfBirth = null;
                if (p.AgeYears.HasValue && p.AgeYears.Value > 0)
                {
                    dateOfBirth = DateTime.UtcNow.AddYears(-p.AgeYears.Value);
                }

                response.Items.Add(new PatientSearchResult
                {
                    PatientId = p.PatientId ?? string.Empty,
                    FullName = p.FullName,
                    Mobile = p.Mobile,
                    Sex = p.Sex,
                    Age = p.AgeYears,
                    DateOfBirth = dateOfBirth ?? null,
                    Address = p.AddressLine,
                    City = p.City,
                    Pincode = p.Pincode,
                    LastRegistrationAt = p.RegisteredAt,
                    LastRegistrationId = p.RegistrationId,
                    Matched = new MatchInfo { By = request.By, Value = request.Q },
                    AppointmentDate = apptDate,
                    AppointmentId = appointmentId,
                    TokenNumber = tokenNo
                });
            }
            return response;
        }
    }

    // Helper class to access HTTP context (you might already have this in your project)
    public static class HttpContextHelper
    {
        private static IHttpContextAccessor? _httpContextAccessor;

        public static void Configure(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public static Guid? HospitalId
        {
            get
            {
                var hospitalId = _httpContextAccessor?.HttpContext?.User?.FindFirst("hospitalId")?.Value;
                if (Guid.TryParse(hospitalId, out var id))
                    return id;
                return null;
            }
        }
    }
}
