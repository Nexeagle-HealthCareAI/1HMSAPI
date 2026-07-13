using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class UpdateDoctorPublicListingHandlerTests
    {
        private AppDbContext _context = null!;
        private UpdateDoctorPublicListingHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new UpdateDoctorPublicListingHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        [Test]
        public async Task Handle_DoctorAtHospital_TurnsOnListing()
        {
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID);
            var doctor = TestDataFactory.SeedDoctor(_context, user, isPubliclyListed: false);
            TestDataFactory.SeedDoctorDepartment(_context, doctor.DoctorID, hospital.HospitalID);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new UpdateDoctorPublicListingRequestModel
            {
                HospitalId = hospital.HospitalID,
                DoctorId = doctor.DoctorID,
                IsPubliclyListed = true,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            var updated = await _context.Doctors.FirstAsync(d => d.DoctorID == doctor.DoctorID);
            Assert.That(updated.IsPubliclyListed, Is.True);
        }

        [Test]
        public async Task Handle_DoctorAtHospital_TurnsOffListing()
        {
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID);
            var doctor = TestDataFactory.SeedDoctor(_context, user, isPubliclyListed: true);
            TestDataFactory.SeedDoctorDepartment(_context, doctor.DoctorID, hospital.HospitalID);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new UpdateDoctorPublicListingRequestModel
            {
                HospitalId = hospital.HospitalID,
                DoctorId = doctor.DoctorID,
                IsPubliclyListed = false,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            var updated = await _context.Doctors.FirstAsync(d => d.DoctorID == doctor.DoctorID);
            Assert.That(updated.IsPubliclyListed, Is.False);
        }

        [Test]
        public async Task Handle_DoctorNotAtThisHospital_ReturnsFailure_DoesNotChangeFlag()
        {
            var user1 = TestDataFactory.SeedUser(_context, email: "a@example.com", phone: "1111111111");
            var hospital1 = TestDataFactory.SeedHospital(_context, user1.UserID);
            var doctor = TestDataFactory.SeedDoctor(_context, user1, isPubliclyListed: false);
            TestDataFactory.SeedDoctorDepartment(_context, doctor.DoctorID, hospital1.HospitalID);

            var user2 = TestDataFactory.SeedUser(_context, email: "b@example.com", phone: "2222222222");
            var otherHospital = TestDataFactory.SeedHospital(_context, user2.UserID);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new UpdateDoctorPublicListingRequestModel
            {
                HospitalId = otherHospital.HospitalID, // doctor is not a member of this hospital
                DoctorId = doctor.DoctorID,
                IsPubliclyListed = true,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            var unchanged = await _context.Doctors.FirstAsync(d => d.DoctorID == doctor.DoctorID);
            Assert.That(unchanged.IsPubliclyListed, Is.False, "A hospital must not be able to toggle a doctor it has no relationship with.");
        }

        [Test]
        public async Task Handle_UnknownDoctorId_ReturnsFailure()
        {
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new UpdateDoctorPublicListingRequestModel
            {
                HospitalId = hospital.HospitalID,
                DoctorId = Guid.NewGuid(),
                IsPubliclyListed = true,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }
    }
}
