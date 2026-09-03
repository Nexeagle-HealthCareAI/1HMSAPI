using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class RescheduleAppointmentHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<ISmsService> _smsServiceMock = null!;
        private RescheduleAppointmentHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _smsServiceMock = new Mock<ISmsService>();
            _handler = new RescheduleAppointmentHandler(_context, _smsServiceMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidRequest_ReschedulesAppointment()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var appointmentId = Guid.NewGuid();
            var patientId = "PAT123";

            var appointment = new Appointment
            {
                ApptId = appointmentId,
                DoctorId = doctor.DoctorID,
                PatientId = patientId,
                ApptDate = DateTime.Today,
                StartAt = DateTime.Today.AddHours(10),
                EndAt = DateTime.Today.AddHours(10).AddMinutes(15),
                CurrentStatusCode = "Booked"
            };
            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            var newDate = DateTime.Today.AddDays(2);
            var newStartAt = newDate.AddHours(11);

            var request = new RescheduleAppointmentRequestModel
            {
                AppointmentId = appointmentId,
                PatientId = patientId,
                ToApptDate = newDate,
                ToStartAt = newStartAt
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.FinalStatus, Is.EqualTo(AppConstants.AppointmentStatus_Future));
            
            var updatedAppt = await _context.Appointments.FindAsync(appointmentId);
            Assert.That(updatedAppt!.ApptDate, Is.EqualTo(newDate));
            Assert.That(updatedAppt.StartAt, Is.EqualTo(newStartAt));
        }

        [Test]
        public async Task Handle_CancelledAppointment_ReturnsError()
        {
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var appointmentId = Guid.NewGuid();
            var patientId = "PAT123";

            var appointment = new Appointment
            {
                ApptId = appointmentId,
                DoctorId = doctor.DoctorID,
                PatientId = patientId,
                ApptDate = DateTime.Today,
                StartAt = DateTime.Today.AddHours(10),
                EndAt = DateTime.Today.AddHours(10).AddMinutes(15),
                CurrentStatusCode = AppConstants.AppointmentStatus_Cancelled,
            };
            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            var request = new RescheduleAppointmentRequestModel
            {
                AppointmentId = appointmentId,
                PatientId = patientId,
                ToApptDate = DateTime.Today.AddDays(2),
                ToStartAt = DateTime.Today.AddDays(2).AddHours(11),
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("This appointment was cancelled — book a new appointment instead."));
        }

        [Test]
        public async Task Handle_AppointmentNotFound_ReturnsFailure()
        {
            // Arrange
            var request = new RescheduleAppointmentRequestModel
            {
                AppointmentId = Guid.NewGuid(),
                PatientId = "PAT123"
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Appointment not found."));
        }

        // ── AppointmentType/ValidUptoDate recompute + consult-charge reconciliation ──────────

        private (Guid HospitalId, Guid DoctorId, string PatientId) SeedPatientAndDoctorWithFreeWindow(int freeFollowUpDays)
        {
            var user = TestDataFactory.SeedUser(_context, email: $"{Guid.NewGuid()}@example.com");
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var hospitalId = Guid.NewGuid();
            var patientId = $"PAT-{Guid.NewGuid():N}".Substring(0, 12);

            _context.DoctorFees.Add(new DoctorFee
            {
                DoctorFeeId = Guid.NewGuid(),
                HospitalId = hospitalId,
                DoctorId = doctor.DoctorID,
                FeeType = "OPD_CONSULT",
                Amount = 500m,
                IsActive = true,
                FreeFollowUpDays = freeFollowUpDays,
            });
            _context.PatientRegistrations.Add(new PatientRegistration
            {
                RegistrationId = Guid.NewGuid(),
                HospitalId = hospitalId,
                PatientId = patientId,
                FullName = "Test Patient",
            });
            _context.SaveChanges();

            return (hospitalId, doctor.DoctorID, patientId);
        }

        private (Guid EncounterId, Guid ChargeEventId) SeedPostedConsultCharge(Guid hospitalId, string patientId, Guid apptId)
        {
            var encounterId = Guid.NewGuid();
            _context.Encounter.Add(new Encounter
            {
                EncounterId = encounterId,
                HospitalId = hospitalId,
                PatientId = patientId,
                EncounterTypeCode = "OPD",
                SourceType = "Appointments",
                SourceId = apptId,
                StatusCode = "OPEN",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            var chargeEventId = Guid.NewGuid();
            _context.BillingChargeEvent.Add(new BillingChargeEvent
            {
                ChargeEventId = chargeEventId,
                HospitalId = hospitalId,
                PatientId = patientId,
                EncounterId = encounterId,
                CategoryCode = "CONSULT",
                DisplayName = "Consultation",
                Qty = 1,
                UnitPrice = 500m,
                NetAmount = 500m,
                StatusCode = BillingConstants.ChargeEventStatus.Posted,
                ServiceDate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            _context.SaveChanges();
            return (encounterId, chargeEventId);
        }

        [Test]
        public async Task Handle_NoPriorHistory_StaysNewWithFreshValidUptoDate()
        {
            var (hospitalId, doctorId, patientId) = SeedPatientAndDoctorWithFreeWindow(freeFollowUpDays: 7);
            var appointmentId = Guid.NewGuid();
            _context.Appointments.Add(new Appointment
            {
                ApptId = appointmentId,
                HospitalId = hospitalId,
                DoctorId = doctorId,
                PatientId = patientId,
                ApptDate = DateTime.Today,
                StartAt = DateTime.Today.AddHours(10),
                EndAt = DateTime.Today.AddHours(10).AddMinutes(15),
                CurrentStatusCode = "Booked",
                AppointmentType = AppConstants.AppointmentType_New,
            });
            await _context.SaveChangesAsync();

            var newDate = DateTime.Today.AddDays(3);
            var response = await _handler.Handle(new RescheduleAppointmentRequestModel
            {
                AppointmentId = appointmentId,
                PatientId = patientId,
                ToApptDate = newDate,
                ToStartAt = newDate.AddHours(11),
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            var updated = await _context.Appointments.FindAsync(appointmentId);
            Assert.That(updated!.AppointmentType, Is.EqualTo(AppConstants.AppointmentType_New));
            Assert.That(updated.ValidUptoDate, Is.EqualTo(newDate.AddDays(7)));
        }

        [Test]
        public async Task Handle_ChargeableRescheduledIntoFreeWindow_FlipsToFreeAndVoidsUnpaidCharge()
        {
            var (hospitalId, doctorId, patientId) = SeedPatientAndDoctorWithFreeWindow(freeFollowUpDays: 10);

            // Prior chargeable visit with this doctor, 5 days before the reschedule target --
            // still within a 10-day free-follow-up window.
            _context.Appointments.Add(new Appointment
            {
                ApptId = Guid.NewGuid(),
                HospitalId = hospitalId,
                DoctorId = doctorId,
                PatientId = patientId,
                ApptDate = DateTime.Today,
                StartAt = DateTime.Today.AddHours(9),
                EndAt = DateTime.Today.AddHours(9).AddMinutes(15),
                CurrentStatusCode = AppConstants.AppointmentStatus_Completed,
                AppointmentType = AppConstants.AppointmentType_New,
            });

            var appointmentId = Guid.NewGuid();
            _context.Appointments.Add(new Appointment
            {
                ApptId = appointmentId,
                HospitalId = hospitalId,
                DoctorId = doctorId,
                PatientId = patientId,
                ApptDate = DateTime.Today.AddDays(20), // originally booked outside the free window
                StartAt = DateTime.Today.AddDays(20).AddHours(10),
                EndAt = DateTime.Today.AddDays(20).AddHours(10).AddMinutes(15),
                CurrentStatusCode = "Booked",
                AppointmentType = AppConstants.AppointmentType_OldFee,
            });
            await _context.SaveChangesAsync();

            var (_, chargeEventId) = SeedPostedConsultCharge(hospitalId, patientId, appointmentId);

            var newDate = DateTime.Today.AddDays(5); // now inside the 10-day free window
            var response = await _handler.Handle(new RescheduleAppointmentRequestModel
            {
                AppointmentId = appointmentId,
                PatientId = patientId,
                ToApptDate = newDate,
                ToStartAt = newDate.AddHours(11),
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            var updated = await _context.Appointments.FindAsync(appointmentId);
            Assert.That(updated!.AppointmentType, Is.EqualTo(AppConstants.AppointmentType_OldNoFee));

            var charge = await _context.BillingChargeEvent.FindAsync(chargeEventId);
            Assert.That(charge!.StatusCode, Is.EqualTo(BillingConstants.ChargeEventStatus.Void));
            Assert.That(charge.VoidReason, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public async Task Handle_ChargeableRescheduledIntoFreeWindow_LeavesAlreadyPaidChargeUntouched()
        {
            var (hospitalId, doctorId, patientId) = SeedPatientAndDoctorWithFreeWindow(freeFollowUpDays: 10);

            _context.Appointments.Add(new Appointment
            {
                ApptId = Guid.NewGuid(),
                HospitalId = hospitalId,
                DoctorId = doctorId,
                PatientId = patientId,
                ApptDate = DateTime.Today,
                StartAt = DateTime.Today.AddHours(9),
                EndAt = DateTime.Today.AddHours(9).AddMinutes(15),
                CurrentStatusCode = AppConstants.AppointmentStatus_Completed,
                AppointmentType = AppConstants.AppointmentType_New,
            });

            var appointmentId = Guid.NewGuid();
            _context.Appointments.Add(new Appointment
            {
                ApptId = appointmentId,
                HospitalId = hospitalId,
                DoctorId = doctorId,
                PatientId = patientId,
                ApptDate = DateTime.Today.AddDays(20),
                StartAt = DateTime.Today.AddDays(20).AddHours(10),
                EndAt = DateTime.Today.AddDays(20).AddHours(10).AddMinutes(15),
                CurrentStatusCode = "Booked",
                AppointmentType = AppConstants.AppointmentType_OldFee,
            });
            await _context.SaveChangesAsync();

            var (encounterId, chargeEventId) = SeedPostedConsultCharge(hospitalId, patientId, appointmentId);
            _context.BillingPayment.Add(new BillingPayment
            {
                PaymentId = Guid.NewGuid(),
                HospitalId = hospitalId,
                PatientId = patientId,
                EncounterId = encounterId,
                PaymentType = "PAYMENT",
                Amount = 500m,
                PaidAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
            await _context.SaveChangesAsync();

            var newDate = DateTime.Today.AddDays(5); // inside the free window
            var response = await _handler.Handle(new RescheduleAppointmentRequestModel
            {
                AppointmentId = appointmentId,
                PatientId = patientId,
                ToApptDate = newDate,
                ToStartAt = newDate.AddHours(11),
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            // AppointmentType is still corrected even though the charge is left alone -- billing
            // reconciliation for already-collected money is a human/refund decision, not automatic.
            var updated = await _context.Appointments.FindAsync(appointmentId);
            Assert.That(updated!.AppointmentType, Is.EqualTo(AppConstants.AppointmentType_OldNoFee));

            var charge = await _context.BillingChargeEvent.FindAsync(chargeEventId);
            Assert.That(charge!.StatusCode, Is.EqualTo(BillingConstants.ChargeEventStatus.Posted));
        }

        [Test]
        public async Task Handle_FreeRescheduledOutsideWindow_FlipsToChargeable()
        {
            var (hospitalId, doctorId, patientId) = SeedPatientAndDoctorWithFreeWindow(freeFollowUpDays: 7);

            _context.Appointments.Add(new Appointment
            {
                ApptId = Guid.NewGuid(),
                HospitalId = hospitalId,
                DoctorId = doctorId,
                PatientId = patientId,
                ApptDate = DateTime.Today,
                StartAt = DateTime.Today.AddHours(9),
                EndAt = DateTime.Today.AddHours(9).AddMinutes(15),
                CurrentStatusCode = AppConstants.AppointmentStatus_Completed,
                AppointmentType = AppConstants.AppointmentType_New,
            });

            var appointmentId = Guid.NewGuid();
            _context.Appointments.Add(new Appointment
            {
                ApptId = appointmentId,
                HospitalId = hospitalId,
                DoctorId = doctorId,
                PatientId = patientId,
                ApptDate = DateTime.Today.AddDays(3), // originally within the 7-day free window
                StartAt = DateTime.Today.AddDays(3).AddHours(10),
                EndAt = DateTime.Today.AddDays(3).AddHours(10).AddMinutes(15),
                CurrentStatusCode = "Booked",
                AppointmentType = AppConstants.AppointmentType_OldNoFee,
            });
            await _context.SaveChangesAsync();

            var newDate = DateTime.Today.AddDays(30); // now well outside the free window
            var response = await _handler.Handle(new RescheduleAppointmentRequestModel
            {
                AppointmentId = appointmentId,
                PatientId = patientId,
                ToApptDate = newDate,
                ToStartAt = newDate.AddHours(11),
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            var updated = await _context.Appointments.FindAsync(appointmentId);
            Assert.That(updated!.AppointmentType, Is.EqualTo(AppConstants.AppointmentType_OldFee));
        }

        [Test]
        public async Task Handle_RescheduledToDifferentDoctor_EvaluatesAgainstNewDoctorsOwnHistory()
        {
            var (hospitalId, originalDoctorId, patientId) = SeedPatientAndDoctorWithFreeWindow(freeFollowUpDays: 10);
            var otherUser = TestDataFactory.SeedUser(_context, email: $"{Guid.NewGuid()}@example.com");
            var otherDoctor = TestDataFactory.SeedDoctor(_context, otherUser);
            _context.DoctorFees.Add(new DoctorFee
            {
                DoctorFeeId = Guid.NewGuid(),
                HospitalId = hospitalId,
                DoctorId = otherDoctor.DoctorID,
                FeeType = "OPD_CONSULT",
                Amount = 700m,
                IsActive = true,
                FreeFollowUpDays = 10,
            });

            // Prior visit was with the ORIGINAL doctor, within what would be its free window.
            _context.Appointments.Add(new Appointment
            {
                ApptId = Guid.NewGuid(),
                HospitalId = hospitalId,
                DoctorId = originalDoctorId,
                PatientId = patientId,
                ApptDate = DateTime.Today,
                StartAt = DateTime.Today.AddHours(9),
                EndAt = DateTime.Today.AddHours(9).AddMinutes(15),
                CurrentStatusCode = AppConstants.AppointmentStatus_Completed,
                AppointmentType = AppConstants.AppointmentType_New,
            });

            var appointmentId = Guid.NewGuid();
            _context.Appointments.Add(new Appointment
            {
                ApptId = appointmentId,
                HospitalId = hospitalId,
                DoctorId = originalDoctorId,
                PatientId = patientId,
                ApptDate = DateTime.Today.AddDays(5),
                StartAt = DateTime.Today.AddDays(5).AddHours(10),
                EndAt = DateTime.Today.AddDays(5).AddHours(10).AddMinutes(15),
                CurrentStatusCode = "Booked",
                AppointmentType = AppConstants.AppointmentType_OldNoFee,
            });
            await _context.SaveChangesAsync();

            // Reschedule to the OTHER doctor, who has never seen this patient -- must evaluate as
            // that doctor's own first (chargeable) visit, not inherit the original doctor's free window.
            var response = await _handler.Handle(new RescheduleAppointmentRequestModel
            {
                AppointmentId = appointmentId,
                PatientId = patientId,
                ToApptDate = DateTime.Today.AddDays(6),
                ToStartAt = DateTime.Today.AddDays(6).AddHours(11),
                ToDoctorId = otherDoctor.DoctorID,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            var updated = await _context.Appointments.FindAsync(appointmentId);
            Assert.That(updated!.DoctorId, Is.EqualTo(otherDoctor.DoctorID));
            Assert.That(updated.AppointmentType, Is.EqualTo(AppConstants.AppointmentType_New));
        }
    }
}
