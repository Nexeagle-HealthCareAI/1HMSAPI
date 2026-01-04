using EasyHMSAPI.Application.Helpers.Interfaces;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    public class GetPatientAnalysisHandler : IRequestHandler<GetPatientAnalysisRequestModel, GetPatientAnalysisResponseModel>
    {
        private readonly AppDbContext _context;

        public GetPatientAnalysisHandler(AppDbContext context, IDoctorValidationHelper doctorValidationHelper)
        {
            _context = context;
        }

        public async Task<GetPatientAnalysisResponseModel> Handle(GetPatientAnalysisRequestModel request, CancellationToken cancellationToken)
        {
            GetPatientAnalysisResponseModel response = new()
            {
                HospitalId = request.HospitalId,
                PatientId = request.PatientId,
                Success = false,
            };
            try
            {
                var existingPatient = await _context.PatientRegistrations
                    .Where(x => x.PatientId == request.PatientId && x.HospitalId == request.HospitalId)
                    .FirstOrDefaultAsync(cancellationToken);
                
                if(existingPatient is not null)
                {
                    // Fetch all appointments for this patient
                    var appointments = await _context.Appointments
                        .Where(x => x.PatientId == request.PatientId && x.HospitalId == request.HospitalId)
                        .OrderBy(x => x.ApptDate)
                        .ToListAsync(cancellationToken);

                    var patientAnalysis = new PatientAnalysisDataModel();

                    // Calculate total visits
                    patientAnalysis.TotalVisit = appointments.Count;

                    // Get last visit date
                    patientAnalysis.LastVisitDate = appointments.OrderByDescending(x => x.ApptDate).FirstOrDefault()?.ApptDate;

                    // Calculate visit frequency (average gap between visits in days)
                    patientAnalysis.VisitFrequency = CalculateVisitFrequency(appointments);

                    // Determine patient tags (New, Returning, Loyal, High Risk)
                    patientAnalysis.PatientTags = DeterminePatientTags(appointments);

                    // Check for follow-ups due
                    patientAnalysis.FollowUpsDue = await CheckFollowUpsDue(request.PatientId, request.HospitalId, cancellationToken);

                    // Check for no-shows
                    patientAnalysis.NoShow = CheckNoShow(appointments);

                    // Get doctor consulted list with visit counts
                    patientAnalysis.DoctorConsulted = await GetDoctorConsultationList(request.PatientId, request.HospitalId, cancellationToken);

                    response.PatientAnalysis = patientAnalysis;
                    response.Success = true;
                    response.Message = "Patient analysis fetched successfully.";
                }
                else
                {
                    response.Message = "Patient not found.";
                }
            }
            catch(Exception ex)
            {
                response.Message = ex.Message + " | " + ex.InnerException?.Message + " | " + ex.StackTrace;
            }

            return response;
        }

        private static double CalculateVisitFrequency(List<Domain.Entities.Appointment> appointments)
        {
            if (appointments.Count <= 1)
                return 0;

            // Calculate gaps between consecutive appointments in days
            var gaps = new List<int>();
            for (int i = 1; i < appointments.Count; i++)
            {
                int gap = (int)(appointments[i].ApptDate - appointments[i - 1].ApptDate).TotalDays;
                if (gap > 0)
                    gaps.Add(gap);
            }

            // Return average gap
            if (gaps.Count == 0)
                return 0;

            return Math.Round((double)gaps.Sum() / gaps.Count, 2);
        }

        private static string DeterminePatientTags(List<Domain.Entities.Appointment> appointments)
        {
            var tags = new List<string>();

            // New Patient: First Visit
            if (appointments.Count == 1)
            {
                tags.Add("New Patient");
            }
            // Returning Patient: Visited 2 or more times
            else if (appointments.Count >= 2)
            {
                tags.Add("Returning Patient");
            }

            // Loyal Patient: At least 3 visits in last 12 months AND not high risk
            var appointmentsInLast12Months = appointments
                .Where(x => x.ApptDate >= DateTime.UtcNow.AddMonths(-12))
                .ToList();

            bool isHighRisk = CheckHighRiskEngagement(appointments);

            if (appointmentsInLast12Months.Count >= 3 && !isHighRisk)
            {
                tags.Add("Loyal Patient");
            }

            // High Risk Engagement: Cancelled or Not Completed ratio >= 60%
            if (isHighRisk)
            {
                tags.Add("High Risk Engagement");
            }

            return string.Join(", ", tags);
        }

        private static bool CheckHighRiskEngagement(List<Domain.Entities.Appointment> appointments)
        {
            if (appointments.Count == 0)
                return false;

            // Count cancelled and not completed appointments
            var cancelledOrNotCompleted = appointments
                .Where(x => x.CurrentStatusCode?.ToLower() == "cancelled" || 
                           x.CurrentStatusCode?.ToLower() == "not completed")
                .Count();

            // Calculate ratio: (cancelled + not completed) / total
            double ratio = (double)cancelledOrNotCompleted / appointments.Count;

            return ratio >= 0.6;
        }

        private async Task<bool> CheckFollowUpsDue(string? patientId, Guid hospitalId, CancellationToken cancellationToken)
        {
            // Check if there are any prescriptions with follow-up dates that have passed
            // and the patient doesn't have an appointment on or after the follow-up date
            var prescriptions = await _context.Prescription
                .Where(x => x.PatientId == patientId && x.HospitalId == hospitalId && x.FollowUpDate.HasValue)
                .ToListAsync(cancellationToken);

            if (prescriptions.Count == 0)
                return false;

            var appointments = await _context.Appointments
                .Where(x => x.PatientId == patientId && x.HospitalId == hospitalId)
                .ToListAsync(cancellationToken);

            foreach (var prescription in prescriptions)
            {
                // Check if follow-up date is in the past
                if (prescription.FollowUpDate < DateTime.UtcNow)
                {
                    // Check if patient has no appointment on or after the follow-up date
                    bool hasAppointmentAfterFollowUp = appointments
                        .Any(x => x.ApptDate >= prescription.FollowUpDate);

                    if (!hasAppointmentAfterFollowUp)
                        return true;
                }
            }

            return false;
        }

        private static bool CheckNoShow(List<Domain.Entities.Appointment> appointments)
        {
            // No-show: Appointment is in Vitals Required or Ready status
            return appointments.Any(x => 
                x.CurrentStatusCode?.ToLower() == "vitals required" || 
                x.CurrentStatusCode?.ToLower() == "ready");
        }

        private async Task<Dictionary<string, int>> GetDoctorConsultationList(string? patientId, Guid hospitalId, CancellationToken cancellationToken)
        {
            var doctorConsultations = new Dictionary<string, int>();

            var appointments = await _context.Appointments
                .Where(x => x.PatientId == patientId && x.HospitalId == hospitalId)
                .GroupBy(x => x.DoctorId)
                .Select(g => new { DoctorId = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            if (appointments.Count == 0)
                return doctorConsultations;

            // Get doctor details with user profile information
            var doctorIds = appointments.Select(x => x.DoctorId).ToList();
            var doctors = await _context.Doctors
                .Where(x => doctorIds.Contains(x.DoctorID))
                .Join(_context.Users, d => d.UserID, u => u.UserID, (d, u) => new { Doctor = d, User = u })
                .Join(_context.UserProfiles, du => du.User.UserID, up => up.UserID, 
                    (du, up) => new { du.Doctor, du.User, UserProfile = up })
                .ToListAsync(cancellationToken);

            foreach (var apt in appointments)
            {
                var doctor = doctors.FirstOrDefault(x => x.Doctor.DoctorID == apt.DoctorId);
                if (doctor != null)
                {
                    string doctorName = doctor.UserProfile?.FullName ?? "Unknown";
                    doctorConsultations[doctorName] = apt.Count;
                }
            }

            return doctorConsultations;
        }
    }
}