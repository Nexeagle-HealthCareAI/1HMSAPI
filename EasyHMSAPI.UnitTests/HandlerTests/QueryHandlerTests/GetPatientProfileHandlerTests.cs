using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using NUnit.Framework;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class GetPatientProfileHandlerTests
    {
        private AppDbContext _context = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
        }

        [TearDown]
        public void TearDown()
        {
            _context?.Dispose();
            InMemoryDbContextFactory.Destroy(_context);
        }

        [Test]
        public async Task Handle_WhenPatientNotFound_ReturnsNull()
        {
            // Arrange
            var handler = new GetPatientProfileHandler(_context);
            var request = new GetPatientProfileRequestModel
            {
                HospitalId = Guid.NewGuid(),
                PatientId = "P-001"
            };

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(result, Is.Null);
        }

        [Test]
        public async Task Handle_WhenPatientExists_ReturnsProfile()
        {
            // Arrange
            var hospitalId = Guid.NewGuid();
            var patientId = "P-123";
            var entity = new PatientRegistration
            {
                RegistrationId = Guid.NewGuid(),
                HospitalId = hospitalId,
                PatientId = patientId,
                FullName = "Jane Roe",
                Mobile = "9876543210",
                AgeYears = 30,
                Sex = "F",
                AddressLine = "123 Street",
                City = "City",
                State = "State",
                Country = "Country",
                Pincode = "123456",
                InsuranceId = "INS-9",
                RegisteredBy = Guid.NewGuid(),
                RegisteredAt = DateTime.UtcNow
            };
            _context.PatientRegistrations.Add(entity);
            await _context.SaveChangesAsync();

            var handler = new GetPatientProfileHandler(_context);
            var request = new GetPatientProfileRequestModel
            {
                HospitalId = hospitalId,
                PatientId = patientId
            };

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.RegistrationId, Is.EqualTo(entity.RegistrationId));
            Assert.That(result.PatientId, Is.EqualTo(patientId));
            Assert.That(result.HospitalId, Is.EqualTo(hospitalId));
            Assert.That(result.FullName, Is.EqualTo("Jane Roe"));
        }
    }
}
