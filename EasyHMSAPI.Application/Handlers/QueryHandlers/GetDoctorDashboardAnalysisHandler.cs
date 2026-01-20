using EasyHMSAPI.Application.Helpers.Interfaces;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetDoctorDashboardAnalysisHandler : IRequestHandler<GetDoctorDashboardAnalysisRequestModel, GetDoctorDashboardAnalysisResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly IDoctorValidationHelper _doctorValidationHelper;

        public GetDoctorDashboardAnalysisHandler(AppDbContext context, IDoctorValidationHelper doctorValidationHelper)
        {
            _context = context;
            _doctorValidationHelper = doctorValidationHelper;
        }

        public async Task<GetDoctorDashboardAnalysisResponseModel> Handle(GetDoctorDashboardAnalysisRequestModel request, CancellationToken cancellationToken)
        {
            GetDoctorDashboardAnalysisResponseModel response = new();
            try
            {
                var existingDoctor = await _context.Doctors
                    .AsNoTracking()
                    .FirstOrDefaultAsync(d => d.DoctorID == request.DoctorId, cancellationToken);
                if (existingDoctor == null)
                {
                    response.Success = false;
                    response.Message = "Invalid doctor Id";
                    return response;
                }

                var existingHospital = await _context.Hospitals
                    .AsNoTracking()
                    .FirstOrDefaultAsync(h => h.HospitalID == request.HospitalId, cancellationToken);
                if (existingHospital == null)
                {
                    response.Success = false;
                    response.Message = "Invalid hospital Id";
                    return response;
                }

                if (!await _doctorValidationHelper.ValidateDoctorAsync(request.HospitalId, request.DoctorId, cancellationToken))
                {
                    response.Success = false;
                    response.Message = "Doctor is not associated with the specified hospital.";
                    return response;
                }

                var data = new DashboardAnalysisData
                {
                    KPI = await GetKPIAnalysis(request.DoctorId, request.HospitalId, cancellationToken),
                    MedicalStats = await GetMedicalStats(request.DoctorId, request.HospitalId, cancellationToken),
                    BPStats = await GetBPStats(request.DoctorId, request.HospitalId, cancellationToken),
                    WeightStats = await GetWeightStats(request.DoctorId, request.HospitalId, cancellationToken),
                    BMIStats = await GetBMIStats(request.DoctorId, request.HospitalId, cancellationToken)
                };

                response.Success = true;
                response.Message = "OPD analytics retrieved successfully.";
                response.Data = data;
            }
            catch (Exception ex)
            {
                response.Success = false;
                response.Message = "An error occurred: " + ex.Message + ex.InnerException + ex.StackTrace;
            }

            return response;
        }

        private static VitalData ParseVitalsJson(string vitalsJson)
        {
            try
            {
                if (string.IsNullOrEmpty(vitalsJson))
                    return new VitalData();

                using (JsonDocument doc = JsonDocument.Parse(vitalsJson))
                {
                    var root = doc.RootElement;
                    
                    int? systolicBP = null;
                    int? diastolicBP = null;
                    
                    if (root.TryGetProperty("Bp", out var bpElement))
                    {
                        if (bpElement.TryGetProperty("Sys", out var sys) && int.TryParse(sys.ToString(), out var sbp))
                            systolicBP = sbp;
                        if (bpElement.TryGetProperty("Dia", out var dia) && int.TryParse(dia.ToString(), out var dbp))
                            diastolicBP = dbp;
                    }

                    decimal? weight = null;
                    if (root.TryGetProperty("WeightKg", out var weightKg) && decimal.TryParse(weightKg.ToString(), out var w))
                        weight = w;

                    decimal? bmi = null;
                    if (root.TryGetProperty("Bmi", out var bmiValue) && decimal.TryParse(bmiValue.ToString(), out var b))
                        bmi = b;

                    return new VitalData
                    {
                        SystolicBP = systolicBP,
                        DiastolicBP = diastolicBP,
                        Weight = weight,
                        BMI = bmi
                    };
                }
            }
            catch
            {
                return new VitalData();
            }
        }

        private async Task<KPIData> GetKPIAnalysis(Guid doctorId, Guid hospitalId, CancellationToken cancellationToken)
        {
            var kpis = new KPIData();
            var now = DateTime.UtcNow;
            var today = now.Date;
            var yesterday = today.AddDays(-1);
            var last7Days = today.AddDays(-7);
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var currentYear = DateTime.UtcNow.Year;
            var prevYear = currentYear - 1;

            var appointments = await _context.Appointments
                .AsNoTracking()
                .Where(a => a.DoctorId == doctorId && a.HospitalId == hospitalId)
                .Select(a => new
                {
                    a.ApptId,
                    a.PatientId,
                    a.ApptDate,
                    a.CurrentStatusCode,
                    a.AppointmentType
                })
                .ToListAsync(cancellationToken);

            var totalVisitsCount = appointments.Count;
            var visitsToday = 0;
            var visitsYesterday = 0;
            var visitsLast7Days = 0;
            var visitsThisMonth = 0;
            var visitsThisYear = 0;
            var visitsPrevYear = 0;
            foreach (var appt in appointments)
            {
                var apptDate = appt.ApptDate.Date;
                var apptYear = appt.ApptDate.Year;
                if (apptDate == today) visitsToday++;
                if (apptDate == yesterday) visitsYesterday++;
                if (apptDate >= last7Days && apptDate <= today) visitsLast7Days++;
                if (apptDate >= monthStart && apptDate <= today) visitsThisMonth++;
                if (apptYear == currentYear) visitsThisYear++;
                if (apptYear == prevYear) visitsPrevYear++;
            }
            kpis.TotalVisits = new VisitData
            {
                Overall = totalVisitsCount,
                ByBucket = new TimeBucketData
                {
                    Today = visitsToday,
                    Yesterday = visitsYesterday,
                    Last7Days = visitsLast7Days,
                    ThisMonth = visitsThisMonth,
                    ThisYear = visitsThisYear,
                    PrevYear = visitsPrevYear
                }
            };

            var allPatients = appointments
                .Where(x => !string.IsNullOrEmpty(x.PatientId))
                .ToList();
            var uniquPatients = allPatients
               .Select(x => x.PatientId)
               .Distinct()
               .ToList();
            var uniquePatientsAppointments = allPatients
                 .GroupBy(x => x.PatientId)
                 .Select(g => new { g.Key, ApptDate = g.First().ApptDate })
                 .ToList()
                 .Select(x => new { PatientId = x.Key, x.ApptDate })
                 .ToList();

            var uniquePatientsToday = new HashSet<string?>();
            var uniquePatientsYesterday = new HashSet<string?>();
            var uniquePatientsLast7Days = new HashSet<string?>();
            var uniquePatientsThisMonth = new HashSet<string?>();
            var uniquePatientsThisYear = new HashSet<string?>();
            var uniquePatientsPrevYear = new HashSet<string?>();
            foreach (var appt in uniquePatientsAppointments)
            {
                var apptDate = appt.ApptDate.Date;
                var apptYear = appt.ApptDate.Year;
                if (!string.IsNullOrEmpty(appt.PatientId) && !string.IsNullOrWhiteSpace(appt.PatientId))
                {
                    if (apptDate == today) uniquePatientsToday.Add(appt.PatientId);
                    if (apptDate == yesterday) uniquePatientsYesterday.Add(appt.PatientId);
                    if (apptDate >= last7Days && apptDate <= today) uniquePatientsLast7Days.Add(appt.PatientId);
                    if (apptDate >= monthStart && apptDate <= today) uniquePatientsThisMonth.Add(appt.PatientId);
                    if (apptYear == currentYear) uniquePatientsThisYear.Add(appt.PatientId);
                    if (apptYear == prevYear) uniquePatientsPrevYear.Add(appt.PatientId);
                }
            }
            kpis.UniquePatients = new VisitData
            {
                Overall = uniquPatients.Count,
                ByBucket = new TimeBucketData
                {
                    Today = uniquePatientsToday.Count,
                    Yesterday = uniquePatientsYesterday.Count,
                    Last7Days = uniquePatientsLast7Days.Count,
                    ThisMonth = uniquePatientsThisMonth.Count,
                    ThisYear = uniquePatientsThisYear.Count,
                    PrevYear = uniquePatientsPrevYear.Count
                }
            };

            var returningPatientsCount = 0;
            var newPatientCount = 0;
            foreach (var item in uniquPatients)
            {
                var apptDetails = appointments
                    .Where(a => a.PatientId == item && a.CurrentStatusCode != AppConstants.AppointmentStatus_Cancelled)
                    .OrderByDescending(a => a.ApptDate)
                    .ToList();
                if (apptDetails.Count > 1)
                {
                    var allApptsAreNew = apptDetails.All(a => a.AppointmentType == AppConstants.AppointmentType_New);

                    if (allApptsAreNew)
                    {
                        newPatientCount++;
                    }
                    else
                    {
                        returningPatientsCount++;
                    }
                }
                else
                {
                    newPatientCount++;
                }
            }
            var totalUniquePatients = newPatientCount + returningPatientsCount;
            var newPatientPercent = totalUniquePatients > 0
                ? Math.Round((decimal)newPatientCount / totalUniquePatients * 100, 2)
                : 0;
            var returningPatientPercent = totalUniquePatients > 0
                ? Math.Round((decimal)returningPatientsCount / totalUniquePatients * 100, 2)
                : 0;

            kpis.NewVsReturningPatients = new PatientTypeData
            {
                New = new PatientCountData { Count = newPatientCount, Percent = newPatientPercent },
                Returning = new PatientCountData { Count = returningPatientsCount, Percent = returningPatientPercent }
            };

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
            var patientAges = await _context.PatientRegistrations
                .AsNoTracking()
                .Where(p => uniquPatients.Contains(p.PatientId))
                .Select(p => p.AgeYears)
                .ToListAsync(cancellationToken);
            foreach (var age in patientAges)
            {
                if (age >= 0 && age <= 10) ageDistribution["0-10"]++;
                else if (age >= 11 && age <= 20) ageDistribution["11-20"]++;
                else if (age >= 21 && age <= 30) ageDistribution["21-30"]++;
                else if (age >= 31 && age <= 40) ageDistribution["31-40"]++;
                else if (age >= 41 && age <= 50) ageDistribution["41-50"]++;
                else if (age >= 51 && age <= 60) ageDistribution["51-60"]++;
                else if (age >= 61 && age <= 70) ageDistribution["61-70"]++;
                else if (age >= 71 && age <= 100) ageDistribution["71-100"]++;
            }
            kpis.AgeDistribution = ageDistribution;

            kpis.Cancelled = appointments.Count(a => a.CurrentStatusCode == AppConstants.AppointmentStatus_Cancelled);
            kpis.NoShow = appointments.Count(x => x.CurrentStatusCode == AppConstants.AppointmentStatus_VitalsRequired && x.ApptDate.Date < DateTime.UtcNow.Date);

            //var totalPatients = uniquePatientIds.Count;
            //var totalNewPercent = totalPatients > 0 ? (newPatients * 100m) / totalPatients : 0;
            //var totalReturningPercent = totalPatients > 0 ? (returningPatients * 100m) / totalPatients : 0;

            return kpis;
        }

        private async Task<MedicalStatsData> GetMedicalStats(Guid doctorId, Guid hospitalId, CancellationToken cancellationToken)
        {
            var appointments = await _context.Appointments
                .AsNoTracking()
                .Where(a => a.DoctorId == doctorId && a.HospitalId == hospitalId)
                .Select(a => a.ApptId)
                .ToListAsync(cancellationToken);

            var medicines = new Dictionary<string, int>();
            var complaints = new Dictionary<string, int>();
            var diagnoses = new Dictionary<string, int>();

            var topMedicines = await _context.DoctorPreferredMedicines
                .AsNoTracking()
                .Where(m => m.DoctorId == doctorId && m.HospitalId == hospitalId && m.IsActive)
                .GroupBy(m => m.MedicineName ?? "Unknown")
                .Select(g => new { g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .ToListAsync(cancellationToken);

            foreach (var med in topMedicines)
            {
                medicines[med.Key] = med.Count;
            }

            var prescriptions = await _context.Prescription
                .AsNoTracking()
                .Where(p => appointments.Contains(p.ApptId))
                .Select(p => new
                {
                    p.Diagnosis,
                    p.ChiefComplaint,
                    p.Examination
                })
                .ToListAsync(cancellationToken);

            foreach (var prescription in prescriptions)
            {
                if (!string.IsNullOrEmpty(prescription.Diagnosis))
                {
                    var key = prescription.Diagnosis;
                    if (diagnoses.ContainsKey(key))
                        diagnoses[key]++;
                    else
                        diagnoses[key] = 1;
                }

                if (!string.IsNullOrEmpty(prescription.ChiefComplaint))
                {
                    var key = prescription.ChiefComplaint;
                    if (complaints.ContainsKey(key))
                        complaints[key]++;
                    else
                        complaints[key] = 1;
                }
            }

            var sortedDiagnoses = diagnoses.OrderByDescending(x => x.Value).Take(5).ToDictionary(x => x.Key, x => x.Value);
            var sortedComplaints = complaints.OrderByDescending(x => x.Value).Take(5).ToDictionary(x => x.Key, x => x.Value);

            return new MedicalStatsData
            {
                Top5MedicineUse = medicines.Any() ? medicines : new Dictionary<string, int>(),
                Top5Complain = sortedComplaints.Any() ? sortedComplaints : new Dictionary<string, int>(),
                Top5Diagnosis = sortedDiagnoses.Any() ? sortedDiagnoses : new Dictionary<string, int>(),
                Top5Investigation = new Dictionary<string, int>(),
                Top5Examination = new Dictionary<string, int>()
            };
        }

        private async Task<BPStatsData> GetBPStats(Guid doctorId, Guid hospitalId, CancellationToken cancellationToken)
        {
            var categoryCounts = new Dictionary<string, int>
            {
                { "NORMAL", 0 },
                { "ELEVATED", 0 },
                { "HTN_STAGE_1", 0 },
                { "HTN_STAGE_2", 0 },
                { "HYPOTENSION", 0 }
            };

            var appointments = await _context.Appointments
                .AsNoTracking()
                .Where(a => a.DoctorId == doctorId && a.HospitalId == hospitalId)
                .Select(a => a.ApptId)
                .ToListAsync(cancellationToken);

            var vitalsRecords = await _context.AppointmentVitals
                .AsNoTracking()
                .Where(v => appointments.Contains(v.ApptId))
                .Select(v => v.VitalsJson)
                .ToListAsync(cancellationToken);

            foreach (var vitalsJson in vitalsRecords)
            {
                var vital = ParseVitalsJson(vitalsJson);
                if (vital.SystolicBP.HasValue && vital.DiastolicBP.HasValue)
                {
                    var systolic = vital.SystolicBP.Value;
                    var diastolic = vital.DiastolicBP.Value;

                    if (systolic < 90 || diastolic < 60)
                        categoryCounts["HYPOTENSION"]++;
                    else if (systolic >= 140 || diastolic >= 90)
                        categoryCounts["HTN_STAGE_2"]++;
                    else if ((systolic >= 130 && systolic <= 139) || (diastolic >= 80 && diastolic <= 89))
                        categoryCounts["HTN_STAGE_1"]++;
                    else if (systolic >= 120 && systolic <= 129 && diastolic < 80)
                        categoryCounts["ELEVATED"]++;
                    else if (systolic < 120 && diastolic < 80)
                        categoryCounts["NORMAL"]++;
                }
            }

            return new BPStatsData
            {
                CategoryCounts = categoryCounts
            };
        }

        private async Task<WeightStatsData> GetWeightStats(Guid doctorId, Guid hospitalId, CancellationToken cancellationToken)
        {
            var appointments = await _context.Appointments
                .AsNoTracking()
                .Where(a => a.DoctorId == doctorId && a.HospitalId == hospitalId)
                .Select(a => a.ApptId)
                .ToListAsync(cancellationToken);

            var vitalsRecords = await _context.AppointmentVitals
                .AsNoTracking()
                .Where(v => appointments.Contains(v.ApptId))
                .Select(v => v.VitalsJson)
                .ToListAsync(cancellationToken);

            var buckets = new Dictionary<string, int>
            {
                { "40-50", 0 },
                { "50-60", 0 },
                { "60-70", 0 },
                { "70-80", 0 },
                { "80-90", 0 },
                { "90+", 0 }
            };

            foreach (var vitalsJson in vitalsRecords)
            {
                var vital = ParseVitalsJson(vitalsJson);
                if (vital.Weight.HasValue)
                {
                    var weight = vital.Weight.Value;
                    if (weight >= 40 && weight < 50) buckets["40-50"]++;
                    else if (weight >= 50 && weight < 60) buckets["50-60"]++;
                    else if (weight >= 60 && weight < 70) buckets["60-70"]++;
                    else if (weight >= 70 && weight < 80) buckets["70-80"]++;
                    else if (weight >= 80 && weight < 90) buckets["80-90"]++;
                    else if (weight >= 90) buckets["90+"]++;
                }
            }

            return new WeightStatsData
            {
                Buckets = buckets.Select(b => new WeightBucketData { Range = b.Key, Count = b.Value }).ToList()
            };
        }

        private async Task<BMIStatsData> GetBMIStats(Guid doctorId, Guid hospitalId, CancellationToken cancellationToken)
        {
            var categoryCounts = new Dictionary<string, int>
            {
                { "UNDERWEIGHT", 0 },
                { "NORMAL", 0 },
                { "OVERWEIGHT", 0 },
                { "OBESE_I", 0 },
                { "OBESE_II", 0 },
                { "OBESE_III", 0 }
            };

            var appointments = await _context.Appointments
                .AsNoTracking()
                .Where(a => a.DoctorId == doctorId && a.HospitalId == hospitalId)
                .Select(a => a.ApptId)
                .ToListAsync(cancellationToken);

            var vitalsRecords = await _context.AppointmentVitals
                .AsNoTracking()
                .Where(v => appointments.Contains(v.ApptId))
                .Select(v => v.VitalsJson)
                .ToListAsync(cancellationToken);

            foreach (var vitalsJson in vitalsRecords)
            {
                var vital = ParseVitalsJson(vitalsJson);
                if (vital.BMI.HasValue)
                {
                    var bmi = vital.BMI.Value;
                    if (bmi < 18.5m) categoryCounts["UNDERWEIGHT"]++;
                    else if (bmi >= 18.5m && bmi < 25m) categoryCounts["NORMAL"]++;
                    else if (bmi >= 25m && bmi < 30m) categoryCounts["OVERWEIGHT"]++;
                    else if (bmi >= 30m && bmi < 35m) categoryCounts["OBESE_I"]++;
                    else if (bmi >= 35m && bmi < 40m) categoryCounts["OBESE_II"]++;
                    else if (bmi >= 40m) categoryCounts["OBESE_III"]++;
                }
            }

            return new BMIStatsData
            {
                CategoryCounts = categoryCounts
            };
        }
    }
}
