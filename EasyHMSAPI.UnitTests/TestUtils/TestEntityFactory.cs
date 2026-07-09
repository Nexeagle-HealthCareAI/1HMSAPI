using System;
using EasyHMSAPI.Domain.Entities;

namespace EasyHMSAPI.UnitTests.TestUtils
{
    public static class TestEntityFactory
    {
        public static User CreateUser(Guid userId, string mobile = "1234567890")
        {
            return new User
            {
                UserID = userId,
                MobileNumber = mobile,
                UserStatusId = 1,
                CreatedAt = DateTime.UtcNow
            };
        }

        public static User CreateUser(Guid userId, int userStatusId, string mobile = "1234567890")
        {
            return new User
            {
                UserID = userId,
                MobileNumber = mobile,
                UserStatusId = userStatusId,
                CreatedAt = DateTime.UtcNow
            };
        }

        public static Doctor CreateDoctor(Guid doctorId, Guid userId, string license = "LIC123")
        {
            return new Doctor
            {
                DoctorID = doctorId,
                UserID = userId,
                LicenseNumber = license,
                CreatedAt = DateTime.UtcNow
            };
        }

        public static Doctor CreateDoctor(Guid doctorId, string license = "LIC123")
        {
            return new Doctor
            {
                DoctorID = doctorId,
                UserID = Guid.NewGuid(),
                LicenseNumber = license,
                CreatedAt = DateTime.UtcNow
            };
        }

        public static Hospital CreateHospital(Guid hospitalId, Guid createdByUserId, string name = "Test Hospital")
        {
            return new Hospital
            {
                HospitalID = hospitalId,
                Name = name,
                CreatedByUserID = createdByUserId,
                Type = "General",
                RegistrationNumber = "REG123",
                Contact = "9999999999",
                Location = "Test Loc",
                City = "Test City",
                State = "Test State",
                Country = "Test Country",
                Pincode = "123456",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
        }

        public static Hospital CreateHospital(Guid hospitalId, string name = "Test Hospital")
        {
            return new Hospital
            {
                HospitalID = hospitalId,
                Name = name,
                CreatedByUserID = Guid.NewGuid(),
                Type = "General",
                RegistrationNumber = "REG123",
                Contact = "9999999999",
                Location = "Test Loc",
                City = "Test City",
                State = "Test State",
                Country = "Test Country",
                Pincode = "123456",
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };
        }

        public static PatientRegistration CreatePatientRegistration(Guid hospitalId, string patientId, string name = "John Doe")
        {
            return new PatientRegistration
            {
                RegistrationId = Guid.NewGuid(),
                HospitalId = hospitalId,
                PatientId = patientId,
                FullName = name,
                Mobile = "9876543210",
                Age = 30,
                Sex = "Male",
                AddressLine = "Test Address",
                City = "Test City",
                State = "Test State",
                Country = "Test Country",
                Pincode = "123456",
                RegisteredAt = DateTime.UtcNow
            };
        }

        public static Appointment CreateAppointment(Guid appointmentId, Guid hospitalId, Guid doctorId, string patientId)
        {
            return new Appointment
            {
                ApptId = appointmentId,
                HospitalId = hospitalId,
                DoctorId = doctorId,
                PatientId = patientId,
                ApptDate = DateTime.UtcNow.Date,
                StartAt = DateTime.UtcNow,
                EndAt = DateTime.UtcNow.AddMinutes(30),
                CurrentStatusCode = "scheduled",
                CreatedAt = DateTime.UtcNow,
                ValidUptoDate = DateTime.UtcNow.AddDays(7)
            };
        }

        public static PrescriptionSetting CreatePrescriptionSetting(Guid hospitalId, Guid doctorId)
        {
            return new PrescriptionSetting
            {
                PrescriptionSettingId = Guid.NewGuid(),
                HospitalId = hospitalId,
                DoctorId = doctorId,
                HeaderHeight = 100,
                FooterHeight = 50,
                ContentLeftMargin = 10,
                ContentRightMargin = 10,
                FontSize = 12,
                FontFamily = "Arial",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                RowVersion = new byte[0],
                ValidDuration = 7
            };
        }

        public static Prescription CreatePrescription(Guid prescriptionId, Guid appointmentId, Guid doctorId, Guid hospitalId, string patientId)
        {
            return new Prescription
            {
                PrescriptionId = prescriptionId,
                ApptId = appointmentId,
                DoctorId = doctorId,
                HospitalId = hospitalId,
                PatientId = patientId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Status = "completed"
            };
        }

        public static PrescriptionAttachment CreatePrescriptionAttachment(Guid attachmentId, Guid appointmentId, Guid doctorId, Guid hospitalId, string patientId)
        {
            return new PrescriptionAttachment
            {
                AttachmentId = attachmentId,
                ApptId = appointmentId,
                DoctorId = doctorId,
                HospitalId = hospitalId,
                PatientId = patientId,
                ReportType = "Lab Report",
                FileName = "report.pdf",
                StorageUrl = "http://example.com/report.pdf",
                UploadedAt = DateTime.UtcNow,
                UploadedBy = "TestUser"
            };
        }

        public static PrescriptionDrawing CreatePrescriptionDrawing(Guid drawingId, Guid appointmentId, Guid doctorId, Guid hospitalId, string patientId, int sequenceNo = 1)
        {
            return new PrescriptionDrawing
            {
                DrawingId = drawingId,
                ApptId = appointmentId,
                DoctorId = doctorId,
                HospitalId = hospitalId,
                PatientId = patientId,
                Label = "Test drawing",
                FileName = "drawing.png",
                StorageUrl = "http://example.com/drawing.png",
                SequenceNo = sequenceNo,
                UploadedAt = DateTime.UtcNow,
                UploadedBy = "TestUser"
            };
        }

        public static LookupPersonal CreateLookupPersonal(Guid personalId, Guid hospitalId, Guid doctorId, int lookupTypeId = 1)
        {
            return new LookupPersonal
            {
                PersonalId = personalId,
                HospitalID = hospitalId,
                DoctorID = doctorId,
                LookupTypeId = lookupTypeId,
                Name = "Test Personal Data",
                Code = "TEST_CODE",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
        }

        public static DoctorShiftOverride CreateDoctorShiftOverride(Guid overrideId, Guid doctorId, Guid hospitalId)
        {
            return new DoctorShiftOverride
            {
                OverrideID = overrideId,
                DoctorID = doctorId,
                HospitalId = hospitalId,
                ShiftName = "Morning",
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(12, 0, 0),
                SlotDurationInMinutes = 30,
                RecurringDays = "Monday,Tuesday",
                StartDate = DateTime.UtcNow.Date,
                EndDate = DateTime.UtcNow.Date.AddDays(7),
                OverrideDate = DateTime.UtcNow.Date,
                CreatedAt = DateTime.UtcNow
            };
        }
    }
}
