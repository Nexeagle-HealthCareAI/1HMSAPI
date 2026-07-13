using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class GetHospitalDoctorsHandlerTests
    {
        private AppDbContext _context = null!;
        private GetHospitalDoctorsHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetHospitalDoctorsHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsDoctorViaDoctorDepartments_EvenWithoutDoctorHospitalIdSet()
        {
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID);
            var doctor = TestDataFactory.SeedDoctor(_context, user, isPubliclyListed: true);
            // Deliberately NOT setting doctor.HospitalId — only the DoctorDepartments row, to prove
            // this handler no longer depends on the retrofitted Doctor.HospitalId field.
            TestDataFactory.SeedDoctorDepartment(_context, doctor.DoctorID, hospital.HospitalID);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetHospitalDoctorsRequestModel { HospitalId = hospital.HospitalID }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Doctors, Has.Count.EqualTo(1));
            Assert.That(response.Doctors[0].DoctorId, Is.EqualTo(doctor.DoctorID));
            Assert.That(response.Doctors[0].IsPubliclyListed, Is.True);
        }

        [Test]
        public async Task Handle_ExcludesDoctorsAtOtherHospitals()
        {
            var user1 = TestDataFactory.SeedUser(_context, email: "a@example.com", phone: "1111111111");
            var hospital1 = TestDataFactory.SeedHospital(_context, user1.UserID);
            var doctor1 = TestDataFactory.SeedDoctor(_context, user1);
            TestDataFactory.SeedDoctorDepartment(_context, doctor1.DoctorID, hospital1.HospitalID);

            var user2 = TestDataFactory.SeedUser(_context, email: "b@example.com", phone: "2222222222");
            var hospital2 = TestDataFactory.SeedHospital(_context, user2.UserID);
            var doctor2 = TestDataFactory.SeedDoctor(_context, user2);
            TestDataFactory.SeedDoctorDepartment(_context, doctor2.DoctorID, hospital2.HospitalID);

            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetHospitalDoctorsRequestModel { HospitalId = hospital1.HospitalID }, CancellationToken.None);

            Assert.That(response.Doctors, Has.Count.EqualTo(1));
            Assert.That(response.Doctors[0].DoctorId, Is.EqualTo(doctor1.DoctorID));
        }

        [Test]
        public async Task Handle_MissingHospitalId_ReturnsFailure()
        {
            var response = await _handler.Handle(new GetHospitalDoctorsRequestModel { HospitalId = Guid.Empty }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }
    }
}
