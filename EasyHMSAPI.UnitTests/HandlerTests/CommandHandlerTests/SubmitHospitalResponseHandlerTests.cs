using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class SubmitHospitalResponseHandlerTests
    {
        private AppDbContext _context = null!;
        private SubmitHospitalResponseHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new SubmitHospitalResponseHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        [Test]
        public async Task Handle_DoctorAtHospital_PersistsTaggedHospitalResponse()
        {
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID);
            TestDataFactory.SeedDoctorDepartment(_context, doctor.DoctorID, hospital.HospitalID);

            var request = new SubmitHospitalResponseRequestModel
            {
                HospitalId = hospital.HospitalID,
                DoctorId = doctor.DoctorID,
                Comment = "Thank you for your feedback, we take this seriously.",
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            var saved = _context.DoctorReviews.Single();
            Assert.That(saved.DoctorId, Is.EqualTo(doctor.DoctorID));
            Assert.That(saved.HospitalId, Is.EqualTo(hospital.HospitalID));
            Assert.That(saved.Comment, Is.EqualTo("Thank you for your feedback, we take this seriously."));
            Assert.That(saved.IsHospitalResponse, Is.True);
            Assert.That(saved.AuthorName, Is.Null);
            Assert.That(saved.IsHidden, Is.False);
        }

        [Test]
        public async Task Handle_DoctorNotAtHospital_RejectsRequest()
        {
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            // No DoctorDepartment row links this doctor to the hospital.

            var request = new SubmitHospitalResponseRequestModel
            {
                HospitalId = Guid.NewGuid(),
                DoctorId = doctor.DoctorID,
                Comment = "x",
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(_context.DoctorReviews.Count(), Is.EqualTo(0));
        }

        [Test]
        public async Task Handle_BlankComment_ReturnsFailure()
        {
            var user = TestDataFactory.SeedUser(_context);
            var doctor = TestDataFactory.SeedDoctor(_context, user);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID);
            TestDataFactory.SeedDoctorDepartment(_context, doctor.DoctorID, hospital.HospitalID);

            var request = new SubmitHospitalResponseRequestModel
            {
                HospitalId = hospital.HospitalID,
                DoctorId = doctor.DoctorID,
                Comment = "   ",
            };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(_context.DoctorReviews.Count(), Is.EqualTo(0));
        }
    }
}
