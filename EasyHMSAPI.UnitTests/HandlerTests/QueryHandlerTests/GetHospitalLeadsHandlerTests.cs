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
    public class GetHospitalLeadsHandlerTests
    {
        private AppDbContext _context = null!;
        private GetHospitalLeadsHandler _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new GetHospitalLeadsHandler(_context);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private HospitalLead SeedLead(Guid hospitalId, string source, string leadType, Guid? doctorId = null, DateTime? occurredAt = null)
        {
            var lead = new HospitalLead
            {
                LeadId = Guid.NewGuid(),
                HospitalId = hospitalId,
                DoctorId = doctorId,
                Source = source,
                LeadType = leadType,
                OccurredAt = occurredAt ?? DateTime.UtcNow,
            };
            _context.HospitalLeads.Add(lead);
            return lead;
        }

        [Test]
        public async Task Handle_EmptyHospitalId_ReturnsFailure()
        {
            var response = await _handler.Handle(new GetHospitalLeadsRequestModel { HospitalId = Guid.Empty }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_HospitalNotFound_ReturnsFailure()
        {
            var response = await _handler.Handle(new GetHospitalLeadsRequestModel { HospitalId = Guid.NewGuid() }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_ScopesToOnlyTheRequestedHospital()
        {
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID);
            SeedLead(hospital.HospitalID, "DoctorDekho", "DoctorProfileView");
            SeedLead(Guid.NewGuid(), "DoctorDekho", "DoctorProfileView"); // different hospital
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetHospitalLeadsRequestModel { HospitalId = hospital.HospitalID }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.Leads, Has.Count.EqualTo(1));
            Assert.That(response.TotalCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Handle_FiltersBySourceAndLeadType()
        {
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID);
            SeedLead(hospital.HospitalID, "WhatsApp", "DoctorNameSearch");
            SeedLead(hospital.HospitalID, "DoctorDekho", "HospitalPageView");
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(
                new GetHospitalLeadsRequestModel { HospitalId = hospital.HospitalID, Source = "WhatsApp" },
                CancellationToken.None);

            Assert.That(response.Leads, Has.Count.EqualTo(1));
            Assert.That(response.Leads[0].Source, Is.EqualTo("WhatsApp"));
            // Breakdown counts stay unfiltered by Source/LeadType -- both should still show.
            Assert.That(response.CountBySource["WhatsApp"], Is.EqualTo(1));
            Assert.That(response.CountBySource["DoctorDekho"], Is.EqualTo(1));
        }

        [Test]
        public async Task Handle_FiltersByDateWindow()
        {
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID);
            SeedLead(hospital.HospitalID, "DoctorDekho", "HospitalPageView", occurredAt: DateTime.UtcNow.AddDays(-10));
            SeedLead(hospital.HospitalID, "DoctorDekho", "HospitalPageView", occurredAt: DateTime.UtcNow);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(
                new GetHospitalLeadsRequestModel { HospitalId = hospital.HospitalID, DateFrom = DateTime.UtcNow.AddDays(-1) },
                CancellationToken.None);

            Assert.That(response.Leads, Has.Count.EqualTo(1));
            Assert.That(response.CountByType["HospitalPageView"], Is.EqualTo(1));
        }

        [Test]
        public async Task Handle_OrdersNewestFirst_AndPaginates()
        {
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID);
            var older = SeedLead(hospital.HospitalID, "DoctorDekho", "HospitalPageView", occurredAt: DateTime.UtcNow.AddHours(-2));
            var newer = SeedLead(hospital.HospitalID, "DoctorDekho", "HospitalPageView", occurredAt: DateTime.UtcNow);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(
                new GetHospitalLeadsRequestModel { HospitalId = hospital.HospitalID, Page = 1, PageSize = 1 },
                CancellationToken.None);

            Assert.That(response.TotalCount, Is.EqualTo(2));
            Assert.That(response.Leads, Has.Count.EqualTo(1));
            Assert.That(response.Leads[0].LeadId, Is.EqualTo(newer.LeadId));
        }

        [Test]
        public async Task Handle_ResolvesDoctorName_WhenDoctorIdPresent()
        {
            var user = TestDataFactory.SeedUser(_context);
            var hospital = TestDataFactory.SeedHospital(_context, user.UserID);
            var doctor = TestDataFactory.SeedDoctor(_context, user, isPubliclyListed: true);
            _context.UserProfiles.Add(new UserProfile
            {
                UserProfileID = Guid.NewGuid(),
                UserID = user.UserID,
                UserStatusId = user.UserStatusId,
                FullName = "Dr. Priya Sharma",
                UpdatedAt = DateTime.UtcNow,
            });
            SeedLead(hospital.HospitalID, "WhatsApp", "DoctorNameSearch", doctorId: doctor.DoctorID);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new GetHospitalLeadsRequestModel { HospitalId = hospital.HospitalID }, CancellationToken.None);

            Assert.That(response.Leads[0].DoctorName, Is.EqualTo("Dr. Priya Sharma"));
        }
    }
}
