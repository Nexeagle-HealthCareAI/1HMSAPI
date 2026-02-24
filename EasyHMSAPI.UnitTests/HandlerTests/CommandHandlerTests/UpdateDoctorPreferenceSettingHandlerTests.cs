using System;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class UpdateDoctorPreferenceSettingHandlerTests
    {
        private AppDbContext _context = null!;
        private UpdateDoctorPreferenceSettingHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new UpdateDoctorPreferenceSettingHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_ValidRequest_UpdatesPreferences()
        {
            // Arrange
            var doctorId = Guid.NewGuid();
            var hospitalId = Guid.NewGuid();
            var pref = new DoctorSectionPreference 
            { 
                PreferenceId = Guid.NewGuid(),
                DoctorId = doctorId, 
                HospitalId = hospitalId, 
                Vitals = false 
            };
            _context.DoctorSectionPreferences.Add(pref);
            await _context.SaveChangesAsync();

            var request = new UpdateDoctorPreferenceSettingRequestModel
            {
                DoctorId = doctorId,
                HospitalId = hospitalId,
                Preference = new DoctorSectionPreferenceUpdateModel { Vitals = true, ChiefComplaint = true }
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Success, Is.True);
            
            var updated = await _context.DoctorSectionPreferences.FirstOrDefaultAsync(p => p.DoctorId == doctorId);
            Assert.That(updated!.Vitals, Is.True);
            Assert.That(updated.ChiefComplaint, Is.True);
        }

        [Test]
        public async Task Handle_PreferenceNotFound_ReturnsFailure()
        {
            // Arrange
            var request = new UpdateDoctorPreferenceSettingRequestModel
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
