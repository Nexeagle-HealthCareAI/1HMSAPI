using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.Helpers.Interfaces;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class GetPrescriptionDrawingsHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IDoctorValidationHelper> _doctorValidationHelperMock = null!;
        private Mock<IBlobStorageService> _blobStorageServiceMock = null!;
        private GetPrescriptionDrawingsHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _doctorValidationHelperMock = new Mock<IDoctorValidationHelper>();
            _blobStorageServiceMock = new Mock<IBlobStorageService>();
            _blobStorageServiceMock
                .Setup(b => b.RefreshUrlAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string container, string prefix, string? storedUrl, CancellationToken ct) => storedUrl);
            _handler = new GetPrescriptionDrawingsHandler(_context, _doctorValidationHelperMock.Object, _blobStorageServiceMock.Object, new Mock<IConfiguration>().Object);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsDrawings_OrderedBySequenceNo()
        {
            // Arrange
            var hospitalId = Guid.NewGuid();
            var hospital = new Hospital { HospitalID = hospitalId, Name = "Hosp", Type = "General", RegistrationNumber = "REG001", Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State", Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid() };
            _context.Hospitals.Add(hospital);

            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);

            var apptId = Guid.NewGuid();
            var patientId = "PAT1";

            // Insert out of sequence order to prove the handler sorts, not just returns insertion order.
            _context.PrescriptionDrawings.Add(TestEntityFactory.CreatePrescriptionDrawing(Guid.NewGuid(), apptId, doctor.DoctorID, hospitalId, patientId, sequenceNo: 2));
            _context.PrescriptionDrawings.Add(TestEntityFactory.CreatePrescriptionDrawing(Guid.NewGuid(), apptId, doctor.DoctorID, hospitalId, patientId, sequenceNo: 1));
            await _context.SaveChangesAsync();

            _doctorValidationHelperMock.Setup(x => x.ValidateDoctorAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var request = new GetPrescriptionDrawingsRequestModel
            {
                AppointmentId = apptId,
                PatientId = patientId,
                HospitalId = hospitalId,
                DoctorId = doctor.DoctorID
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Drawings, Has.Count.EqualTo(2));
            Assert.That(response.Drawings![0].SequenceNo, Is.EqualTo(1));
            Assert.That(response.Drawings![1].SequenceNo, Is.EqualTo(2));
        }

        [Test]
        public async Task Handle_NoDrawings_ReturnsSuccessWithEmptyList()
        {
            // Arrange
            var hospitalId = Guid.NewGuid();
            var hospital = new Hospital { HospitalID = hospitalId, Name = "Hosp", Type = "General", RegistrationNumber = "REG001", Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State", Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid() };
            _context.Hospitals.Add(hospital);

            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            await _context.SaveChangesAsync();

            _doctorValidationHelperMock.Setup(x => x.ValidateDoctorAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var request = new GetPrescriptionDrawingsRequestModel
            {
                AppointmentId = Guid.NewGuid(),
                PatientId = "PAT1",
                HospitalId = hospitalId,
                DoctorId = doctor.DoctorID
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert: an empty gallery is a normal state, not an error.
            Assert.That(response.Success, Is.True);
            Assert.That(response.Drawings, Is.Not.Null);
            Assert.That(response.Drawings, Has.Count.EqualTo(0));
        }
    }
}
