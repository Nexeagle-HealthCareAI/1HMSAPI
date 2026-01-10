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
                    var patients = await _context.PatientRegistrations
                        .Where(p => p.HospitalId == request.HospitalId)
                        .Select(p => new PatientDataModel
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
                            RegistrationDate = p.RegisteredAt
                        }).ToListAsync(cancellationToken);

                    // Get all appointments for this hospital to extract doctor and patient information
                    var appointments = await _context.Appointments
                        .Where(a => a.HospitalId == request.HospitalId)
                        .Select(a => new { a.DoctorId, a.PatientId })
                        .ToListAsync(cancellationToken);

                    // Get unique doctor IDs
                    var uniqueDoctorIds = appointments.Select(a => a.DoctorId).Distinct().ToList();

                    // Get doctor names from Doctor and UserProfile tables
                    var doctors = await _context.Doctors
                        .Where(d => uniqueDoctorIds.Contains(d.DoctorID))
                        .Join(_context.UserProfiles,
                            doctor => doctor.UserID,
                            userProfile => userProfile.UserID,
                            (doctor, userProfile) => new { DoctorId = doctor.DoctorID, DoctorName = userProfile.FullName })
                        .ToListAsync(cancellationToken);

                    // Build doctor data model
                    var doctorsData = new List<DoctorDataModel>();

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

                    response.PatientsData = patients;
                    response.DoctorsData = doctorsData;
                    response.Success = true;
                    response.Message = (patients?.Count > 0 || doctorsData?.Count > 0) ? "Data retrieved successfully." : "No data found for the hospital";
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
    }
}
