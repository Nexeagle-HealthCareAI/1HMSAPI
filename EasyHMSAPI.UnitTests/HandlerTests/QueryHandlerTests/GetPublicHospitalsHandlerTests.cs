using System.Linq;
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
    public class GetPublicHospitalsHandlerTests
    {
        private AppDbContext _context = null!;
        private GetPublicHospitalsHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetPublicHospitalsHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        [Test]
        public async Task Handle_ReturnsOnlyPubliclyListedActiveHospitals()
        {
            var user = TestDataFactory.SeedUser(_context);
            var listed = TestDataFactory.SeedHospital(_context, user.UserID, city: "Kolkata", isPubliclyListed: true);

            var user2 = TestDataFactory.SeedUser(_context, email: "b@example.com", phone: "2222222222");
            TestDataFactory.SeedHospital(_context, user2.UserID, city: "Mumbai", isPubliclyListed: false);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicHospitalsRequestModel(), CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Hospitals, Has.Count.EqualTo(1));
            Assert.That(response.Hospitals[0].HospitalId, Is.EqualTo(listed.HospitalID));
            Assert.That(response.Hospitals[0].City, Is.EqualTo("Kolkata"));
        }

        [Test]
        public async Task Handle_ExcludesInactiveHospital()
        {
            var user = TestDataFactory.SeedUser(_context);
            TestDataFactory.SeedHospital(_context, user.UserID, isPubliclyListed: true, isActive: false);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetPublicHospitalsRequestModel(), CancellationToken.None);

            Assert.That(response.Hospitals, Is.Empty);
        }
    }
}
