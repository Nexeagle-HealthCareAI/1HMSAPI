using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Data.Enums;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text.Json;

namespace EasyHMSAPI.Application.Handlers.CommandHandlers
{
    public class RegisterAppointmentHandler : IRequestHandler<RegisterAppointmentRequestModel, RegisterAppointmentResponseModel>
    {
        private readonly AppDbContext _context;
        private readonly ISmsService _smsService;
        private readonly IWhatsAppMessagingService _whatsAppMessagingService;
        private readonly IMediator _mediator;

        public RegisterAppointmentHandler(AppDbContext context, ISmsService smsService, IWhatsAppMessagingService whatsAppMessagingService, IMediator mediator)
        {
            _context = context;
            _smsService = smsService;
            _whatsAppMessagingService = whatsAppMessagingService;
            _mediator = mediator;
        }

        public async Task<RegisterAppointmentResponseModel> Handle(RegisterAppointmentRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                // Check doctor status before proceeding
                var existingDoctor = await _context.Doctors
                    .Where(d => d.DoctorID == request.DoctorId && d.User.UserStatusId != (int)UserStatusEnum.Revoked)
                    .Select(x => new
                    {
                        x.User.UserStatusId,
                        x.DoctorID,
                        DoctorName = x.User.UserProfiles.FirstOrDefault()!.FullName
                    })
                    .FirstOrDefaultAsync();
                if (existingDoctor is null)
                {
                    throw new Exception("Doctor is not active or has been revoked.");
                }

                var patient = new PatientRegistration();
                if (request.Patient is not null)
                {
                    patient = await AddOrUpdatePatient(request, cancellationToken);
                }

                // Set status to 'Future' if appointment date is in the future
                var status = request.ApptDate.Date > DateTime.UtcNow.Date
                    ? AppConstants.AppointmentStatus_Future
                    : AppConstants.AppointmentStatus_VitalsRequired;

                var (appointment, isNewAppointment) = await CreateOrUpdateAppointment(request, patient, status, cancellationToken);

                if(request.AppointmentId is not null)
                {
                    patient = await _context.PatientRegistrations
                        .Where(p => p.PatientId == appointment.PatientId)
                        .FirstOrDefaultAsync(cancellationToken);
                }

                // Determine appointment type based on patient history and prescription settings
                AppointmentTypeResolver.Result? typeResult = null;
                if(patient is not null)
                {
                    typeResult = await SetAppointmentType(appointment, patient, request, isNewAppointment, cancellationToken);
                }
                else
                {
                    throw new Exception("Patient not found for setting appointment type.");
                }

                // Save appointment first to ensure ApptId exists in DB
                await _context.SaveChangesAsync(cancellationToken);

                // Auto OPD billing: when the visit is chargeable (New / Old-Fee) and the billing
                // policy's OPD consult trigger is AUTO, create the encounter + consult charge and a
                // DRAFT invoice in the ledger at booking time. Best-effort — never fail the booking.
                if (isNewAppointment && typeResult?.FeeApplies == true)
                {
                    await TryAutoCreateOpdInvoice(appointment, patient, request, cancellationToken);
                }

                int? tokenNumber = null;
                if (request.AllocateToken && isNewAppointment)
                {
                    tokenNumber = await AllocateAppointmentTokenWithLocking(request, appointment, cancellationToken);
                }
                else if(request.AppointmentId is not null)
                {
                    var existingToken = await _context.AppointmentTokens
                        .Where(x => x.ApptId == request.AppointmentId)
                        .FirstOrDefaultAsync(cancellationToken);
                    if(existingToken is not null) 
                    {
                        _context.Remove(existingToken); 
                    }
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
                //bool isSmsSent = false;
                bool isReminderSent = false;
                if (!string.IsNullOrWhiteSpace(patient.Mobile))
                {
                    var smsMsg = $"Dear {patient.FullName}, your appointment is booked for {appointment.ApptDate:yyyy-MM-dd} at {appointment.StartAt:HH:mm}.";
                    var token = string.Empty;
                    if (tokenNumber.HasValue)
                    {
                        smsMsg += $" Your token number is {tokenNumber}.";
                        token = tokenNumber.HasValue ? tokenNumber.Value.ToString() : string.Empty;
                    }
                    //isSmsSent = await _smsService.SendInvitationSmsAsync(patient.Mobile, smsMsg);

                    var hospitalName = await _context.Hospitals
                        .Where(h => h.HospitalID == request.HospitalId)
                        .Select(h => h.Name)
                        .FirstOrDefaultAsync(cancellationToken);
                    var doctorName = existingDoctor.DoctorName;
                    var appointmentDate = appointment.ApptDate.Date.ToString("dd-MM-yyyy");
                    var appointmentTime = appointment.StartAt.ToString("HH:mm");
                    isReminderSent = await _whatsAppMessagingService.SendAppointmentConfirmationAsync(
                        patient.Mobile,
                        patient.FullName ?? string.Empty,
                        hospitalName ?? string.Empty,
                        doctorName,
                        token,
                        appointmentDate,
                        appointmentTime);
                }

                return new RegisterAppointmentResponseModel
                {
                    PatientId = patient.PatientId,
                    AppointmentId = appointment.ApptId,
                    Status = status,
                    TokenNumber = tokenNumber,
                    IsReminderSent = isReminderSent,
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

            bool isNew = false;
            if (request.AppointmentId is not null)
            {
                var existingAppointment = await _context.Appointments
                    .Where(x => x.ApptId == request.AppointmentId)
                    .FirstOrDefaultAsync(cancellationToken);
                if(existingAppointment is not null)
                {
                    if(existingAppointment.DoctorId != request.DoctorId)
                    {
                        existingAppointment.DoctorId = request.DoctorId;
                    }
                    if(request.ReferredByReferrerId is not null)
                    {
                        existingAppointment.ReferredByReferrerId = request.ReferredByReferrerId;
                        existingAppointment.ReferrerRelation = string.IsNullOrWhiteSpace(request.ReferrerRelation) ? null : request.ReferrerRelation;
                    }
                    if(existingAppointment.ApptDate.Date != request.ApptDate.Date)
                    {
                        existingAppointment.ApptDate = request.ApptDate;
                        if(request.ApptDate.Date > DateTime.UtcNow.Date)
                        {
                            existingAppointment.CurrentStatusCode = AppConstants.AppointmentStatus_Future;
                            existingAppointment.LastStatusCodeAt = DateTime.UtcNow;
                            var history = string.IsNullOrEmpty(existingAppointment.StatusHistoryJson)
                            ? new List<object>()
                            : JsonSerializer.Deserialize<List<object>>(existingAppointment.StatusHistoryJson) ?? new List<object>();
                            history.Add(new { status = AppConstants.AppointmentStatus_Future, timestamp = DateTime.UtcNow });
                            existingAppointment.StatusHistoryJson = JsonSerializer.Serialize(history);
                        }
                        else if(request.ApptDate.Date <= DateTime.UtcNow.Date)
                        {
                            existingAppointment.CurrentStatusCode = AppConstants.AppointmentStatus_VitalsRequired;
                            existingAppointment.LastStatusCodeAt = DateTime.UtcNow;
                            var history = string.IsNullOrEmpty(existingAppointment.StatusHistoryJson)
                            ? new List<object>()
                            : JsonSerializer.Deserialize<List<object>>(existingAppointment.StatusHistoryJson) ?? new List<object>();
                            history.Add(new { status = AppConstants.AppointmentStatus_VitalsRequired, timestamp = DateTime.UtcNow });
                            existingAppointment.StatusHistoryJson = JsonSerializer.Serialize(history);
                        }
                    }

                    var bookedSlots = await (from a in _context.Appointments
                                             join d in _context.Doctors on a.DoctorId equals d.DoctorID
                                             join u in _context.Users on d.UserID equals u.UserID
                                             where a.DoctorId == request.DoctorId && a.HospitalId == request.HospitalId && a.ApptDate.Date == request.ApptDate.Date && u.UserStatusId != (int)UserStatusEnum.Revoked && a.CurrentStatusCode != AppConstants.AppointmentStatus_Cancelled
                                             select a.StartAt.TimeOfDay)
                                     .ToListAsync(cancellationToken);

                    var requestDate = request.ApptDate.Date;

                    var overrideShifts = await _context.DoctorShiftOverrides
                        .Where(o => o.DoctorID == request.DoctorId &&
                                  o.HospitalId == request.HospitalId &&
                                  o.StartDate <= requestDate &&
                                  (!o.EndDate.HasValue || o.EndDate >= requestDate))
                        .OrderBy(o => o.StartTime)
                        .ToListAsync(cancellationToken);

                    var shiftDetails = new List<ShiftDayDetailsModel>();
                    if (overrideShifts.Count > 0)
                    {
                        shiftDetails = overrideShifts
                            .Select(shift => new ShiftDayDetailsModel
                            {
                                OverrideId = shift.OverrideID,
                                ShiftName = shift.ShiftName,
                                StartTime = shift.StartTime,
                                EndTime = shift.EndTime,
                                SlotDurationInMinutes = shift.SlotDurationInMinutes,
                                RecurringDays = shift.RecurringDays
                            })
                            .ToList();
                    }
                    else
                    {
                        shiftDetails = await _context.DoctorShiftTemplates
                            .Where(t => t.IsActive)
                            .OrderBy(t => t.StartTime)
                            .Select(t => new ShiftDayDetailsModel
                            {
                                ShiftName = t.ShiftName,
                                StartTime = t.StartTime,
                                EndTime = t.EndTime,
                                SlotDurationInMinutes = t.SlotDurationInMinutes
                            })
                            .ToListAsync(cancellationToken);
                    }

                    int slotDurationMinutes = shiftDetails.FirstOrDefault()?.SlotDurationInMinutes ?? 10;
                    var availableSlotStart = FindFirstAvailableSlot(bookedSlots, shiftDetails, slotDurationMinutes, request.ApptDate);
                    
                    if (availableSlotStart.HasValue)
                    {
                        existingAppointment.StartAt = availableSlotStart.Value;
                        request.StartAt = availableSlotStart.Value;
                        existingAppointment.EndAt = availableSlotStart.Value.AddMinutes(slotDurationMinutes);
                    }

                    return (existingAppointment, isNew);
                }
                else
                {
                    throw new ArgumentException("Appointment not found for the given AppointmentId");
                }
            }
            else
            {
                if (request.StartAt is null)
                    throw new ArgumentNullException(nameof(request.StartAt));
                if(patient.PatientId is null)
                    throw new Exception("PatientId cannot be null when creating an appointment");

                var appointment = new Appointment
                {
                    ApptId = Guid.NewGuid(),
                    HospitalId = request.HospitalId,
                    DoctorId = request.DoctorId,
                    PatientId = patient.PatientId,
                    ApptDate = request.ApptDate,
                    StartAt = request.StartAt.Value,
                    EndAt = request.StartAt.Value
                            .AddMinutes((request.SlotTimeInMinutes ?? 15) > 0 ? (request.SlotTimeInMinutes ?? 15) : 15),
                    CurrentStatusCode = statusCode,
                    Reason = request.Reason ?? string.Empty,
                    InsuranceId = !string.IsNullOrWhiteSpace(request?.Patient?.InsuranceId) ? request.Patient?.InsuranceId : null,
                    PaymentMode = !string.IsNullOrWhiteSpace(request?.Patient?.PaymentMode) ? request.Patient?.PaymentMode : "CASH",
                    StatusHistoryJson = $"[{{\"status\":\"{statusCode}\",\"timestamp\":\"{DateTime.UtcNow:o}\"}}]",
                    LastStatusCodeAt = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    CreatedBy = request?.UserId,
                    AppointmentType = null,
                    ReferredByReferrerId = request?.ReferredByReferrerId,
                    ReferrerRelation = string.IsNullOrWhiteSpace(request?.ReferrerRelation) ? null : request!.ReferrerRelation
                };
                _context.Appointments.Add(appointment);
                isNew = true;

                return (appointment, isNew);
            }
        }

        private async Task<AppointmentTypeResolver.Result> SetAppointmentType(Appointment appointment, PatientRegistration patient, RegisterAppointmentRequestModel request, bool isNewAppointment, CancellationToken cancellationToken)
        {
            // Delegated to the shared resolver so the booking decision and the consult-timeline
            // preview always agree. Inputs mirror the previous inline logic exactly.
            var result = await AppointmentTypeResolver.ResolveAsync(
                _context,
                request.Patient?.PatientId,
                patient.PatientId,
                request.Patient?.FullName,
                request.DoctorId,
                appointment.ApptDate,
                request.AppointmentId,
                cancellationToken);

            appointment.AppointmentType = result.AppointmentType;
            appointment.ValidUptoDate = result.ValidUptoDate;
            return result;
        }

        // Creates the OPD encounter + consult charge (CreateChargeEventHandler self-gates on the
        // AUTO policy and skips Old/No-Fee), then a DRAFT invoice for the posted charge. Best-effort:
        // any failure (e.g. number series not configured) is swallowed so the booking still succeeds.
        private async Task TryAutoCreateOpdInvoice(Appointment appointment, PatientRegistration patient, RegisterAppointmentRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (string.IsNullOrEmpty(patient.PatientId)) return;

                var policy = await _context.BillingPolicy
                    .FirstOrDefaultAsync(p => p.HospitalId == request.HospitalId, cancellationToken);
                if (!string.Equals(policy?.OpdConsultTrigger, "AUTO", StringComparison.OrdinalIgnoreCase))
                    return;

                var charge = await _mediator.Send(new CreateChargeEventRequestModel
                {
                    PatientId = patient.PatientId,
                    HospitalId = request.HospitalId,
                    EncounterType = "OPD",
                    AppointmentId = appointment.ApptId,
                    LoggedInUserName = request.UserId?.ToString(),
                }, cancellationToken);

                var data = charge?.Data;
                var hasCharge = data != null && (data.ConsultChargePosted || data.ConsultAlreadyCharged) && data.ConsultFee > 0;
                if (charge?.Success != true || data == null || !hasCharge)
                    return;

                await _mediator.Send(new CreateDraftInvoiceRequestModel
                {
                    PatientId = patient.PatientId,
                    HospitalId = request.HospitalId,
                    EncounterId = data.EncounterId,
                    LoggedInUserName = request.UserId?.ToString(),
                }, cancellationToken);
            }
            catch
            {
                // Non-fatal: the bill can still be created later from the Add Bill popup.
            }
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

        private static DateTime? FindFirstAvailableSlot(List<TimeSpan> bookedSlots, List<ShiftDayDetailsModel> shiftDetails, int slotDurationMinutes, DateTime appointmentDate)
        {
            foreach (var shift in shiftDetails)
            {
                var currentSlotStart = shift.StartTime ?? TimeSpan.Zero;
                var shiftEndTime = shift.EndTime ?? TimeSpan.Zero;
                
                while (currentSlotStart.Add(TimeSpan.FromMinutes(slotDurationMinutes)) <= shiftEndTime)
                {
                    if (!bookedSlots.Contains(currentSlotStart))
                    {
                        return appointmentDate.Date.Add(currentSlotStart);
                    }
                    currentSlotStart = currentSlotStart.Add(TimeSpan.FromMinutes(slotDurationMinutes));
                }
            }
            
            return null;
        }

        private async Task<int?> AllocateAppointmentTokenWithLocking(RegisterAppointmentRequestModel request, Appointment appointment, CancellationToken cancellationToken)
        {
            var queueDate = request.ApptDate.Date;
            
            var doctorQueue = await _context.DoctorQueues
                .Where(dq => dq.DoctorId == request.DoctorId && dq.TokenDate == queueDate)
                .FirstOrDefaultAsync(cancellationToken);

            int tokenNumber;
            if (doctorQueue == null)
            {
                doctorQueue = new DoctorQueue
                {
                    HospitalId = request.HospitalId,
                    DoctorId = request.DoctorId,
                    TokenDate = queueDate,
                    NextTokenNo = 2,
                    TokenStrategy = AppConstants.TokenStrategy_Sequential,
                };
                _context.DoctorQueues.Add(doctorQueue);
                tokenNumber = 1;
            }
            else
            {
                tokenNumber = doctorQueue.NextTokenNo;
                doctorQueue.NextTokenNo++;
            }

            var appointmentToken = await _context.AppointmentTokens
                .FirstOrDefaultAsync(t => t.ApptId == appointment.ApptId &&
                                         t.DoctorId == request.DoctorId &&
                                         t.TokenDate == queueDate &&
                                         t.HospitalId == request.HospitalId,
                                         cancellationToken);
            
            if (appointmentToken == null)
            {
                appointmentToken = new AppointmentToken
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
                appointmentToken.TokenNo = tokenNumber;
                appointmentToken.IsManual = false;
                appointmentToken.CreatedAt = DateTime.UtcNow;
            }
            
            await _context.SaveChangesAsync(cancellationToken);

            return tokenNumber;
        }
    }
}
