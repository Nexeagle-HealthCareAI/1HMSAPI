using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class PublicApiClientHandlerTests
    {
        private AppDbContext _context = null!;
        private CreatePublicApiClientHandler _createHandler = null!;
        private RevokePublicApiClientHandler _revokeHandler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _createHandler = new CreatePublicApiClientHandler(_context);
            _revokeHandler = new RevokePublicApiClientHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        [Test]
        public async Task Create_ReturnsRawKeyOnce_StoresOnlyHash()
        {
            var hospitalId = Guid.NewGuid();
            var response = await _createHandler.Handle(new CreatePublicApiClientRequestModel
            {
                HospitalId = hospitalId,
                ClientName = "Nexeagle",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.ApiKey, Is.Not.Null.And.Not.Empty);

            var stored = _context.PublicApiClient.Single(c => c.ApiClientId == response.ApiClientId);
            Assert.That(stored.ApiKeyHash, Is.Not.EqualTo(response.ApiKey));
            Assert.That(stored.IsActive, Is.True);
            Assert.That(stored.HospitalId, Is.EqualTo(hospitalId));
        }

        [Test]
        public async Task Revoke_DeactivatesKey()
        {
            var hospitalId = Guid.NewGuid();
            var created = await _createHandler.Handle(new CreatePublicApiClientRequestModel { HospitalId = hospitalId }, CancellationToken.None);

            var response = await _revokeHandler.Handle(new RevokePublicApiClientRequestModel
            {
                ApiClientId = created.ApiClientId!.Value,
                HospitalId = hospitalId,
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            var stored = _context.PublicApiClient.Single(c => c.ApiClientId == created.ApiClientId);
            Assert.That(stored.IsActive, Is.False);
        }

        [Test]
        public async Task Revoke_WrongHospital_ReturnsFailure_DoesNotDeactivate()
        {
            var hospitalId = Guid.NewGuid();
            var created = await _createHandler.Handle(new CreatePublicApiClientRequestModel { HospitalId = hospitalId }, CancellationToken.None);

            var response = await _revokeHandler.Handle(new RevokePublicApiClientRequestModel
            {
                ApiClientId = created.ApiClientId!.Value,
                HospitalId = Guid.NewGuid(),
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            var stored = _context.PublicApiClient.Single(c => c.ApiClientId == created.ApiClientId);
            Assert.That(stored.IsActive, Is.True);
        }
    }
}
