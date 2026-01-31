using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.Helpers.Interfaces;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class GetDoctorDashboardAnalysisHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IDoctorValidationHelper> _doctorValidationHelperMock = null!;
        private GetDoctorDashboardAnalysisHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _doctorValidationHelperMock = new Mock<IDoctorValidationHelper>();
            _handler = new GetDoctorDashboardAnalysisHandler(_context, _doctorValidationHelperMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsAnalysis()
        {
            // Arrange
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var hospitalId = Guid.NewGuid();
            var hospital = new Hospital 
            { 
                HospitalID = hospitalId, 
                Name = "Hosp", 
                Email = "e@m.com",
                Type = "General",
                RegistrationNumber = "REG001",
                Contact = "1234567890",
                Location = "Test Location",
                City = "Test City",
                State = "Test State",
                Country = "Test Country",
                Pincode = "123456",
                CreatedByUserID = Guid.NewGuid()
            };
            _context.Hospitals.Add(hospital);

            var appointment = new Appointment
            {
                ApptId = Guid.NewGuid(),
                DoctorId = doctor.DoctorID,
                HospitalId = hospitalId,
                PatientId = "PAT1",
                ApptDate = DateTime.Today,
                CurrentStatusCode = "Completed",
                AppointmentType = AppConstants.AppointmentType_New
            };
            _context.Appointments.Add(appointment);
            
            var vitals = new AppointmentVitals
            {
                VitalId = Guid.NewGuid(),
                ApptId = appointment.ApptId,
                VitalsJson = JsonSerializer.Serialize(new { Bp = new { Sys = 120, Dia = 80 }, WeightKg = 75, Bmi = 24.5 })
            };
            _context.AppointmentVitals.Add(vitals);
            await _context.SaveChangesAsync();

            _doctorValidationHelperMock.Setup(x => x.ValidateDoctorAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var request = new GetDoctorDashboardAnalysisRequestModel
            {
                DoctorId = doctor.DoctorID,
                HospitalId = hospitalId
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Data, Is.Not.Null);
            Assert.That(response.Data!.KPI.TotalVisits.Overall, Is.EqualTo(1));
            Assert.That(response.Data.KPI.TotalVisits.ByBucket.Today, Is.EqualTo(1));
            Assert.That(response.Data.BPStats.CategoryCounts["NORMAL"], Is.EqualTo(0)); 
            // 120/80 is ELEVATED in the code logic? 
            // Logic: else if (systolic >= 120 && systolic <= 129 && diastolic < 80) ELEVATED
            // 120, 80 -> diastolic is 80 (not < 80). 
            // else if ((systolic >= 130 && systolic <= 139) || (diastolic >= 80 && diastolic <= 89)) HTN_STAGE_1
            // So 120/80 matches HTN_STAGE_1 because Diastolic 80 is >= 80.
            Assert.That(response.Data.BPStats.CategoryCounts["HTN_STAGE_1"], Is.EqualTo(1));
        }

        [Test]
        public async Task Handle_InvalidDoctor_ReturnsFailure()
        {
             // Arrange
            var request = new GetDoctorDashboardAnalysisRequestModel { DoctorId = Guid.NewGuid() };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Is.EqualTo("Invalid doctor Id"));
        }
    }
}
