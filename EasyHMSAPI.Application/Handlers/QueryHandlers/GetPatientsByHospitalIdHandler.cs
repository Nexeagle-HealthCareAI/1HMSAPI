using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetPatientsByHospitalIdHandler : IRequestHandler<GetPatientsByHospitalIdRequestModel, GetPatientsByHospitalIdResponseModel>
    {
        private readonly AppDbContext _context;

        public GetPatientsByHospitalIdHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<GetPatientsByHospitalIdResponseModel> Handle(GetPatientsByHospitalIdRequestModel request, CancellationToken cancellationToken)
        {
            GetPatientsByHospitalIdResponseModel response = new()
            {
                HospitalId = request.HospitalId,
                Success = false,
            };
            try
            {
                var existingHospital = await _context.Hospitals
                    .Where(x => x.HospitalID == request.HospitalId)
                    .Select(y => new
                    {
                        y.HospitalID,
                        y.Name
                    })
                    .FirstOrDefaultAsync(cancellationToken);
                if (existingHospital is not null)
                {
                    // Get all appointments for this hospital to extract doctor and patient information
                    var appointments = await _context.Appointments
                        .Where(a => a.HospitalId == request.HospitalId)
                        .AsNoTracking()
                        .ToListAsync(cancellationToken);

                    var doctorsData = new List<DoctorDataModel>();
                    var uniqueDoctorIds = appointments.Select(a => a.DoctorId).Distinct().ToList();
                    var doctors = await _context.Doctors
                        .Where(d => uniqueDoctorIds.Contains(d.DoctorID))
                        .Join(_context.UserProfiles,
                            doctor => doctor.UserID,
                            userProfile => userProfile.UserID,
                            (doctor, userProfile) => new { DoctorId = doctor.DoctorID, DoctorName = userProfile.FullName })
                        .ToListAsync(cancellationToken);
                    
                    foreach (var doctor in doctors)
                    {
                        // Get all patient IDs for this doctor
                        var doctorPatientIds = appointments
                            .Where(a => a.DoctorId == doctor.DoctorId)
                            .Select(a => a.PatientId)
                            .Distinct()
                            .ToList();

                        // Get total patient count
                        var totalPatientCount = doctorPatientIds.Count;

                        // Get male and female patient counts
                        var patientCounts = await _context.PatientRegistrations
                            .Where(p => p.HospitalId == request.HospitalId && doctorPatientIds.Contains(p.PatientId))
                            .GroupBy(p => p.Sex)
                            .Select(g => new { Sex = g.Key, Count = g.Count() })
                            .ToListAsync(cancellationToken);

                        var maleCount = patientCounts.FirstOrDefault(p => p.Sex == AppConstants.PatientSex_Male)?.Count ?? 0;
                        var femaleCount = patientCounts.FirstOrDefault(p => p.Sex == AppConstants.PatientSex_Female)?.Count ?? 0;

                        // Calculate shared patient count
                        // A patient is shared if they have appointments with more than one doctor
                        var sharedPatientCount = 0;
                        foreach (var patientId in doctorPatientIds)
                        {
                            var appointmentCountForPatient = appointments
                                .Where(a => a.PatientId == patientId)
                                .Select(a => a.DoctorId)
                                .Distinct()
                                .Count();

                            if (appointmentCountForPatient > 1)
                            {
                                sharedPatientCount++;
                            }
                        }
                       
                        doctorsData.Add(new DoctorDataModel
                        {
                            DoctorName = doctor.DoctorName,
                            TotalPatientCount = totalPatientCount,
                            MalePatientCount = maleCount,
                            FemalePatientCount = femaleCount,
                            SharedPatientCount = sharedPatientCount
                        });
                    }


                    var patientDoctorsMap = new Dictionary<string, List<string>>();
                    foreach (var appointment in appointments)
                    {
                        var doctorName = doctors.FirstOrDefault(d => d.DoctorId == appointment.DoctorId)?.DoctorName;
                        if (!string.IsNullOrEmpty(doctorName))
                        {
                            if(!string.IsNullOrEmpty(appointment.PatientId))
                            {
                                if (!patientDoctorsMap.ContainsKey(appointment.PatientId))
                                {
                                    patientDoctorsMap[appointment.PatientId] = new List<string>();
                                }
                                if (!patientDoctorsMap[appointment.PatientId].Contains(doctorName))
                                {
                                    patientDoctorsMap[appointment.PatientId].Add(doctorName);
                                }
                            }
                        }
                    }
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
                    var patientDetails = uniquePatientsAppointments
                        .Where(a => uniquPatients.Contains(a.PatientId))
                        .Join(_context.PatientRegistrations,
                            a => a.PatientId,
                            p => p.PatientId,
                            (a, p) => new PatientDataModel
                            {
                                PatientId = p.PatientId,
                                Name = p.FullName,
                                Age = p.AgeYears,
                                Sex = p.Sex,
                                Contact = p.Mobile,
                                AddressLine = p.AddressLine,
                                City = p.City,
                                State = p.State,
                                Country = p.Country,
                                PinCode = p.Pincode,
                                RegistrationDate = p.RegisteredAt,
                                DoctorNames = patientDoctorsMap.ContainsKey(p.PatientId) ? string.Join(", ", patientDoctorsMap[p.PatientId]) : null
                            })
                        .ToList();

                    var statistics = await CalculateStatisticsAsync(request.HospitalId, patientDetails, cancellationToken);

                    response.PatientsData = patientDetails;
                    response.DoctorsData = doctorsData;
                    response.Statistics = statistics;
                    response.Success = true;
                    response.Message = (patientDetails?.Count > 0 || doctorsData?.Count > 0) ? "Data retrieved successfully." : "No data found for the hospital";
                }
                else
                {
                    response.Message = "Hospital does not exist.";
                }
            }
            catch (Exception ex)
            {
                response.Message = ex.Message + ex.InnerException + ex.StackTrace;
            }

            return response;
        }

        private async Task<HospitalPatientStatisticsModel> CalculateStatisticsAsync(Guid hospitalId, List<PatientDataModel> patients, CancellationToken cancellationToken)
        {
            var today = DateTime.UtcNow.Date;
            var yesterday = today.AddDays(-1);
            var startOfWeek = today.AddDays(-(int)today.DayOfWeek);
            var startOfMonth = new DateTime(today.Year, today.Month, 1);
            var startOfYear = new DateTime(today.Year, 1, 1);
            var startOfPreviousYear = new DateTime(today.Year - 1, 1, 1);
            var endOfPreviousYear = new DateTime(today.Year - 1, 12, 31);

            var allPatientRegistrations = await _context.PatientRegistrations
                .Where(p => p.HospitalId == hospitalId)
                .Select(p => new { p.RegisteredAt, p.Sex })
                .ToListAsync(cancellationToken);

            var newRegistrations = new NewPatientRegistrationModel
            {
                Today = allPatientRegistrations.Count(p => p.RegisteredAt.HasValue && p.RegisteredAt.Value.Date == today),
                Yesterday = allPatientRegistrations.Count(p => p.RegisteredAt.HasValue && p.RegisteredAt.Value.Date == yesterday),
                ThisWeek = allPatientRegistrations.Count(p => p.RegisteredAt.HasValue && p.RegisteredAt.Value.Date >= startOfWeek && p.RegisteredAt.Value.Date <= today),
                ThisMonth = allPatientRegistrations.Count(p => p.RegisteredAt.HasValue && p.RegisteredAt.Value.Date >= startOfMonth && p.RegisteredAt.Value.Date <= today),
                ThisYear = allPatientRegistrations.Count(p => p.RegisteredAt.HasValue && p.RegisteredAt.Value.Year == today.Year),
                PreviousYear = allPatientRegistrations.Count(p => p.RegisteredAt.HasValue && p.RegisteredAt.Value.Date >= startOfPreviousYear && p.RegisteredAt.Value.Date <= endOfPreviousYear)
            };

            var statistics = new HospitalPatientStatisticsModel
            {
                TotalPatientCount = patients.Count,
                MalePatientCount = patients.Count(p => p.Sex == AppConstants.PatientSex_Male),
                FemalePatientCount = patients.Count(p => p.Sex == AppConstants.PatientSex_Female),
                NewRegistrations = newRegistrations
            };

            return statistics;
        }
    }
}
