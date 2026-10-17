using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    /// <summary>Family members and their records belong to the signed-in account only; names and blood group are cleaned the same way everywhere.</summary>
    [TestFixture]
    public class PatientFamilyMemberTests
    {
        private AppDbContext _context = null!;
        private Mock<IBlobStorageService> _blob = null!;
        private static CancellationToken Ct => CancellationToken.None;
        private const string Mine = "9876543210", Other = "9123456780";

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _blob = new Mock<IBlobStorageService>();
            _blob.Setup(b => b.DeleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private Task<EasyHMSAPI.Application.ResponseModels.QueryResponseModels.PatientFamilyMembersResponseModel> Save(Action<SavePatientFamilyMemberRequestModel>? change = null, string mobile = Mine, Guid? id = null)
        {
            var r = new SavePatientFamilyMemberRequestModel { Mobile = mobile, MemberId = id, FirstName = "Ayaan", LastName = "Khan", Relation = "Son", Age = 4, AgeUnit = "y", Sex = "male", BloodGroup = "o +", Allergies = "Peanuts" };
            change?.Invoke(r);
            return new SavePatientFamilyMemberHandler(_context).Handle(r, Ct);
        }

        private DeletePatientFamilyMemberHandler Deleter() => new(_context, _blob.Object, new ConfigurationBuilder().Build());

        [Test]
        public async Task AddingAMember_KeepsCleanedDetails_AndListsThemForThatAccountOnly()
        {
            var added = await Save();
            await Save(r => r.FirstName = "Zara", mobile: Other);

            var list = await new GetPatientFamilyMembersHandler(_context).Handle(new GetPatientFamilyMembersRequestModel { Mobile = Mine }, Ct);

            Assert.That(added.Success, Is.True, added.Message);
            Assert.That(list.Members.Select(m => m.FullName), Is.EqualTo(new[] { "Ayaan Khan" }));
            Assert.That((list.Members[0].BloodGroup, list.Members[0].Sex, list.Members[0].AgeUnit, list.Members[0].Relation), Is.EqualTo(("O+", "Male", "Y", "Son")));
        }

        [Test]
        public async Task ChangingAMember_IsRefusedForSomeoneElsesAccount()
        {
            var id = (await Save()).Member!.MemberId;

            var stranger = await Save(r => r.FirstName = "Hacked", Other, id);
            var own = await Save(r => r.FirstName = "Ayan", Mine, id);

            Assert.That(stranger.Success, Is.False);
            Assert.That(stranger.StatusCode, Is.EqualTo(404));
            Assert.That(own.Success, Is.True);
            Assert.That(_context.PatientFamilyMembers.Single().FirstName, Is.EqualTo("Ayan"));
        }

        [TestCase(null, "Son", null, null)]
        [TestCase("Ayaan", null, null, null)]
        [TestCase("Ayaan", "Son", "Z+", null)]
        [TestCase("Ayaan", "Son", null, "Robot")]
        public async Task BadDetailsAreRejected(string? first, string? relation, string? blood, string? sex)
        {
            var r = await Save(x => { x.FirstName = first; x.Relation = relation; x.BloodGroup = blood; x.Sex = sex; });
            Assert.That(r.Success, Is.False);
            Assert.That(r.StatusCode, Is.EqualTo(400));
            Assert.That(_context.PatientFamilyMembers.Any(), Is.False);
        }

        [Test]
        public async Task AccountCannotHoldMoreThanTheLimit()
        {
            for (var i = 0; i < PatientProfileRules.MaxFamilyMembers; i++) Assert.That((await Save()).Success, Is.True);
            var extra = await Save();
            Assert.That(extra.Success, Is.False);
            Assert.That(await Save(mobile: Other), Has.Property("Success").True);
        }

        [Test]
        public async Task UploadingForAMember_NeedsAMemberOfThisAccount()
        {
            var id = (await Save()).Member!.MemberId;
            var handler = new UploadHealthLockerDocumentHandler(_context, _blob.Object, new ConfigurationBuilder().Build());
            var file = new Moq.Mock<Microsoft.AspNetCore.Http.IFormFile>().Object;

            var r = await handler.Handle(new UploadHealthLockerDocumentRequestModel { Mobile = Other, File = file, FileName = "a.pdf", MemberId = id }, Ct);

            Assert.That(r.Success, Is.False);
            Assert.That(r.Message, Is.EqualTo("Family member not found."));
            Assert.That(_context.PatientHealthLockerDocuments.Any(), Is.False);
        }

        [Test]
        public async Task LockerListsTheHoldersOwnRecordsApartFromAMembers()
        {
            var id = (await Save()).Member!.MemberId;
            _context.PatientHealthLockerDocuments.AddRange(
                new PatientHealthLockerDocument { DocumentId = Guid.NewGuid(), Mobile = Mine, FileName = "mine.pdf" },
                new PatientHealthLockerDocument { DocumentId = Guid.NewGuid(), Mobile = Mine, MemberId = id, FileName = "ayaan.pdf" });
            await _context.SaveChangesAsync();
            var handler = new GetHealthLockerDocumentsHandler(_context, _blob.Object, new ConfigurationBuilder().Build());

            var own = await handler.Handle(new GetHealthLockerDocumentsRequestModel { Mobile = Mine }, Ct);
            var member = await handler.Handle(new GetHealthLockerDocumentsRequestModel { Mobile = Mine, MemberId = id }, Ct);
            var list = await new GetPatientFamilyMembersHandler(_context).Handle(new GetPatientFamilyMembersRequestModel { Mobile = Mine }, Ct);

            Assert.That(own.Documents.Select(d => d.FileName), Is.EqualTo(new[] { "mine.pdf" }));
            Assert.That(member.Documents.Select(d => d.FileName), Is.EqualTo(new[] { "ayaan.pdf" }));
            Assert.That(list.Members.Single().DocumentCount, Is.EqualTo(1));
        }

        [Test]
        public async Task RemovingAMember_RemovesTheirRecordsOnly_AndNotForAStranger()
        {
            var id = (await Save()).Member!.MemberId;
            _context.PatientHealthLockerDocuments.AddRange(
                new PatientHealthLockerDocument { DocumentId = Guid.NewGuid(), Mobile = Mine, FileName = "mine.pdf" },
                new PatientHealthLockerDocument { DocumentId = Guid.NewGuid(), Mobile = Mine, MemberId = id, FileName = "ayaan.pdf" });
            await _context.SaveChangesAsync();

            var stranger = await Deleter().Handle(new DeletePatientFamilyMemberRequestModel { Mobile = Other, MemberId = id }, Ct);
            Assert.That(stranger.StatusCode, Is.EqualTo(404));
            Assert.That(_context.PatientFamilyMembers.Count(), Is.EqualTo(1));

            var done = await Deleter().Handle(new DeletePatientFamilyMemberRequestModel { Mobile = Mine, MemberId = id }, Ct);
            Assert.That(done.Success, Is.True);
            Assert.That(_context.PatientFamilyMembers.Any(), Is.False);
            Assert.That(_context.PatientHealthLockerDocuments.Select(d => d.FileName), Is.EqualTo(new[] { "mine.pdf" }));
        }

        [Test]
        public async Task LockerAllView_ListsEveryonesRecords_WithTheirVisitDetails()
        {
            var id = (await Save()).Member!.MemberId;
            _context.PatientHealthLockerDocuments.AddRange(
                new PatientHealthLockerDocument { DocumentId = Guid.NewGuid(), Mobile = Mine, FileName = "mine.pdf" },
                new PatientHealthLockerDocument { DocumentId = Guid.NewGuid(), Mobile = Mine, MemberId = id, FileName = "ayaan.pdf", ExternalDoctorName = "Dr Rao", ExternalClinicName = "Rao Clinic", ExternalLocation = "Lucknow", VisitDate = new DateTime(2026, 9, 1) },
                new PatientHealthLockerDocument { DocumentId = Guid.NewGuid(), Mobile = Other, FileName = "stranger.pdf" });
            await _context.SaveChangesAsync();
            var handler = new GetHealthLockerDocumentsHandler(_context, _blob.Object, new ConfigurationBuilder().Build());

            var all = await handler.Handle(new GetHealthLockerDocumentsRequestModel { Mobile = Mine, All = true }, Ct);

            Assert.That(all.Documents.Select(d => d.FileName), Is.EquivalentTo(new[] { "mine.pdf", "ayaan.pdf" }));
            var tagged = all.Documents.Single(d => d.FileName == "ayaan.pdf");
            Assert.That((tagged.MemberId, tagged.ExternalDoctorName, tagged.ExternalClinicName, tagged.ExternalLocation, tagged.VisitDate),
                Is.EqualTo((id, "Dr Rao", "Rao Clinic", "Lucknow", (DateTime?)new DateTime(2026, 9, 1))));
        }

        [TestCase(true, "Dr Rao", null, "Choose a visit from your appointments, or add a doctor yourself, not both.")]
        [TestCase(false, null, "Rao Clinic", "Enter the doctor's name.")]
        [TestCase(false, "Dr Rao", null, null)]
        [TestCase(true, null, null, null)]
        public void VisitIsAnAppointmentOrATypedInDoctor(bool withAppt, string? doctor, string? clinic, string? expected) =>
            Assert.That(PatientProfileRules.ValidateVisit(withAppt ? Guid.NewGuid() : null, doctor, clinic, null, null), Is.EqualTo(expected));

        [Test]
        public void AVisitCannotBeInTheFuture() =>
            Assert.That(PatientProfileRules.ValidateVisit(null, "Dr Rao", null, null, DateTime.UtcNow.AddDays(30)), Is.EqualTo("The visit date cannot be in the future."));

        [Test]
        public async Task UploadWithBothAnAppointmentAndATypedInDoctor_IsRefusedBeforeAnythingIsStored()
        {
            var handler = new UploadHealthLockerDocumentHandler(_context, _blob.Object, new ConfigurationBuilder().Build());
            var file = new Moq.Mock<Microsoft.AspNetCore.Http.IFormFile>().Object;

            var r = await handler.Handle(new UploadHealthLockerDocumentRequestModel { Mobile = Mine, File = file, FileName = "a.pdf", ApptId = Guid.NewGuid(), ExternalDoctorName = "Dr Rao" }, Ct);

            Assert.That(r.Success, Is.False);
            Assert.That(r.Message, Does.Contain("not both"));
            _blob.Verify(b => b.UploadAsync(It.IsAny<string>(), It.IsAny<Microsoft.AspNetCore.Http.IFormFile>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Test]
        public async Task ProfileKeepsFirstAndLastNameAndBloodGroup_AndStillAcceptsAFullName()
        {
            var handler = new UpdatePublicPatientProfileHandler(_context);
            var a = await handler.Handle(new UpdatePublicPatientProfileRequestModel { Mobile = Mine, FirstName = "Aquib", LastName = "Khan", BloodGroup = "ab-", GuardianName = "Abx" }, Ct);
            var b = await handler.Handle(new UpdatePublicPatientProfileRequestModel { Mobile = Other, FullName = "Zara Bano Ali" }, Ct);

            Assert.That((a.FullName, a.FirstName, a.LastName, a.BloodGroup, a.GuardianName), Is.EqualTo(("Aquib Khan", "Aquib", "Khan", "AB-", "Abx")));
            Assert.That((b.FirstName, b.LastName, b.FullName), Is.EqualTo(("Zara", "Bano Ali", "Zara Bano Ali")));
            Assert.That((await handler.Handle(new UpdatePublicPatientProfileRequestModel { Mobile = Mine, FirstName = "A", BloodGroup = "Z" }, Ct)).Success, Is.False);
        }
    }
}
