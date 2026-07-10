using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class SearchPatientHandler : IRequestHandler<SearchPatientRequestModel, SearchPatientResponseModel>
    {
        private const int MaxResults = 20;

        private readonly AppDbContext _context;

        public SearchPatientHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<SearchPatientResponseModel> Handle(SearchPatientRequestModel request, CancellationToken cancellationToken)
        {
            var response = new SearchPatientResponseModel();
            var today = DateTime.UtcNow.Date;

            // Build optimized query with all search criteria
            IQueryable<PatientRegistration> query = _context.PatientRegistrations
                .Where(p => p.HospitalId == request.HospitalId && p.MergedIntoPatientId == null);

            var searchTerm = request.SearchText?.ToLower() ?? string.Empty;

            // Apply search text filter across multiple columns if provided
            if (!string.IsNullOrEmpty(request.SearchText))
            {
                query = query.Where(p =>
                    (p.FullName != null && (p.FullName.ToLower().Contains(searchTerm) || AppDbContext.Soundex(p.FullName) == AppDbContext.Soundex(searchTerm))) ||
                    (p.PatientId != null && p.PatientId.ToLower().Contains(searchTerm)) ||
                    (p.Mobile != null && p.Mobile.Contains(searchTerm)) ||
                    (p.AlternateMobile != null && p.AlternateMobile.Contains(searchTerm)) ||
                    (p.AadhaarNumber != null && p.AadhaarNumber.Contains(searchTerm)) ||
                    (p.AbhaId != null && p.AbhaId.ToLower().Contains(searchTerm)) ||
                    _context.Appointments.Any(a =>
                        a.PatientId == p.PatientId &&
                        a.HospitalId == request.HospitalId &&
                        a.ApptId.ToString().Contains(searchTerm))
                );
            }

            // Fetch patients with minimal database hits
            var patients = await query.ToListAsync(cancellationToken);

            if (patients.Count == 0)
                return response;

            // Rank by relevance (exact > starts-with > contains > id match > fuzzy) and cap the
            // result set — the SQL filter above is a broad candidate net, not a ranked result.
            if (!string.IsNullOrEmpty(request.SearchText))
            {
                var normalizedSearch = FuzzyMatch.Normalize(request.SearchText);
                patients = patients
                    .OrderByDescending(p => ScoreCandidate(p, searchTerm, normalizedSearch))
                    .ToList();
            }
            patients = patients.Take(MaxResults).ToList();

            // Get all patient IDs for batch operations (exclude nulls)
            var patientIds = patients.Where(p => p.PatientId != null).Select(p => p.PatientId!).ToList();

            // Batch fetch upcoming appointments for all patients
            var upcomingAppointments = await _context.Appointments
                .Where(a => a.HospitalId == request.HospitalId &&
                           a.ApptDate >= today &&
                           a.PatientId != null &&
                           patientIds.Contains(a.PatientId))
                .OrderBy(a => a.ApptDate)
                .GroupBy(a => a.PatientId)
                .Select(g => g.First())
                .ToListAsync(cancellationToken);

            var appointmentIds = upcomingAppointments.Select(a => a.ApptId).ToList();

            // Batch fetch appointment tokens
            var appointmentTokens = await _context.AppointmentTokens
                .Where(t => appointmentIds.Contains(t.ApptId))
                .OrderByDescending(t => t.CreatedAt)
                .GroupBy(t => t.ApptId)
                .Select(g => g.First())
                .ToListAsync(cancellationToken);

            // Create lookup dictionaries for efficient access
            var appointmentLookup = upcomingAppointments
                .Where(a => a.PatientId != null)
                .ToDictionary(a => a.PatientId!);
            var tokenLookup = appointmentTokens.ToDictionary(t => t.ApptId);

            // Build response
            foreach (var p in patients)
            {
                string? tokenNo = null;
                DateTime? apptDate = null;
                Guid? appointmentId = null;

                if (p.PatientId != null && appointmentLookup.TryGetValue(p.PatientId, out var upcomingAppt))
                {
                    if (tokenLookup.TryGetValue(upcomingAppt.ApptId, out var token))
                    {
                        tokenNo = token.TokenNo.ToString();
                    }
                    apptDate = upcomingAppt.ApptDate;
                    appointmentId = upcomingAppt.ApptId;
                }

                // Calculate date of birth from age and unit
                DateTime? dateOfBirth = null;
                if (p.Age.HasValue && p.Age.Value > 0)
                {
                    if (p.AgeUnit == "M") dateOfBirth = DateTime.UtcNow.AddMonths(-p.Age.Value);
                    else if (p.AgeUnit == "D") dateOfBirth = DateTime.UtcNow.AddDays(-p.Age.Value);
                    else dateOfBirth = DateTime.UtcNow.AddYears(-p.Age.Value);
                }

                response.Items.Add(new PatientSearchResult
                {
                    PatientId = p.PatientId ?? string.Empty,
                    FullName = p.FullName,
                    Mobile = p.Mobile,
                    Sex = p.Sex,
                    Age = p.Age,
                    AgeUnit = p.AgeUnit,
                    DateOfBirth = dateOfBirth,
                    Address = p.AddressLine,
                    City = p.City,
                    Pincode = p.Pincode,
                    LastRegistrationAt = p.RegisteredAt,
                    LastRegistrationId = p.RegistrationId,
                    AppointmentDate = apptDate,
                    AppointmentId = appointmentId,
                    TokenNumber = tokenNo
                });
            }
            return response;
        }

        // Tiered relevance score for a candidate against the search term: exact name match highest,
        // then starts-with, then contains, then a UHID/mobile/aadhaar/abha match, else a Jaro-Winkler
        // fuzzy score on the name (catches spelling variants the SQL Contains/Soundex filter still let through).
        private static double ScoreCandidate(PatientRegistration p, string searchTermLower, string normalizedSearch)
        {
            var normalizedName = FuzzyMatch.Normalize(p.FullName);
            if (normalizedSearch.Length > 0 && normalizedName == normalizedSearch) return 4.0;
            if (normalizedSearch.Length > 0 && normalizedName.StartsWith(normalizedSearch)) return 3.0;
            if (normalizedSearch.Length > 0 && normalizedName.Contains(normalizedSearch)) return 2.0;
            if (IdMatches(p, searchTermLower)) return 1.0;
            return FuzzyMatch.JaroWinkler(p.FullName, searchTermLower);
        }

        private static bool IdMatches(PatientRegistration p, string searchTermLower) =>
            (!string.IsNullOrEmpty(p.PatientId) && p.PatientId.ToLower().Contains(searchTermLower)) ||
            (!string.IsNullOrEmpty(p.Mobile) && p.Mobile.Contains(searchTermLower)) ||
            (!string.IsNullOrEmpty(p.AlternateMobile) && p.AlternateMobile.Contains(searchTermLower)) ||
            (!string.IsNullOrEmpty(p.AadhaarNumber) && p.AadhaarNumber.Contains(searchTermLower)) ||
            (!string.IsNullOrEmpty(p.AbhaId) && p.AbhaId.ToLower().Contains(searchTermLower));
    }
}
