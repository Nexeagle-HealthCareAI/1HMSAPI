using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    // Covers the new LetterheadMode field added for the pathology report 3-mode letterhead
    // selection (CUSTOM_TEMPLATE / BLANK_PREPRINTED / SYSTEM_DEFAULT).
    [TestFixture]
    public class UpdateLabConfigurationCommandHandlerTests
    {
        private AppDbContext _context = null!;
        private UpdateLabConfigurationCommandHandler _updateHandler = null!;
        private GetLabConfigurationQueryHandler _getHandler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _updateHandler = new UpdateLabConfigurationCommandHandler(_context);
            _getHandler = new GetLabConfigurationQueryHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_NewConfig_PersistsLetterheadMode()
        {
            var hospitalId = Guid.NewGuid();

            var result = await _updateHandler.Handle(new UpdateLabConfigurationCommand
            {
                HospitalId = hospitalId,
                LetterheadMode = "BLANK_PREPRINTED",
            }, CancellationToken.None);

            Assert.That(result, Is.True);
            var saved = _context.LabConfiguration.Single(c => c.HospitalId == hospitalId);
            Assert.That(saved.LetterheadMode, Is.EqualTo("BLANK_PREPRINTED"));
        }

        [Test]
        public async Task Handle_ExistingConfig_UpdatesLetterheadMode()
        {
            var hospitalId = Guid.NewGuid();
            _context.LabConfiguration.Add(new LabConfiguration
            {
                ConfigId = Guid.NewGuid(),
                HospitalId = hospitalId,
                LetterheadMode = "SYSTEM_DEFAULT",
            });
            _context.SaveChanges();

            await _updateHandler.Handle(new UpdateLabConfigurationCommand
            {
                HospitalId = hospitalId,
                LetterheadMode = "custom_template", // lowercase on the wire -- must still normalize
            }, CancellationToken.None);

            var saved = _context.LabConfiguration.Single(c => c.HospitalId == hospitalId);
            Assert.That(saved.LetterheadMode, Is.EqualTo("CUSTOM_TEMPLATE"));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("NOT_A_REAL_MODE")]
        public async Task Handle_MissingOrInvalidLetterheadMode_DefaultsToSystemDefault(string? mode)
        {
            var hospitalId = Guid.NewGuid();

            await _updateHandler.Handle(new UpdateLabConfigurationCommand
            {
                HospitalId = hospitalId,
                LetterheadMode = mode,
            }, CancellationToken.None);

            var saved = _context.LabConfiguration.Single(c => c.HospitalId == hospitalId);
            Assert.That(saved.LetterheadMode, Is.EqualTo("SYSTEM_DEFAULT"));
        }

        [Test]
        public async Task Handle_UpdateThenGet_RoundTripsLetterheadMode()
        {
            var hospitalId = Guid.NewGuid();

            await _updateHandler.Handle(new UpdateLabConfigurationCommand
            {
                HospitalId = hospitalId,
                LetterheadMode = "CUSTOM_TEMPLATE",
            }, CancellationToken.None);

            var fetched = await _getHandler.Handle(new GetLabConfigurationQuery { HospitalId = hospitalId }, CancellationToken.None);

            Assert.That(fetched.LetterheadMode, Is.EqualTo("CUSTOM_TEMPLATE"));
        }

        [Test]
        public async Task Handle_GetWithNoConfigRow_DefaultsToSystemDefault()
        {
            var fetched = await _getHandler.Handle(new GetLabConfigurationQuery { HospitalId = Guid.NewGuid() }, CancellationToken.None);

            Assert.That(fetched.LetterheadMode, Is.EqualTo("SYSTEM_DEFAULT"));
        }
    }
}
