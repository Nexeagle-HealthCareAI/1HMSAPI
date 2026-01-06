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

                // Determine appointment type based on patient history and prescription settings
                await SetAppointmentType(appointment, patient, request, isNewAppointment, cancellationToken);

                // Save appointment first to ensure ApptId exists in DB
                await _context.SaveChangesAsync(cancellationToken);

                int? tokenNumber = null;
                if (request.AllocateToken && isNewAppointment)
                {
                    tokenNumber = await AllocateAppointmentTokenWithLocking(request, appointment, cancellationToken);
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
                var msg = "Failed to register appointment, Db Exception" + dbEx + dbEx.InnerException + dbEx.StackTrace;
                throw new Exception(msg);
            }
            catch (Exception ex)
            {
                var msg = "Failed to register appointment" + ex + ex.InnerException + ex.StackTrace;
                throw new Exception(msg);
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
                    AppointmentType = null // Will be set by SetAppointmentType
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

        private async Task SetAppointmentType(Appointment appointment, PatientRegistration patient, RegisterAppointmentRequestModel request, bool isNewAppointment, CancellationToken cancellationToken)
        {
            string? requestPatientId = request.Patient != null ? request.Patient?.PatientId?.ToUpper() : null;

            var existingPatient = await _context.PatientRegistrations
                .Where(p => p.PatientId != null && p.PatientId.ToUpper() == requestPatientId)
                .FirstOrDefaultAsync(cancellationToken);
            if (existingPatient is null)
            {
                if (!string.IsNullOrWhiteSpace(request.Patient?.FullName))
                {
                    var requestFullName = request.Patient.FullName.Trim().ToLower();
                    existingPatient = await _context.PatientRegistrations
                        .Where(p => p.FullName != null && p.FullName.Trim().ToLower() == requestFullName)
                        .FirstOrDefaultAsync(cancellationToken);
                }
                if (existingPatient is null)
                {
                    appointment.AppointmentType = "New/Fee";
                }
                else
                {
                    var lastAppoitment = await _context.Appointments
                        .Where(a => a.PatientId == existingPatient.PatientId && a.CurrentStatusCode != AppConstants.AppointmentStatus_VitalsRequired)
                        .OrderByDescending(a => a.ApptDate)
                        .FirstOrDefaultAsync(cancellationToken);
                    if (lastAppoitment is not null)
                    {
                        var prescriptionSettings = await _context.PrescriptionSettings
                            .Where(ps => ps.DoctorId == request.DoctorId)
                            .FirstOrDefaultAsync(cancellationToken);
                        if (prescriptionSettings is not null)
                        {
                            var newDate = lastAppoitment.ApptDate.AddDays(prescriptionSettings.ValidDuration);
                            if (request.ApptDate <= newDate)
                            {
                                appointment.AppointmentType = "Old/No-Fee";
                            }
                            else
                            {
                                appointment.AppointmentType = "Old/Fee";
                            }
                        }
                        else
                        {
                            appointment.AppointmentType = "New";
                        }
                    }
                    else
                    {
                        appointment.AppointmentType = "New";
                    }
                }
            }
            else
            {
                appointment.AppointmentType = "New";
            }

            return;
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

        private async Task<int> AllocateAppointmentTokenWithLocking(RegisterAppointmentRequestModel request, Appointment appointment, CancellationToken cancellationToken)
        {
            var queueDate = request.ApptDate.Date;
            
            // Use the DbContext's execution strategy to handle retries with transactions
            var executionStrategy = _context.Database.CreateExecutionStrategy();
            
            return await executionStrategy.ExecuteAsync(async () =>
            {
                using (var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, cancellationToken))
                {
                    try
                    {
                        // Query with exclusive lock using UPDLOCK hint to prevent race conditions
                        var doctorQueue = await _context.DoctorQueues
                            .FromSql($@"SELECT * FROM DoctorQueues WITH (UPDLOCK, ROWLOCK) 
                                       WHERE DoctorId = {request.DoctorId} AND TokenDate = {queueDate}")
                            .FirstOrDefaultAsync(cancellationToken);

                        int tokenNumber;
                        if (doctorQueue == null)
                        {
                            // Create a new queue with token 1
                            doctorQueue = new DoctorQueue
                            {
                                HospitalId = request.HospitalId,
                                DoctorId = request.DoctorId,
                                TokenDate = queueDate,
                                NextTokenNo = 2,
                                TokenStrategy = AppConstants.TokenStrategy_Sequential,
                            };
                            tokenNumber = 1;
                            _context.DoctorQueues.Add(doctorQueue);
                            await _context.SaveChangesAsync(cancellationToken);
                        }
                        else
                        {
                            // Use next token number and increment
                            tokenNumber = doctorQueue.NextTokenNo;
                            doctorQueue.NextTokenNo++;
                            _context.DoctorQueues.Update(doctorQueue);
                            await _context.SaveChangesAsync(cancellationToken);
                        }

                        // Check for existing token to prevent duplicates
                        var existingToken = await _context.AppointmentTokens
                            .FirstOrDefaultAsync(t => t.ApptId == appointment.ApptId &&
                                                     t.DoctorId == request.DoctorId &&
                                                     t.TokenDate == queueDate &&
                                                     t.HospitalId == request.HospitalId,
                                                     cancellationToken);
                        
                        if (existingToken == null)
                        {
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
                            existingToken.TokenNo = tokenNumber;
                            existingToken.IsManual = false;
                            existingToken.CreatedAt = DateTime.UtcNow;
                            _context.AppointmentTokens.Update(existingToken);
                        }
                        
                        await _context.SaveChangesAsync(cancellationToken);
                        await transaction.CommitAsync(cancellationToken);
                        return tokenNumber;
                    }
                    catch
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        throw;
                    }
                }
            });
        }
    }
}
