using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class RegisterAppointmentHandler : IRequestHandler<RegisterAppointmentRequestModel, RegisterAppointmentResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly ISmsService _smsService;

        public RegisterAppointmentHandler(AppDbContext context, ISmsService smsService)
        {
            _context = context;
            _smsService = smsService;
        }

        public async Task<RegisterAppointmentResponseModel> Handle(RegisterAppointmentRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                // Check doctor status before proceeding
                var doctorActive = await _context.Doctors.AnyAsync(d => d.DoctorID == request.DoctorId && d.User.UserStatusId != (int)UserStatusEnum.Revoked, cancellationToken);
                if (!doctorActive)
                {
                    throw new Exception("Doctor is not active or has been revoked.");
                }

                var patient = await AddOrUpdatePatient(request, cancellationToken);

                // Set status to 'Future' if appointment date is in the future
                var status = request.ApptDate.Date > DateTime.UtcNow.Date
                    ? AppConstants.AppointmentStatus_Future
                    : AppConstants.AppointmentStatus_VitalsRequired;

                var (appointment, isNewAppointment) = await CreateOrUpdateAppointment(request, patient, status, cancellationToken);

                // Save appointment first to ensure ApptId exists in DB
                await _context.SaveChangesAsync(cancellationToken);

                int? tokenNumber = null;
                if (request.AllocateToken && isNewAppointment)
                {
                    tokenNumber = await AllocateAppointmentToken(request, appointment, cancellationToken);
                    await _context.SaveChangesAsync(cancellationToken); // Save token after creation
                }
                else if (request.AllocateToken && !isNewAppointment)
                {
                    // Get existing token number if appointment is being updated
                    var existingToken = await _context.AppointmentTokens
                        .FirstOrDefaultAsync(t => t.ApptId == appointment.ApptId &&
                                                 t.DoctorId == request.DoctorId &&
                                                 t.TokenDate == request.ApptDate.Date &&
                                                 t.HospitalId == request.HospitalId,
                                                 cancellationToken);
                    if (existingToken != null)
                        tokenNumber = existingToken.TokenNo;
                }

                // Send SMS reminder
                bool isSmsSent = false;
                if (!string.IsNullOrWhiteSpace(patient.Mobile))
                {
                    var smsMsg = $"Dear {patient.FullName}, your appointment is booked for {appointment.ApptDate:yyyy-MM-dd} at {appointment.StartAt:HH:mm}.";
                    if (tokenNumber.HasValue)
                    {
                        smsMsg += $" Your token number is {tokenNumber}.";
                    }
                    isSmsSent = await _smsService.SendInvitationSmsAsync(patient.Mobile, smsMsg);
                }

                return new RegisterAppointmentResponseModel
                {
                    PatientId = patient.PatientId,
                    AppointmentId = appointment.ApptId,
                    Status = status,
                    TokenNumber = tokenNumber,
                    IsReminderSent = isSmsSent,
                    Message = "Appointment registered successfully"
                };
            }
            catch (DbUpdateException dbEx)
            {
                throw new Exception($"Failed to register appointment", dbEx);
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to register appointment: " + ex.Message, ex);
            }
        }

        private async Task<PatientRegistration> AddOrUpdatePatient(RegisterAppointmentRequestModel request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Patient?.Mobile))
                throw new ArgumentException("Patient mobile number is required");

            // Find patient by both mobile and name
            var patient = await _context.PatientRegistrations
                .Where(x => x.Mobile == request.Patient.Mobile && x.FullName == request.Patient.FullName)
                .FirstOrDefaultAsync(cancellationToken);

            if (patient != null)
            {
                // Update only changed fields
                if (!string.IsNullOrEmpty(request.Patient?.FullName) && request.Patient?.FullName != patient.FullName)
                    patient.FullName = request.Patient?.FullName ?? patient.FullName;
                if (!string.IsNullOrEmpty(request.Patient?.Mobile) && request.Patient?.Mobile != patient.Mobile)
                    patient.Mobile = request.Patient?.Mobile ?? patient.Mobile;
                if (request.Patient?.AgeYears != null && request.Patient.AgeYears != patient.AgeYears)
                    patient.AgeYears = request.Patient.AgeYears;
                if (!string.IsNullOrEmpty(request.Patient?.Sex) && request.Patient?.Sex != patient.Sex)
                    patient.Sex = request.Patient?.Sex ?? patient.Sex;
                if (!string.IsNullOrEmpty(request.Patient?.AddressLine1) && request.Patient?.AddressLine1 != patient.AddressLine)
                    patient.AddressLine = request.Patient?.AddressLine1 ?? patient.AddressLine;
                if (!string.IsNullOrEmpty(request.Patient?.City) && request.Patient?.City != patient.City)
                    patient.City = request.Patient?.City ?? patient.City;
                if (!string.IsNullOrEmpty(request.Patient?.State) && request.Patient?.State != patient.State)
                    patient.State = request.Patient?.State ?? patient.State;
                if (!string.IsNullOrEmpty(request.Patient?.Pincode) && request.Patient?.Pincode != patient.Pincode)
                    patient.Pincode = request.Patient?.Pincode ?? patient.Pincode;
                if (!string.IsNullOrWhiteSpace(request.Patient?.InsuranceId) && request.Patient?.InsuranceId != patient.InsuranceId)
                    patient.InsuranceId = request.Patient?.InsuranceId ?? patient.InsuranceId;
                if (!string.IsNullOrEmpty(request.Patient?.Country) && request.Patient?.Country != patient.Country)
                    patient.Country = request.Patient?.Country ?? patient.Country;
                patient.HospitalId = request.HospitalId;
                return patient;
            }
            else
            {
                // Always create new registration if name is new, even if contact exists
                var newPatientId = GenerateNewPatientId();
                var newPatient = new PatientRegistration
                {
                    RegistrationId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    PatientId = newPatientId,
                    RegisteredAt = DateTime.UtcNow,
                    RegisteredBy = request.UserId,
                    FullName = request.Patient?.FullName ?? string.Empty,
                    Mobile = request.Patient?.Mobile,
                    AgeYears = (short)(request.Patient?.AgeYears ?? 0),
                    Sex = request.Patient?.Sex,
                    AddressLine = request.Patient?.AddressLine1,
                    City = request.Patient?.City,
                    State = request.Patient?.State,
                    Pincode = request.Patient?.Pincode,
                    InsuranceId = !string.IsNullOrWhiteSpace(request.Patient?.InsuranceId) ? request.Patient?.InsuranceId : null,
                    Country = request.Patient?.Country ?? string.Empty
                };
                _context.PatientRegistrations.Add(newPatient);
                return newPatient;
            }
        }

        private async Task<(Appointment appointment, bool isNewAppointment)> CreateOrUpdateAppointment(RegisterAppointmentRequestModel request, PatientRegistration patient, string statusCode, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.UserId);

            // Check for existing appointment with same details
            var appointment = await _context.Appointments.FirstOrDefaultAsync(a =>
                a.HospitalId == request.HospitalId &&
                a.DoctorId == request.DoctorId &&
                a.PatientId == patient.PatientId &&
                a.ApptDate == request.ApptDate.Date &&
                a.StartAt == request.StartAt &&
                a.EndAt == request.StartAt.AddMinutes(request.SlotTimeInMinutes > 0 ? request.SlotTimeInMinutes : 15),
                cancellationToken);

            bool isNew = false;
            if (appointment == null)
            {
                appointment = new Appointment
                {
                    ApptId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    DoctorId = request.DoctorId,
                    PatientId = patient.PatientId, // string type
                    ApptDate = request.ApptDate.Date,
                    StartAt = request.StartAt,
                    EndAt = request.StartAt.AddMinutes(request.SlotTimeInMinutes > 0 ? request.SlotTimeInMinutes : 15),
                    CurrentStatusCode = statusCode,
                    Reason = request.Reason ?? string.Empty,
                    InsuranceId = !string.IsNullOrWhiteSpace(request?.Patient?.InsuranceId) ? request.Patient?.InsuranceId : null,
                    PaymentMode = !string.IsNullOrWhiteSpace(request?.Patient?.PaymentMode) ? request.Patient?.PaymentMode : "CASH",
                    StatusHistoryJson = $"[{{\"status\":\"{statusCode}\",\"timestamp\":\"{DateTime.UtcNow:o}\"}}]",
                    LastStatusCodeAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = request?.UserId,
                };
                _context.Appointments.Add(appointment);
                isNew = true;
            }
            else
            {
                // Update appointment fields if needed
                appointment.Reason = request.Reason ?? appointment.Reason;
                appointment.InsuranceId = !string.IsNullOrWhiteSpace(request.Patient?.InsuranceId) ? request.Patient?.InsuranceId : appointment.InsuranceId;
                appointment.PaymentMode = !string.IsNullOrWhiteSpace(request.Patient?.PaymentMode) ? request.Patient?.PaymentMode : appointment.PaymentMode;
                appointment.CurrentStatusCode = statusCode;
                appointment.StatusHistoryJson = $"[{{\"status\":\"{statusCode}\",\"timestamp\":\"{DateTime.UtcNow:o}\"}}]";
                appointment.LastStatusCodeAt = DateTime.UtcNow;
                appointment.CreatedBy = request.UserId;
                _context.Appointments.Update(appointment);
            }
            return (appointment, isNew);
        }

        private string GenerateNewPatientId()
        {
            // Generate a unique PatientId: PTID + 8-digit random number
            string newId;
            var rng = RandomNumberGenerator.Create();
            do
            {
                var bytes = new byte[4];
                rng.GetBytes(bytes);
                int num = Math.Abs(BitConverter.ToInt32(bytes, 0)) % 100000000;
                newId = $"PTID{num:D8}";
            }
            // Ensure uniqueness in DB
            while (_context.PatientRegistrations.Any(p => p.PatientId == newId));
            return newId;
        }

        private async Task<int> AllocateAppointmentToken(RegisterAppointmentRequestModel request, Appointment appointment, CancellationToken cancellationToken)
        {
            // Always allocate token for the requested appointment date (future or today)
            var queueDate = request.ApptDate.Date;
            // Try to find an existing DoctorQueue for the doctor and date
            var doctorQueue = await _context.DoctorQueues
                .FirstOrDefaultAsync(q => q.DoctorId == request.DoctorId && q.TokenDate == queueDate, cancellationToken);

            int tokenNumber;
            if (doctorQueue == null)
            {
                // If no queue exists for this date, create a new one and start token series at 1
                doctorQueue = new DoctorQueue
                {
                    HospitalId = request.HospitalId,
                    DoctorId = request.DoctorId,
                    TokenDate = queueDate,
                    NextTokenNo = 2, // Next token will be 2, current is 1
                    TokenStrategy = AppConstants.TokenStrategy_Sequential,
                };
                tokenNumber = 1;
                _context.DoctorQueues.Add(doctorQueue);
            }
            else
            {
                // Use the next token number in the queue for this date
                tokenNumber = doctorQueue.NextTokenNo;
                doctorQueue.NextTokenNo++;
                _context.DoctorQueues.Update(doctorQueue);
            }

            // Check for existing token for this appointment (should not happen for new appointments)
            var existingToken = await _context.AppointmentTokens
                .FirstOrDefaultAsync(t => t.ApptId == appointment.ApptId &&
                                         t.DoctorId == request.DoctorId &&
                                         t.TokenDate == queueDate &&
                                         t.HospitalId == request.HospitalId,
                                         cancellationToken);
            if (existingToken == null)
            {
                // Create a new token for this appointment
                var appointmentToken = new AppointmentToken
                {
                    TokenId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    DoctorId = request.DoctorId,
                    ApptId = appointment.ApptId,
                    TokenDate = queueDate,
                    TokenNo = tokenNumber,
                    IsManual = false,
                    CreatedAt = DateTime.UtcNow
                };
                _context.AppointmentTokens.Add(appointmentToken);
            }
            else
            {
                // Update the existing token if found (should rarely happen)
                existingToken.TokenNo = tokenNumber;
                existingToken.IsManual = false;
                existingToken.CreatedAt = DateTime.UtcNow;
                _context.AppointmentTokens.Update(existingToken);
            }
            return tokenNumber;
        }
    }
}
