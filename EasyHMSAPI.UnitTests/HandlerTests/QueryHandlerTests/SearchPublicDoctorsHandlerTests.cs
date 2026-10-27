using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class SearchPublicDoctorsHandlerTests
    {
        // Salt Lake, Kolkata -- the reference point for the distance tests.
        private const double SaltLakeLat = 22.5800;
        private const double SaltLakeLng = 88.4200;

        private AppDbContext _context = null!;
        private SearchPublicDoctorsHandler _handler = null!;
        private ListPublicDoctorNamesHandler _namesHandler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();

            var blob = new Mock<IBlobStorageService>();
            blob.Setup(x => x.GetUrlAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("http://example.com/photo.jpg");
            var config = new Mock<IConfiguration>();
            config.Setup(c => c["BlobStorage:ProfilePhotosContainer"]).Returns("photos");

            // Separate caches: the directory handler's 60 s entries must not hide data a test adds.
            var directory = new GetPublicDoctorsHandler(_context, blob.Object, new MemoryCache(new MemoryCacheOptions()), config.Object);
            var mediator = new Mock<IMediator>();
            mediator.Setup(m => m.Send(It.IsAny<GetPublicDoctorsRequestModel>(), It.IsAny<CancellationToken>()))
                .Returns((GetPublicDoctorsRequestModel r, CancellationToken ct) => directory.Handle(r, ct));

            var cache = new MemoryCache(new MemoryCacheOptions());
            _handler = new SearchPublicDoctorsHandler(_context, cache, mediator.Object);
            _namesHandler = new ListPublicDoctorNamesHandler(_context, cache);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private Hospital SeedHospital(string city = "Kolkata", double? lat = SaltLakeLat, double? lng = SaltLakeLng, bool listed = true)
        {
            var owner = TestDataFactory.SeedUser(_context, email: $"{Guid.NewGuid():N}@h.test", phone: Guid.NewGuid().ToString("N")[..10]);
            var hospital = TestDataFactory.SeedHospital(_context, owner.UserID, city: city, isPubliclyListed: listed);
            hospital.Latitude = lat.HasValue ? (decimal)lat.Value : null;
            hospital.Longitude = lng.HasValue ? (decimal)lng.Value : null;
            _context.SaveChanges();
            return hospital;
        }

        private Doctor SeedDoctor(Hospital hospital, string name, string qualification = "MBBS", int experience = 5,
            string? departmentName = null, string languagesJson = "[\"Hindi\",\"English\"]", string? gender = null)
        {
            var user = TestDataFactory.SeedUser(_context, email: $"{Guid.NewGuid():N}@d.test", phone: Guid.NewGuid().ToString("N")[..10]);
            var doctor = TestDataFactory.SeedDoctor(_context, user, isPubliclyListed: true);
            doctor.Qualification = qualification;
            doctor.ExperienceYears = experience;
            doctor.LanguagesJson = languagesJson;

            Guid? departmentId = null;
            if (departmentName != null)
            {
                var department = new Department { DepartmentID = Guid.NewGuid(), Name = departmentName, IsActive = true, HospitalID = hospital.HospitalID };
                _context.Departments.Add(department);
                departmentId = department.DepartmentID;
                doctor.PrimaryDepartmentID = department.DepartmentID;
            }
            TestDataFactory.SeedDoctorDepartment(_context, doctor.DoctorID, hospital.HospitalID, departmentId);

            _context.UserProfiles.Add(new UserProfile
            {
                UserProfileID = Guid.NewGuid(),
                UserID = user.UserID,
                UserStatusId = user.UserStatusId,
                FullName = name,
                Gender = gender,
                UpdatedAt = DateTime.UtcNow,
            });
            _context.SaveChanges();
            return doctor;
        }

        private Task<SearchPublicDoctorsResponseModel> Search(SearchPublicDoctorsRequestModel request) =>
            _handler.Handle(request, CancellationToken.None);

        [Test]
        public async Task Name_IsSpellingTolerant()
        {
            var h = SeedHospital();
            var sharma = SeedDoctor(h, "Dr. Neha Sharma");
            SeedDoctor(h, "Dr. Anil Banerjee");

            var result = await Search(new SearchPublicDoctorsRequestModel { Query = "dr sarma" });

            Assert.That(result.Doctors.Select(d => d.Doctor.DoctorId), Is.EqualTo(new[] { sharma.DoctorID }));
            Assert.That(result.Doctors[0].NameMatchScore, Is.GreaterThanOrEqualTo(DoctorNameMatcher.MatchThreshold));
        }

        [Test]
        public async Task Degree_MatchesWholeTokenOnly()
        {
            var h = SeedHospital();
            var md = SeedDoctor(h, "Dr. A", qualification: "MBBS, M.D. (Medicine)");
            SeedDoctor(h, "Dr. B", qualification: "BDS, MDS");

            var result = await Search(new SearchPublicDoctorsRequestModel { Degree = "md" });

            Assert.That(result.Doctors.Select(d => d.Doctor.DoctorId), Is.EqualTo(new[] { md.DoctorID }));
        }

        [Test]
        public async Task Specialty_UsesDepartmentFallback()
        {
            var h = SeedHospital();
            var ortho = SeedDoctor(h, "Dr. Ortho", departmentName: "Orthopedics");
            SeedDoctor(h, "Dr. Skin", departmentName: "Dermatology");

            var result = await Search(new SearchPublicDoctorsRequestModel { SpecialtyCategories = new List<string> { "orthopedics" } });

            Assert.That(result.Doctors.Select(d => d.Doctor.DoctorId), Is.EqualTo(new[] { ortho.DoctorID }));
        }

        [Test]
        public async Task Distance_FiltersByRadius_AndSortsNearestFirst()
        {
            var near = SeedHospital(lat: 22.5850, lng: 88.4250);   // < 1 km
            var mid = SeedHospital(lat: 22.6100, lng: 88.4300);    // ~3.5 km
            var far = SeedHospital(lat: 22.8000, lng: 88.4200);    // ~24 km
            var noPin = SeedHospital(lat: null, lng: null);
            var dMid = SeedDoctor(mid, "Dr. Mid", experience: 30);
            var dNear = SeedDoctor(near, "Dr. Near", experience: 2);
            SeedDoctor(far, "Dr. Far");
            SeedDoctor(noPin, "Dr. NoPin");

            var result = await Search(new SearchPublicDoctorsRequestModel { Latitude = SaltLakeLat, Longitude = SaltLakeLng });

            Assert.That(result.Total, Is.EqualTo(2));
            Assert.That(result.Doctors.Select(d => d.Doctor.DoctorId), Is.EqualTo(new[] { dNear.DoctorID, dMid.DoctorID }));
            Assert.That(result.Doctors[0].DistanceKm, Is.LessThan(1));
        }

        [Test]
        public async Task NoLocation_RanksByExperience()
        {
            var h = SeedHospital();
            var junior = SeedDoctor(h, "Dr. Junior", experience: 3);
            var senior = SeedDoctor(h, "Dr. Senior", experience: 25);

            var result = await Search(new SearchPublicDoctorsRequestModel { Degree = "MBBS" });

            Assert.That(result.Doctors.Select(d => d.Doctor.DoctorId), Is.EqualTo(new[] { senior.DoctorID, junior.DoctorID }));
        }

        [Test]
        public async Task OwnHospital_ComesFirst_EvenWhenNotOnMarketplace_AndOthersStillFollow()
        {
            var own = SeedHospital(listed: false);
            var other = SeedHospital();
            var otherDoctor = SeedDoctor(other, "Dr. Other", experience: 30);
            var ownDoctor = SeedDoctor(own, "Dr. Own", experience: 1);

            var result = await Search(new SearchPublicDoctorsRequestModel { HospitalId = own.HospitalID, Degree = "MBBS" });

            Assert.That(result.Doctors.Select(d => d.Doctor.DoctorId), Is.EqualTo(new[] { ownDoctor.DoctorID, otherDoctor.DoctorID }));
            Assert.That(result.Doctors[0].SameHospital, Is.True);
            Assert.That(result.Doctors[1].SameHospital, Is.False);
            Assert.That(result.Doctors[0].Doctor.HospitalId, Is.EqualTo(own.HospitalID));
        }

        [Test]
        public async Task UnlistedDoctor_IsNeverReturned()
        {
            var h = SeedHospital();
            var hidden = SeedDoctor(h, "Dr. Hidden Sharma");
            hidden.IsDelistedByAdmin = true;
            _context.SaveChanges();

            var result = await Search(new SearchPublicDoctorsRequestModel { Query = "sharma" });

            Assert.That(result.Doctors, Is.Empty);
            Assert.That(result.Total, Is.EqualTo(0));
        }

        [Test]
        public async Task Limit_IsCappedAtTen_AndTotalCountsAllMatches()
        {
            var h = SeedHospital();
            for (var i = 0; i < 12; i++) SeedDoctor(h, $"Dr. Doctor {i}");

            var result = await Search(new SearchPublicDoctorsRequestModel { Degree = "MBBS", Limit = 50 });

            Assert.That(result.Doctors, Has.Count.EqualTo(10));
            Assert.That(result.Total, Is.EqualTo(12));
        }

        [Test]
        public async Task Language_And_City_Filter()
        {
            var kolkata = SeedHospital(city: "Kolkata");
            var patna = SeedHospital(city: "Patna");
            var bengali = SeedDoctor(kolkata, "Dr. Bengali", languagesJson: "[\"Bengali\",\"English\"]");
            SeedDoctor(kolkata, "Dr. Hindi", languagesJson: "[\"Hindi\"]");
            SeedDoctor(patna, "Dr. Patna Bengali", languagesJson: "[\"Bengali\"]");

            var result = await Search(new SearchPublicDoctorsRequestModel { Degree = "MBBS", City = "kolkata", Language = "bengali" });

            Assert.That(result.Doctors.Select(d => d.Doctor.DoctorId), Is.EqualTo(new[] { bengali.DoctorID }));
        }

        [Test]
        public async Task DoctorNames_ListsLiveDoctors_FilteredByCity()
        {
            var kolkata = SeedHospital(city: "Kolkata");
            var patna = SeedHospital(city: "Patna");
            SeedDoctor(kolkata, "Dr. Kolkata One");
            SeedDoctor(patna, "Dr. Patna One");

            var result = await _namesHandler.Handle(new ListPublicDoctorNamesRequestModel { City = "Kolkata" }, CancellationToken.None);

            Assert.That(result.Doctors.Select(d => d.FullName), Is.EqualTo(new[] { "Dr. Kolkata One" }));
        }

        // ------------------------------------------------------------ Vita Phase 1 filters
        [Test]
        public async Task Gender_FindsLadyDoctorsOnly_AndUnknownGenderNeverMatches()
        {
            var h = SeedHospital();
            var neha = SeedDoctor(h, "Dr. Neha Sharma", gender: "Female");
            SeedDoctor(h, "Dr. Amit Das", gender: "Male");
            SeedDoctor(h, "Dr. No Gender");
            var anita = SeedDoctor(h, "Dr. Anita Sen", gender: "F");

            var result = await Search(new SearchPublicDoctorsRequestModel { City = "Kolkata", Degree = "MBBS", Gender = "female" });

            Assert.That(result.Doctors.Select(d => d.Doctor.DoctorId), Is.EquivalentTo(new[] { neha.DoctorID, anita.DoctorID }));
            Assert.That(result.Total, Is.EqualTo(2));
            var none = await Search(new SearchPublicDoctorsRequestModel { Degree = "MBBS", Gender = "other" });
            Assert.That(none.Total, Is.EqualTo(0));
        }

        [Test]
        public async Task Offset_PagesThroughTheSameRanking()
        {
            var h = SeedHospital();
            var a = SeedDoctor(h, "Dr. A", experience: 30);
            var b = SeedDoctor(h, "Dr. B", experience: 20);
            var c = SeedDoctor(h, "Dr. C", experience: 10);

            var first = await Search(new SearchPublicDoctorsRequestModel { Degree = "MBBS", Limit = 2 });
            var second = await Search(new SearchPublicDoctorsRequestModel { Degree = "MBBS", Limit = 2, Offset = 2 });
            var past = await Search(new SearchPublicDoctorsRequestModel { Degree = "MBBS", Limit = 2, Offset = 4 });

            Assert.That(first.Doctors.Select(d => d.Doctor.DoctorId), Is.EqualTo(new[] { a.DoctorID, b.DoctorID }));
            Assert.That(second.Doctors.Select(d => d.Doctor.DoctorId), Is.EqualTo(new[] { c.DoctorID }));
            Assert.That(first.Total, Is.EqualTo(3));
            Assert.That(second.Total, Is.EqualTo(3));
            Assert.That(past.Doctors, Is.Empty);
            Assert.That(past.Total, Is.EqualTo(3));
        }

        [Test]
        public async Task OnlyAtHospital_ListsThatHospitalsDoctorsOnly()
        {
            var sadar = SeedHospital(city: "Purnia");
            var other = SeedHospital(city: "Purnia");
            var mine = SeedDoctor(sadar, "Dr. Sadar One");
            SeedDoctor(other, "Dr. Elsewhere");

            var only = await Search(new SearchPublicDoctorsRequestModel { HospitalId = sadar.HospitalID, OnlyAtHospital = true });
            var first = await Search(new SearchPublicDoctorsRequestModel { HospitalId = sadar.HospitalID });

            Assert.That(only.Doctors.Select(d => d.Doctor.DoctorId), Is.EqualTo(new[] { mine.DoctorID }));
            Assert.That(first.Doctors.Count, Is.EqualTo(2));
            Assert.That(first.Doctors[0].Doctor.DoctorId, Is.EqualTo(mine.DoctorID));
        }

        [Test]
        public async Task AvailableToday_LeavesOutDoctorsOnLeaveToday()
        {
            var h = SeedHospital();
            var working = SeedDoctor(h, "Dr. Working");
            var onLeave = SeedDoctor(h, "Dr. On Leave");
            _context.DoctorShiftTemplates.Add(new DoctorShiftTemplate
            {
                TemplateID = Guid.NewGuid(), ShiftName = "Morning", StartTime = new TimeSpan(9, 0, 0), EndTime = new TimeSpan(13, 0, 0), IsActive = true,
            });
            var todayIst = DateTime.UtcNow.AddMinutes(330).Date;
            _context.DoctorTimeOffs.Add(new DoctorTimeOff
            {
                TimeOffID = Guid.NewGuid(), DoctorID = onLeave.DoctorID, HospitalId = h.HospitalID,
                FromDate = todayIst.AddDays(-1), ToDate = todayIst.AddDays(1), Reason = "personal",
            });
            _context.SaveChanges();

            var result = await Search(new SearchPublicDoctorsRequestModel { Degree = "MBBS", AvailableToday = true });

            Assert.That(result.Doctors.Select(d => d.Doctor.DoctorId), Is.EqualTo(new[] { working.DoctorID }));
            Assert.That(result.Total, Is.EqualTo(1));
        }

        [Test]
        public void GenderKey_ReadsCommonSpellings()
        {
            Assert.That(PublicDoctorPool.GenderKey("Female"), Is.EqualTo("F"));
            Assert.That(PublicDoctorPool.GenderKey(" f "), Is.EqualTo("F"));
            Assert.That(PublicDoctorPool.GenderKey("lady"), Is.EqualTo("F"));
            Assert.That(PublicDoctorPool.GenderKey("MALE"), Is.EqualTo("M"));
            Assert.That(PublicDoctorPool.GenderKey("other"), Is.Null);
            Assert.That(PublicDoctorPool.GenderKey(null), Is.Null);
        }

        // ------------------------------------------------------------ search_hospitals
        private Task<SearchPublicHospitalsResponseModel> SearchHospitals(SearchPublicHospitalsRequestModel request) =>
            new SearchPublicHospitalsHandler(_context, new MemoryCache(new MemoryCacheOptions())).Handle(request, CancellationToken.None);

        private Hospital NamedHospital(string name, string city, double? lat = null, double? lng = null, bool listed = true, bool withDoctor = true)
        {
            var h = SeedHospital(city: city, lat: lat, lng: lng, listed: listed);
            h.Name = name;
            h.Contact = "06454-222222";
            _context.SaveChanges();
            if (withDoctor) SeedDoctor(h, $"Dr. {name} One");
            return h;
        }

        [Test]
        public async Task Hospitals_NameWithCityInTheQuery_IsSpellingTolerant()
        {
            var sadar = NamedHospital("Sadar Hospital", "Purnia");
            NamedHospital("Sadar Hospital", "Katihar");
            NamedHospital("Max Care Hospital", "Purnia");

            var result = await SearchHospitals(new SearchPublicHospitalsRequestModel { Query = "sadar haspatal purnea" });

            Assert.That(result.Hospitals.Select(h => h.HospitalId).First(), Is.EqualTo(sadar.HospitalID));
            Assert.That(result.Hospitals[0].DoctorCount, Is.EqualTo(1));
            Assert.That(result.Hospitals[0].Contact, Is.EqualTo("06454-222222"));
            Assert.That(HospitalNameMatcher.Score("sadar hospital purnia", "Sadar Hospital", "Purnia"), Is.EqualTo(1.0));
        }

        [Test]
        public async Task Hospitals_NearMe_UsesTheRadius_AndSkipsUnlistedHospitalsWithoutDoctors()
        {
            var near = NamedHospital("Salt Lake Care", "Kolkata", lat: SaltLakeLat, lng: SaltLakeLng);
            NamedHospital("Far Away", "Kolkata", lat: SaltLakeLat + 1.0, lng: SaltLakeLng);
            NamedHospital("Hidden", "Kolkata", lat: SaltLakeLat, lng: SaltLakeLng, listed: false, withDoctor: false);

            var result = await SearchHospitals(new SearchPublicHospitalsRequestModel { Latitude = SaltLakeLat, Longitude = SaltLakeLng });

            Assert.That(result.Hospitals.Select(h => h.HospitalId), Is.EqualTo(new[] { near.HospitalID }));
            Assert.That(result.Hospitals[0].DistanceKm, Is.EqualTo(0));
        }
    }

    [TestFixture]
    public class DoctorSearchHelpersTests
    {
        [TestCase("dr sarma", "Dr. Neha Sharma", true)]
        [TestCase("Sharma ji", "Dr. Neha Sharma", true)]
        [TestCase("bane", "Dr. Anil Banerjee", true)]
        [TestCase("anita sen", "Dr. Anita Sen", true)]
        [TestCase("ghosh", "Dr. Neha Sharma", false)]
        [TestCase("dr", "Dr. Neha Sharma", false)]
        public void NameMatcher(string query, string name, bool expected)
        {
            Assert.That(DoctorNameMatcher.IsMatch(query, name), Is.EqualTo(expected));
        }

        [Test]
        public void DegreeTokens_SplitsDottedAndBracketedQualifications()
        {
            var tokens = SearchPublicDoctorsHandler.DegreeTokens("M.B.B.S., M.D. (Medicine), DM-Cardiology");
            Assert.That(tokens, Is.SupersetOf(new[] { "MBBS", "MD", "MEDICINE", "DM", "CARDIOLOGY" }));
            Assert.That(tokens, Does.Not.Contain("MDS"));
        }

        [Test]
        public void Haversine_KolkataToHowrah_IsAboutTenKm()
        {
            var km = SearchPublicDoctorsHandler.HaversineKm(22.5726, 88.3639, 22.5958, 88.2636);
            Assert.That(km, Is.InRange(9.5, 11.0));
        }
    }
}
