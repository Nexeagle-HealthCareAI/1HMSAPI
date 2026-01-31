using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class UpdatePatientVitalsHandlerTests
    {
        private AppDbContext _context = null!;
        private UpdatePatientVitalsHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new UpdatePatientVitalsHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_NewVitals_CreatesRecord()
        {
            // Arrange
            var apptId = Guid.NewGuid();
            var patientId = "PAT123";
            var appointment = new Appointment 
            { 
                ApptId = apptId, 
                PatientId = patientId, 
                HospitalId = Guid.NewGuid() 
            };
            _context.Appointments.Add(appointment);
            await _context.SaveChangesAsync();

            var vitalsJsonModel = new VitalsJson { Bp = new BloodPressure { Sys = 120, Dia = 80 } };
            var request = new UpdatePatientVitalsRequestModel
            {
                AppointmentId = apptId,
                PatientId = patientId,
                VitalsJson = vitalsJsonModel,
                RecordedBy = Guid.NewGuid()
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.VitalId, Is.Not.EqualTo(Guid.Empty));
            
            var vitals = await _context.AppointmentVitals.FirstOrDefaultAsync(v => v.ApptId == apptId);
            Assert.That(vitals, Is.Not.Null);
            Assert.That(vitals!.VitalsJson, Does.Contain("\"Sys\":120"));
            Assert.That(vitals!.VitalsJson, Does.Contain("\"Dia\":80"));
        }

        [Test]
        public async Task Handle_AppointmentNotFound_ReturnsFailure()
        {
            // Arrange
            var request = new UpdatePatientVitalsRequestModel
            {
                AppointmentId = Guid.NewGuid(),
                PatientId = "PAT123"
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("Appointment not found"));
        }
    }
}
