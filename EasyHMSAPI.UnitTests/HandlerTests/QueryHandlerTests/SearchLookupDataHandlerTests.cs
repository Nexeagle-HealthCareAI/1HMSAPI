using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.Helpers.Interfaces;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class SearchLookupDataHandlerTests
    {
        private AppDbContext _context = null!;
        private Mock<IDoctorValidationHelper> _doctorValidationHelperMock = null!;
        private SearchLookupDataHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
             _context = InMemoryDbContextFactory.CreateContext();
            _doctorValidationHelperMock = new Mock<IDoctorValidationHelper>();
            _handler = new SearchLookupDataHandler(_context, _doctorValidationHelperMock.Object);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsLookupData()
        {
            // Arrange
            var hospitalId = Guid.NewGuid();
            var hospital = new Hospital { HospitalID = hospitalId, Name = "Hosp", Type = "General", RegistrationNumber = "REG001", Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State", Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid()  };
            _context.Hospitals.Add(hospital);

            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);

            var lookupType = new LookupType { LookupTypeId = 1, LookupTypeCode = "Type1" };
            _context.LookupTypes.Add(lookupType);

            var data = new LookupPersonal
            {
                PersonalId = Guid.NewGuid(),
                LookupTypeId = 1,
                HospitalID = hospitalId,
                DoctorID = doctor.DoctorID,
                Name = "Data1",
                NameLower = "data1"
            };
            _context.LookupPersonals.Add(data);
            await _context.SaveChangesAsync();

            _doctorValidationHelperMock.Setup(x => x.ValidateDoctorAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var request = new SearchLookupDataRequestModel
            {
                HospitalId = hospitalId,
                DoctorId = doctor.DoctorID,
                LookupType = "Type1",
                SearchText = "data"
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.PersonalLookupData, Has.Count.EqualTo(1));
            Assert.That(response.PersonalLookupData[0].Name, Is.EqualTo("Data1"));
        }

        [Test]
        public async Task Handle_SearchTextMatchesCode_ReturnsMasterLookupData()
        {
            // Arrange — mirrors the ICD-10 picker's "search by code" case (e.g. "J18"), which the
            // Name-only WHERE clause used to miss entirely.
            var hospitalId = Guid.NewGuid();
            _context.Hospitals.Add(new Hospital
            {
                HospitalID = hospitalId, Name = "Hosp", Type = "General", RegistrationNumber = "REG001",
                Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State",
                Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid(),
            });

            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);

            var lookupType = new LookupType { LookupTypeId = 2, LookupTypeCode = "ICD10" };
            _context.LookupTypes.Add(lookupType);

            _context.LookupMasters.Add(new LookupMaster
            {
                LookupId = Guid.NewGuid(),
                LookupTypeId = 2,
                Code = "J18.9",
                Name = "Pneumonia, unspecified organism",
                NameLower = "pneumonia, unspecified organism",
            });
            await _context.SaveChangesAsync();

            _doctorValidationHelperMock.Setup(x => x.ValidateDoctorAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var request = new SearchLookupDataRequestModel
            {
                HospitalId = hospitalId,
                DoctorId = doctor.DoctorID,
                LookupType = "ICD10",
                SearchText = "j18",
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.MasterLookupData, Has.Count.EqualTo(1));
            Assert.That(response.MasterLookupData[0].Code, Is.EqualTo("J18.9"));
        }
    }
}
