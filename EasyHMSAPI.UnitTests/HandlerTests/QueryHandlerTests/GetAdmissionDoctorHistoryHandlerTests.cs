using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class GetAdmissionDoctorHistoryHandlerTests
    {
        private AppDbContext _context = null!;
        private GetAdmissionDoctorHistoryHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetAdmissionDoctorHistoryHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsRowsNewestFirst_WithDoctorNamesResolved()
        {
            var hospitalId = Guid.NewGuid();
            _context.Hospitals.Add(new Hospital
            {
                HospitalID = hospitalId, Name = "Hosp", Email = "e@m.com", Type = "General", RegistrationNumber = "REG001",
                Contact = "1234567890", Location = "Test Location", City = "Test City", State = "Test State",
                Country = "Test Country", Pincode = "123456", CreatedByUserID = Guid.NewGuid(),
            });
            _context.SaveChanges();

            var user1 = TestDataFactory.SeedUser(_context, email: "doc1@example.com", phone: "1111111111");
            var doctor1 = TestDataFactory.SeedDoctor(_context, user1);
            var user2 = TestDataFactory.SeedUser(_context, email: "doc2@example.com", phone: "2222222222");
            var doctor2 = TestDataFactory.SeedDoctor(_context, user2);

            _context.UserProfiles.Add(new UserProfile { UserProfileID = Guid.NewGuid(), UserID = user1.UserID, FullName = "Dr. Alice", UpdatedAt = DateTime.UtcNow });
            _context.UserProfiles.Add(new UserProfile { UserProfileID = Guid.NewGuid(), UserID = user2.UserID, FullName = "Dr. Bob", UpdatedAt = DateTime.UtcNow });

            var admissionId = Guid.NewGuid();
            var earlier = DateTime.UtcNow.AddDays(-2);
            var later = DateTime.UtcNow;
            _context.AdmissionDoctorAssignment.Add(new AdmissionDoctorAssignment
            {
                AssignmentId = Guid.NewGuid(), HospitalId = hospitalId, AdmissionId = admissionId,
                DoctorId = doctor1.DoctorID, AssignedAt = earlier, UnassignedAt = later, StatusCode = "REPLACED",
                CreatedAt = earlier, UpdatedAt = later,
            });
            _context.AdmissionDoctorAssignment.Add(new AdmissionDoctorAssignment
            {
                AssignmentId = Guid.NewGuid(), HospitalId = hospitalId, AdmissionId = admissionId,
                DoctorId = doctor2.DoctorID, AssignedAt = later, StatusCode = "ACTIVE",
                CreatedAt = later, UpdatedAt = later,
            });
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetAdmissionDoctorHistoryRequestModel { HospitalId = hospitalId, AdmissionId = admissionId }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            Assert.That(response.Items, Has.Count.EqualTo(2));
            Assert.That(response.Items[0].DoctorName, Is.EqualTo("Dr. Bob"));
            Assert.That(response.Items[0].StatusCode, Is.EqualTo("ACTIVE"));
            Assert.That(response.Items[0].UnassignedAt, Is.Null);
            Assert.That(response.Items[1].DoctorName, Is.EqualTo("Dr. Alice"));
            Assert.That(response.Items[1].StatusCode, Is.EqualTo("REPLACED"));
            Assert.That(response.Items[1].UnassignedAt, Is.Not.Null);
        }

        [Test]
        public async Task Handle_NoHistory_ReturnsEmptyList()
        {
            var response = await _handler.Handle(new GetAdmissionDoctorHistoryRequestModel { HospitalId = Guid.NewGuid(), AdmissionId = Guid.NewGuid() }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Items, Is.Empty);
        }
    }
}
