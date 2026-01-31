using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class GetDoctorPreferenceSettingHandlerTests
    {
        private AppDbContext _context = null!;
        private GetDoctorPreferenceSettingHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetDoctorPreferenceSettingHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsPreference()
        {
            // Arrange
            var doctorId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            var pref = new DoctorSectionPreference 
            { 
                PreferenceId = Guid.NewGuid(),
                DoctorId = doctorId, 
                HospitalId = hospitalId,
                Vitals = true,
                ChiefComplaint = false 
            };
            _context.DoctorSectionPreferences.Add(pref);
            await _context.SaveChangesAsync();

            var request = new GetDoctorPreferenceSettingRequestModel
            {
                DoctorId = doctorId,
                HospitalId = hospitalId
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            Assert.That(response.Preference.Vitals, Is.True);
            Assert.That(response.Preference.ChiefComplaint, Is.False);
        }

        [Test]
        public async Task Handle_NotFound_ReturnsFailure()
        {
            // Arrange
            var request = new GetDoctorPreferenceSettingRequestModel
            {
                DoctorId = Guid.NewGuid(),
                HospitalId = Guid.NewGuid()
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("not found"));
        }
    }
}
