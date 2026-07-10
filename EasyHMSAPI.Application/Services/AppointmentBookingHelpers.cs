using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Booking logic shared between RegisterAppointmentHandler (internal, staff-authenticated)
    /// and the public Nexeagle booking/confirm handlers, so both stay in sync instead of
    /// carrying two hand-copied versions of the same rules.
    /// </summary>
    public static class AppointmentBookingHelpers
    {
        // Set to 'Future' if the appointment date is in the future, else 'VitalsRequired' —
        // the single rule every booking/reschedule/confirm path uses to pick an initial status.
        public static string ResolveInitialStatus(DateTime apptDate)
        {
            return apptDate.Date > DateTime.UtcNow.Date
                ? AppConstants.AppointmentStatus_Future
                : AppConstants.AppointmentStatus_VitalsRequired;
        }

        public static async Task<PatientRegistration> FindOrCreatePatientAsync(
            AppDbContext context,
            Patient? patientInfo,
            Guid hospitalId,
            Guid? registeredByUserId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(patientInfo?.Mobile))
                throw new ArgumentException("Patient mobile number is required");

            // Find patient by both mobile and name
            var patient = await context.PatientRegistrations
                .Where(x => x.Mobile == patientInfo.Mobile && x.FullName == patientInfo.FullName)
                .FirstOrDefaultAsync(cancellationToken);

            if (patient != null)
            {
                // Update only changed fields
                if (!string.IsNullOrEmpty(patientInfo.FullName) && patientInfo.FullName != patient.FullName)
                    patient.FullName = patientInfo.FullName ?? patient.FullName;
                if (!string.IsNullOrEmpty(patientInfo.Mobile) && patientInfo.Mobile != patient.Mobile)
                    patient.Mobile = patientInfo.Mobile ?? patient.Mobile;
                if (patientInfo.Age != null && patientInfo.Age != patient.Age)
                    patient.Age = patientInfo.Age;
                if (!string.IsNullOrEmpty(patientInfo.AgeUnit) && patientInfo.AgeUnit != patient.AgeUnit)
                    patient.AgeUnit = patientInfo.AgeUnit;
                if (!string.IsNullOrEmpty(patientInfo.Sex) && patientInfo.Sex != patient.Sex)
                    patient.Sex = patientInfo.Sex ?? patient.Sex;
                if (!string.IsNullOrEmpty(patientInfo.AddressLine1) && patientInfo.AddressLine1 != patient.AddressLine)
                    patient.AddressLine = patientInfo.AddressLine1 ?? patient.AddressLine;
                if (!string.IsNullOrEmpty(patientInfo.City) && patientInfo.City != patient.City)
                    patient.City = patientInfo.City ?? patient.City;
                if (!string.IsNullOrEmpty(patientInfo.State) && patientInfo.State != patient.State)
                    patient.State = patientInfo.State ?? patient.State;
                if (!string.IsNullOrEmpty(patientInfo.Pincode) && patientInfo.Pincode != patient.Pincode)
                    patient.Pincode = patientInfo.Pincode ?? patient.Pincode;
                if (!string.IsNullOrWhiteSpace(patientInfo.InsuranceId) && patientInfo.InsuranceId != patient.InsuranceId)
                    patient.InsuranceId = patientInfo.InsuranceId ?? patient.InsuranceId;
                if (!string.IsNullOrEmpty(patientInfo.Country) && patientInfo.Country != patient.Country)
                    patient.Country = patientInfo.Country ?? patient.Country;
                if (!string.IsNullOrEmpty(patientInfo.BloodGroup)) patient.BloodGroup = patientInfo.BloodGroup;
                if (!string.IsNullOrEmpty(patientInfo.Block)) patient.Block = patientInfo.Block;
                if (!string.IsNullOrEmpty(patientInfo.AlternateMobile)) patient.AlternateMobile = patientInfo.AlternateMobile;
                if (!string.IsNullOrEmpty(patientInfo.Email)) patient.Email = patientInfo.Email;
                if (!string.IsNullOrEmpty(patientInfo.EmergencyContactName)) patient.EmergencyContactName = patientInfo.EmergencyContactName;
                if (!string.IsNullOrEmpty(patientInfo.EmergencyContactRelation)) patient.EmergencyContactRelation = patientInfo.EmergencyContactRelation;
                if (!string.IsNullOrEmpty(patientInfo.EmergencyContactPhone)) patient.EmergencyContactPhone = patientInfo.EmergencyContactPhone;
                if (!string.IsNullOrEmpty(patientInfo.GuardianName)) patient.GuardianName = patientInfo.GuardianName;
                if (!string.IsNullOrEmpty(patientInfo.GuardianRelation)) patient.GuardianRelation = patientInfo.GuardianRelation;
                if (patientInfo.MarketingConsent == true && !patient.MarketingConsent)
                {
                    patient.MarketingConsent = true;
                    patient.MarketingConsentAt = DateTime.UtcNow;
                }
                patient.HospitalId = hospitalId;
                return patient;
            }
            else
            {
                // Always create new registration if name is new, even if contact exists
                var newPatientId = await GenerateNewPatientIdAsync(context);
                var newPatient = new PatientRegistration
                {
                    RegistrationId = Guid.NewGuid(),
                    HospitalId = hospitalId,
                    PatientId = newPatientId,
                    RegisteredAt = DateTime.UtcNow,
                    RegisteredBy = registeredByUserId,
                    FullName = patientInfo.FullName ?? string.Empty,
                    Mobile = patientInfo.Mobile,
                    Age = (short)(patientInfo.Age ?? 0),
                    AgeUnit = patientInfo.AgeUnit ?? "Y",
                    Sex = patientInfo.Sex,
                    AddressLine = patientInfo.AddressLine1,
                    City = patientInfo.City,
                    State = patientInfo.State,
                    Pincode = patientInfo.Pincode,
                    InsuranceId = !string.IsNullOrWhiteSpace(patientInfo.InsuranceId) ? patientInfo.InsuranceId : null,
                    Country = patientInfo.Country ?? string.Empty,
                    BloodGroup = patientInfo.BloodGroup,
                    Block = patientInfo.Block,
                    AlternateMobile = patientInfo.AlternateMobile,
                    Email = patientInfo.Email,
                    EmergencyContactName = patientInfo.EmergencyContactName,
                    EmergencyContactRelation = patientInfo.EmergencyContactRelation,
                    EmergencyContactPhone = patientInfo.EmergencyContactPhone,
                    GuardianName = patientInfo.GuardianName,
                    GuardianRelation = patientInfo.GuardianRelation,
                    MarketingConsent = patientInfo.MarketingConsent == true,
                    MarketingConsentAt = patientInfo.MarketingConsent == true ? DateTime.UtcNow : null,
                };
                context.PatientRegistrations.Add(newPatient);
                return newPatient;
            }
        }

        private static async Task<string> GenerateNewPatientIdAsync(AppDbContext context)
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
            while (await context.PatientRegistrations.AnyAsync(p => p.PatientId == newId));
            return newId;
        }

        public static async Task<int?> AllocateTokenWithLockingAsync(
            AppDbContext context,
            Guid hospitalId,
            Guid doctorId,
            DateTime apptDate,
            Guid apptId,
            CancellationToken cancellationToken)
        {
            var queueDate = apptDate.Date;

            var doctorQueue = await context.DoctorQueues
                .Where(dq => dq.DoctorId == doctorId && dq.TokenDate == queueDate)
                .FirstOrDefaultAsync(cancellationToken);

            int tokenNumber;
            if (doctorQueue == null)
            {
                doctorQueue = new DoctorQueue
                {
                    HospitalId = hospitalId,
                    DoctorId = doctorId,
                    TokenDate = queueDate,
                    NextTokenNo = 2,
                    TokenStrategy = AppConstants.TokenStrategy_Sequential,
                };
                context.DoctorQueues.Add(doctorQueue);
                tokenNumber = 1;
            }
            else
            {
                tokenNumber = doctorQueue.NextTokenNo;
                doctorQueue.NextTokenNo++;
            }

            var appointmentToken = await context.AppointmentTokens
                .FirstOrDefaultAsync(t => t.ApptId == apptId &&
                                         t.DoctorId == doctorId &&
                                         t.TokenDate == queueDate &&
                                         t.HospitalId == hospitalId,
                                         cancellationToken);

            if (appointmentToken == null)
            {
                appointmentToken = new AppointmentToken
                {
                    TokenId = Guid.NewGuid(),
                    HospitalId = hospitalId,
                    DoctorId = doctorId,
                    ApptId = apptId,
                    TokenDate = queueDate,
                    TokenNo = tokenNumber,
                    IsManual = false,
                    CreatedAt = DateTime.UtcNow
                };
                context.AppointmentTokens.Add(appointmentToken);
            }
            else
            {
                appointmentToken.TokenNo = tokenNumber;
                appointmentToken.IsManual = false;
                appointmentToken.CreatedAt = DateTime.UtcNow;
            }

            await context.SaveChangesAsync(cancellationToken);

            return tokenNumber;
        }
    }
}
