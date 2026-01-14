using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetHospitalOverallAnalysisHandler : IRequestHandler<GetHospitalOverallAnalysisRequestModel, GetHospitalOverallAnalysisResponseModel>
    {
        private readonly AppDbContext _context;

        public GetHospitalOverallAnalysisHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetHospitalOverallAnalysisResponseModel> Handle(GetHospitalOverallAnalysisRequestModel request, CancellationToken cancellationToken)
        {
            var response = new GetHospitalOverallAnalysisResponseModel
            {
                Success = false,
                Message = "An error occurred while retrieving hospital analysis."
            };

            try
            {
                if (request.HospitalId == Guid.Empty)
                {
                    response.Message = "Hospital ID is required.";
                    return response;
                }

                var hospitalExists = await _context.Hospitals
                    .AsNoTracking()
                    .AnyAsync(h => h.HospitalID == request.HospitalId, cancellationToken);
                
                if (!hospitalExists)
                {
                    response.Message = "Hospital not found.";
                    return response;
                }

                var now = DateTime.UtcNow;
                var today = now.Date;

                // Fetch appointments and patients sequentially to avoid DbContext concurrency issues
                var appointments = await _context.Appointments
                    .AsNoTracking()
                    .Where(a => a.HospitalId == request.HospitalId)
                    .ToListAsync(cancellationToken);

                var patients = await _context.PatientRegistrations
                    .AsNoTracking()
                    .Where(p => p.HospitalId == request.HospitalId)
                    .ToListAsync(cancellationToken);

                if (appointments.Count == 0)
                {
                    response.Success = true;
                    response.Message = "OPD analytics retrieved successfully.";
                    response.Data = new HospitalAnalysisDataModel();
                    return response;
                }

                var data = new HospitalAnalysisDataModel();
                var yesterday = today.AddDays(-1);
                var last7Days = today.AddDays(-7);
                var monthStart = new DateTime(now.Year, now.Month, 1);
                var yearStart = new DateTime(now.Year, 1, 1);
                var prevYearStart = new DateTime(now.Year - 1, 1, 1);
                var prevYearEnd = new DateTime(now.Year - 1, 12, 31);

                // Calculate KPIs
                data.Kpis = CalculateKpis(appointments, patients, today, yesterday, last7Days, monthStart, yearStart, prevYearStart, prevYearEnd);

                // Calculate Breakdowns
                data.Breakdowns = await CalculateBreakdownsOptimized(appointments, request.HospitalId, cancellationToken);

                // Calculate Overall Analysis
                data.Overall = CalculateOverallAnalysis(appointments, patients);

                // Calculate Gender-wise Analysis
                data.GenderWise = CalculateGenderWiseAnalysis(appointments, patients);

                response.Data = data;
                response.Success = true;
                response.Message = "OPD analytics retrieved successfully.";
            }
            catch (Exception ex)
            {
                response.Message = "An error occurred:" + ex.Message + ex.InnerException + ex.StackTrace;
            }

            return response;
        }

        private KpisModel CalculateKpis(List<Domain.Entities.Appointment> appointments, List<Domain.Entities.PatientRegistration> patients,
            DateTime today, DateTime yesterday, DateTime last7Days, DateTime monthStart, DateTime yearStart, DateTime prevYearStart, DateTime prevYearEnd)
        {
            var kpis = new KpisModel();
            var currentYear = DateTime.UtcNow.Year;
            var prevYear = currentYear - 1;

            // Total Visits - Single pass with HashSet for unique patients
            var totalVisits = appointments.Count;
            var visitsToday = 0;
            var visitsYesterday = 0;
            var visitsLast7Days = 0;
            var visitsThisMonth = 0;
            var visitsThisYear = 0;
            var visitsPrevYear = 0;

            var uniquePatientIdsSet = new HashSet<string?>();
            var uniquePatientsToday = new HashSet<string?>();
            var uniquePatientsYesterday = new HashSet<string?>();
            var uniquePatientsLast7Days = new HashSet<string?>();
            var uniquePatientsThisMonth = new HashSet<string?>();
            var uniquePatientsThisYear = new HashSet<string?>();
            var uniquePatientsPrevYear = new HashSet<string?>();
            var patientFirstApptDate = new Dictionary<string, DateTime>();

            // Single pass through appointments
            foreach (var appt in appointments)
            {
                var apptDate = appt.ApptDate.Date;
                var apptYear = appt.ApptDate.Year;

                // Total visits
                if (apptDate == today) visitsToday++;
                if (apptDate == yesterday) visitsYesterday++;
                if (apptDate >= last7Days && apptDate <= today) visitsLast7Days++;
                if (apptDate >= monthStart && apptDate <= today) visitsThisMonth++;
                if (apptYear == currentYear) visitsThisYear++;
                if (apptYear == prevYear) visitsPrevYear++;

                // Unique patients
                if (!string.IsNullOrEmpty(appt.PatientId))
                {
                    uniquePatientIdsSet.Add(appt.PatientId);
                    if (apptDate == today) uniquePatientsToday.Add(appt.PatientId);
                    if (apptDate == yesterday) uniquePatientsYesterday.Add(appt.PatientId);
                    if (apptDate >= last7Days && apptDate <= today) uniquePatientsLast7Days.Add(appt.PatientId);
                    if (apptDate >= monthStart && apptDate <= today) uniquePatientsThisMonth.Add(appt.PatientId);
                    if (apptYear == currentYear) uniquePatientsThisYear.Add(appt.PatientId);
                    if (apptYear == prevYear) uniquePatientsPrevYear.Add(appt.PatientId);

                    // Track first appointment date for each patient
                    if (!patientFirstApptDate.ContainsKey(appt.PatientId))
                    {
                        patientFirstApptDate[appt.PatientId] = appt.ApptDate;
                    }
                    else if (appt.ApptDate < patientFirstApptDate[appt.PatientId])
                    {
                        patientFirstApptDate[appt.PatientId] = appt.ApptDate;
                    }
                }
            }

            var uniquePatientIds = uniquePatientIdsSet.Count;

            kpis.TotalVisits = new VisitMetricModel
            {
                Overall = totalVisits,
                ByBucket = new BucketMetricModel
                {
                    Today = visitsToday,
                    Yesterday = visitsYesterday,
                    Last7Days = visitsLast7Days,
                    ThisMonth = visitsThisMonth,
                    ThisYear = visitsThisYear,
                    PrevYear = visitsPrevYear
                }
            };

            kpis.UniquePatients = new VisitMetricModel
            {
                Overall = uniquePatientIds,
                ByBucket = new BucketMetricModel
                {
                    Today = uniquePatientsToday.Count,
                    Yesterday = uniquePatientsYesterday.Count,
                    Last7Days = uniquePatientsLast7Days.Count,
                    ThisMonth = uniquePatientsThisMonth.Count,
                    ThisYear = uniquePatientsThisYear.Count,
                    PrevYear = uniquePatientsPrevYear.Count
                }
            };

            // New vs Returning Patients
            var newPatients = patientFirstApptDate.Count(p => p.Value.Date >= monthStart);
            var returningPatients = uniquePatientIds - newPatients;
            var newPercent = uniquePatientIds > 0 ? Math.Round((decimal)newPatients / uniquePatientIds * 100, 2) : 0;
            var returningPercent = uniquePatientIds > 0 ? Math.Round((decimal)returningPatients / uniquePatientIds * 100, 2) : 0;

            kpis.NewVsReturningPatients = new PatientTypeModel
            {
                New = new PatientCountModel { Count = newPatients, Percent = newPercent },
                Returning = new PatientCountModel { Count = returningPatients, Percent = returningPercent }
            };

            return kpis;
        }

        private async Task<BreakdownsModel> CalculateBreakdownsOptimized(List<Domain.Entities.Appointment> appointments, Guid hospitalId, CancellationToken cancellationToken)
        {
            var breakdowns = new BreakdownsModel();

            // Filter appointments to only get doctors present in appointments
            var doctorIds = appointments.Select(a => a.DoctorId).Distinct().ToList();

            // Fetch doctors only for those present in appointments
            var doctorsDict = await _context.Doctors
                .AsNoTracking()
                .Where(d => doctorIds.Contains(d.DoctorID))
                .Include(d => d.User)
                .Include(d => d.DoctorSpecializations)
                    .ThenInclude(ds => ds.Specialization)
                .ToDictionaryAsync(d => d.DoctorID, cancellationToken);

            var doctorSpecDict = await _context.DoctorSpecializations
                .AsNoTracking()
                .ToDictionaryAsync(ds => ds.DoctorID, ds => ds.SpecializationID, cancellationToken);

            var now = DateTime.UtcNow;
            var currentYear = now.Year;
            var last30Days = now.AddDays(-30).Date;
            var last7Days = now.AddDays(-7).Date;

            // Doctor Breakdown - Group and aggregate in one pass
            var doctorGroups = appointments.GroupBy(a => a.DoctorId);
            var doctorBreakdowns = new List<DoctorBreakdownModel>();

            foreach (var group in doctorGroups)
            {
                var doctorId = group.Key;
                if (!doctorsDict.TryGetValue(doctorId, out var doctor))
                    continue;

                var doctorAppts = group.ToList();
                var specialty = doctor?.DoctorSpecializations?.FirstOrDefault()?.Specialization?.Name ?? "General";
                var uniquePatients = doctorAppts.Where(a => !string.IsNullOrEmpty(a.PatientId)).Select(a => a.PatientId).Distinct().Count();
                var newPatientCount = doctorAppts.Where(a => a.ApptDate.Date >= last30Days && !string.IsNullOrEmpty(a.PatientId)).Select(a => a.PatientId).Distinct().Count();
                var noShow = doctorAppts.Count(a => a.CurrentStatusCode == "NO_SHOW");
                var sharePercent = appointments.Count > 0 ? Math.Round((decimal)doctorAppts.Count / appointments.Count * 100, 2) : 0;

                doctorBreakdowns.Add(new DoctorBreakdownModel
                {
                    DoctorId = doctorId,
                    DoctorName = doctor?.User?.UserProfiles?.FirstOrDefault()?.FullName ?? "Unknown",
                    Specialty = specialty,
                    OverallVisits = doctorAppts.Count,
                    UniquePatients = uniquePatients,
                    NewPatients = new NewPatientMetricModel
                    {
                        Day = doctorAppts.Count(a => a.ApptDate.Date == now.Date),
                        Week = doctorAppts.Count(a => a.ApptDate.Date >= last7Days && a.ApptDate.Date <= now.Date),
                        Month = doctorAppts.Count(a => a.ApptDate.Month == now.Month && a.ApptDate.Year == currentYear),
                        Year = doctorAppts.Count(a => a.ApptDate.Year == currentYear)
                    },
                    ReturningPatients = uniquePatients - newPatientCount,
                    FirstVisits = newPatientCount,
                    NoShow = noShow,
                    SharePercent = sharePercent
                });
            }

            breakdowns.ByDoctor = doctorBreakdowns.OrderByDescending(d => d.OverallVisits).ToList();

            // Specialty Breakdown - Single pass grouping
            var specialtyBreakdowns = new Dictionary<Guid, SpecialtyBreakdownModel>();

            var appointmentsBySpecialty = appointments
                .Where(a => doctorSpecDict.ContainsKey(a.DoctorId))
                .GroupBy(a => doctorSpecDict[a.DoctorId])
                .Where(g => g.Key != Guid.Empty);

            // Fetch all specializations at once to avoid async calls in loop
            var specialtyIds = appointmentsBySpecialty.Select(g => g.Key).Distinct().ToList();
            var specialtiesDict = await _context.Specializations
                .AsNoTracking()
                .Where(s => specialtyIds.Contains(s.SpecializationID))
                .ToDictionaryAsync(s => s.SpecializationID, cancellationToken);

            foreach (var specGroup in appointmentsBySpecialty)
            {
                var specId = specGroup.Key;
                var specAppts = specGroup.ToList();
                var uniquePatients = specAppts.Where(a => !string.IsNullOrEmpty(a.PatientId)).Select(a => a.PatientId).Distinct().Count();
                var sharePercent = appointments.Count > 0 ? Math.Round((decimal)specAppts.Count / appointments.Count * 100, 2) : 0;

                var specialty = specialtiesDict.TryGetValue(specId, out var spec) ? spec : null;
                var specialtyCode = specialty?.SpecializationID.ToString().Substring(0, Math.Min(4, specialty.SpecializationID.ToString().Length)).ToUpper() ?? "N/A";

                specialtyBreakdowns[specId] = new SpecialtyBreakdownModel
                {
                    SpecialtyCode = specialtyCode,
                    SpecialtyName = specialty?.Name ?? "Unknown",
                    OverallVisits = specAppts.Count,
                    UniquePatients = uniquePatients,
                    SharePercent = sharePercent,
                    TrendVsPreviousPeriod = new TrendModel { Percent = 0, Direction = "STABLE" }
                };
            }

            breakdowns.BySpecialty = specialtyBreakdowns.Values.OrderByDescending(s => s.OverallVisits).ToList();

            return breakdowns;
        }

        private OverallModel CalculateOverallAnalysis(List<Domain.Entities.Appointment> appointments, List<Domain.Entities.PatientRegistration> patients)
        {
            var overall = new OverallModel();

            // Initialize age distribution
            var ageDistribution = new Dictionary<string, int>
            {
                { "0-10", 0 },
                { "11-20", 0 },
                { "21-30", 0 },
                { "31-40", 0 },
                { "41-50", 0 },
                { "51-60", 0 },
                { "61-70", 0 },
                { "71-100", 0 }
            };

            // Single pass for age distribution
            foreach (var patient in patients)
            {
                if (patient.AgeYears.HasValue)
                {
                    var age = patient.AgeYears.Value;
                    var ageKey = age switch
                    {
                        >= 0 and < 11 => "0-10",
                        >= 11 and < 21 => "11-20",
                        >= 21 and < 31 => "21-30",
                        >= 31 and < 41 => "31-40",
                        >= 41 and < 51 => "41-50",
                        >= 51 and < 61 => "51-60",
                        >= 61 and < 71 => "61-70",
                        >= 71 => "71-100",
                        _ => null
                    };

                    if (ageKey != null)
                    {
                        ageDistribution[ageKey]++;
                    }
                }
            }

            overall.AgeDistribution = ageDistribution;

            // No-shows and Cancellations - Single pass
            var noShowCount = 0;
            var cancelledCount = 0;
            foreach (var appt in appointments)
            {
                if (appt.CurrentStatusCode == "NO_SHOW") noShowCount++;
                if (appt.CurrentStatusCode == "CANCELLED") cancelledCount++;
            }

            overall.NoShow = noShowCount;
            overall.Cancelled = cancelledCount;

            // Top 5 Cities
            overall.Top5City = patients
                .Where(p => !string.IsNullOrEmpty(p.City))
                .GroupBy(p => p.City)
                .OrderByDescending(g => g.Count())
                .Take(5)
                .ToDictionary(g => g.Key ?? string.Empty, g => g.Count());

            // Unique Cities
            overall.UniqueCities = patients
                .Where(p => !string.IsNullOrEmpty(p.City))
                .Select(p => p.City)
                .Where(c => c != null)
                .Distinct()
                .ToList();

            return overall;
        }

        private List<GenderWiseModel> CalculateGenderWiseAnalysis(List<Domain.Entities.Appointment> appointments, List<Domain.Entities.PatientRegistration> patients)
        {
            var genderWise = new List<GenderWiseModel>();

            // Group patients by gender
            var genderGroups = patients
                .Where(p => !string.IsNullOrEmpty(p.Sex))
                .GroupBy(p => p.Sex)
                .ToList();

            foreach (var genderGroup in genderGroups)
            {
                var gender = genderGroup.Key;
                var patientIds = new HashSet<string?>(genderGroup.Select(p => p.PatientId));

                // Filter appointments for this gender
                var genderAppts = appointments.Where(a => patientIds.Contains(a.PatientId)).ToList();

                // Age distribution
                var ageDistribution = new Dictionary<string, int>
                {
                    { "0-10", 0 },
                    { "11-20", 0 },
                    { "21-30", 0 },
                    { "31-40", 0 },
                    { "41-50", 0 },
                    { "51-60", 0 },
                    { "61-70", 0 },
                    { "71-100", 0 }
                };

                foreach (var patient in genderGroup)
                {
                    if (patient.AgeYears.HasValue)
                    {
                        var age = patient.AgeYears.Value;
                        var ageKey = age switch
                        {
                            >= 0 and < 11 => "0-10",
                            >= 11 and < 21 => "11-20",
                            >= 21 and < 31 => "21-30",
                            >= 31 and < 41 => "31-40",
                            >= 41 and < 51 => "41-50",
                            >= 51 and < 61 => "51-60",
                            >= 61 and < 71 => "61-70",
                            >= 71 => "71-100",
                            _ => null
                        };

                        if (ageKey != null)
                        {
                            ageDistribution[ageKey]++;
                        }
                    }
                }

                genderWise.Add(new GenderWiseModel
                {
                    Gender = gender,
                    OverallVisits = genderAppts.Count,
                    NoShow = genderAppts.Count(a => a.CurrentStatusCode == "NO_SHOW"),
                    Cancelled = genderAppts.Count(a => a.CurrentStatusCode == "CANCELLED"),
                    AgeDistribution = ageDistribution
                });
            }

            return genderWise;
        }
    }
}
