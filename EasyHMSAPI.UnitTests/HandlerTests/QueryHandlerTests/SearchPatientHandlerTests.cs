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
    public class SearchPatientHandlerTests
    {
         private AppDbContext _context = null!;
        private SearchPatientHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new SearchPatientHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            
            InMemoryDbContextFactory.Destroy(_context);
            _context?.Dispose();
        }

        [Test]
        public async Task Handle_SearchByMobile_ReturnsPatient()
        {
            // Arrange
            var hospitalId = Guid.NewGuid();
            var patient = new PatientRegistration
            {
                PatientId = "PAT1",
                HospitalId = hospitalId,
                FullName = "John",
                Mobile = "1234567890"
            };
            _context.PatientRegistrations.Add(patient);
            await _context.SaveChangesAsync();

            var request = new SearchPatientRequestModel
            {
                HospitalId = hospitalId,
                SearchText = "12345"
            };

            // Act
            var response = await _handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.That(response.Items, Has.Count.EqualTo(1));
            Assert.That(response.Items[0].FullName, Is.EqualTo("John"));
        }

        [Test]
        public async Task Handle_RanksExactNameMatch_AboveStartsWithAndContains()
        {
            var hospitalId = Guid.NewGuid();
            _context.PatientRegistrations.AddRange(
                new PatientRegistration { PatientId = "PAT-CONTAINS", HospitalId = hospitalId, FullName = "Big John Doe" },
                new PatientRegistration { PatientId = "PAT-STARTS", HospitalId = hospitalId, FullName = "Johnson Lee" },
                new PatientRegistration { PatientId = "PAT-EXACT", HospitalId = hospitalId, FullName = "John" }
            );
            await _context.SaveChangesAsync();

            var request = new SearchPatientRequestModel { HospitalId = hospitalId, SearchText = "John" };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Items, Has.Count.EqualTo(3));
            Assert.That(response.Items[0].PatientId, Is.EqualTo("PAT-EXACT"));
            Assert.That(response.Items[1].PatientId, Is.EqualTo("PAT-STARTS"));
            Assert.That(response.Items[2].PatientId, Is.EqualTo("PAT-CONTAINS"));
        }

        [Test]
        public async Task Handle_FuzzySpellingVariant_StillSurfaces_RankedBelowExactMatch()
        {
            // "Smith"/"Smyth" is a textbook Soundex-equivalent pair (same phonetic code), so "Smyth"
            // passes the SQL candidate filter via Soundex even though it doesn't contain "smith" as a
            // substring — it should still come back, ranked below the exact match via Jaro-Winkler.
            // FullNameSoundex is a DB-computed PERSISTED column in production (see
            // alter_patientregistrations_add_soundex_index) — the InMemory provider doesn't emulate
            // computed columns, so tests must set it explicitly the same way the DB would.
            var hospitalId = Guid.NewGuid();
            _context.PatientRegistrations.AddRange(
                new PatientRegistration { PatientId = "PAT-EXACT", HospitalId = hospitalId, FullName = "Smith", FullNameSoundex = AppDbContext.Soundex("Smith") },
                new PatientRegistration { PatientId = "PAT-FUZZY", HospitalId = hospitalId, FullName = "Smyth", FullNameSoundex = AppDbContext.Soundex("Smyth") }
            );
            await _context.SaveChangesAsync();

            var request = new SearchPatientRequestModel { HospitalId = hospitalId, SearchText = "Smith" };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Items.Select(i => i.PatientId), Contains.Item("PAT-FUZZY"));
            var exactIndex = response.Items.FindIndex(i => i.PatientId == "PAT-EXACT");
            var fuzzyIndex = response.Items.FindIndex(i => i.PatientId == "PAT-FUZZY");
            Assert.That(exactIndex, Is.LessThan(fuzzyIndex));
        }

        [Test]
        public async Task Handle_CapsResultsAtTwenty()
        {
            var hospitalId = Guid.NewGuid();
            for (var i = 0; i < 25; i++)
            {
                _context.PatientRegistrations.Add(new PatientRegistration
                {
                    PatientId = $"PAT-{i:D2}",
                    HospitalId = hospitalId,
                    FullName = $"Test Patient {i:D2}",
                });
            }
            await _context.SaveChangesAsync();

            var request = new SearchPatientRequestModel { HospitalId = hospitalId, SearchText = "Test" };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Items, Has.Count.EqualTo(20));
        }

        [Test]
        public async Task Handle_HospitalScoping_ExcludesOtherHospitalPatients()
        {
            var hospitalA = Guid.NewGuid();
            var hospitalB = Guid.NewGuid();
            _context.PatientRegistrations.AddRange(
                new PatientRegistration { PatientId = "PAT-A", HospitalId = hospitalA, FullName = "Alice Cooper" },
                new PatientRegistration { PatientId = "PAT-B", HospitalId = hospitalB, FullName = "Alice Cooper" }
            );
            await _context.SaveChangesAsync();

            var request = new SearchPatientRequestModel { HospitalId = hospitalA, SearchText = "Alice" };

            var response = await _handler.Handle(request, CancellationToken.None);

            Assert.That(response.Items, Has.Count.EqualTo(1));
            Assert.That(response.Items[0].PatientId, Is.EqualTo("PAT-A"));
        }
    }
}
