using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Implementations;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.Application.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    /// <summary>A doctor asks for a writer on a topic, and a hospital doctor is passed from EasyHMS to Doctor Dekho already signed in.</summary>
    [TestFixture]
    public class HealthWikiWriterRequestTests
    {
        private AppDbContext _context = null!;
        private RecordingNotifier _notifier = null!;
        private static CancellationToken Ct => CancellationToken.None;
        private long _seed = 9100000100;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _notifier = new RecordingNotifier();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private HealthWikiContributor Person(string type, string status = "VERIFIED", string name = "Person")
        {
            var c = new HealthWikiContributor
            {
                ContributorId = Guid.NewGuid(), Type = type, FullName = name, Mobile = type == "HOSPITAL_DOCTOR" ? null : (_seed++).ToString(), Status = status,
                Qualification = "MBBS", Speciality = "Cardiology", RegistrationNumber = "1", RegistrationCouncil = "Council", RoleTitle = "Writer",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            if (type == "HOSPITAL_DOCTOR") c.DoctorId = Guid.NewGuid();
            _context.HealthWikiContributors.Add(c);
            _context.SaveChanges();
            return c;
        }

        private CreateContributorTopicRequestModel Topic(Guid by, bool writer, string type = "MEDICAL") => new()
        {
            ContributorId = by, Title = "Managing blood pressure at home", Type = type, Outline = "Habits and checks.", WhyItMatters = "Common question.", RequestWriter = writer,
        };

        private async Task<Guid> Suggest(HealthWikiContributor by, bool writer, string type = "MEDICAL")
        {
            var res = await new HealthWikiTopicService(_context).CreateAsync(Topic(by.ContributorId, writer, type), Ct);
            Assert.That(res.Success, Is.True, res.Message);
            return res.Topic!.Id;
        }

        private Task<Application.ResponseModels.CommandResponseModels.DecideTopicResponseModel> Accept(Guid topicId, Guid? writer = null, bool contentTeam = false) =>
            new DecideTopicHandler(_context, _notifier).Handle(new DecideTopicRequestModel
            { TopicId = topicId, Action = DecideTopicRequestModel.Accept, ActorName = "Editor", WriterContributorId = writer, ContentTeam = contentTeam }, Ct);

        [Test]
        public async Task AskingForAWriter_IsKept_OnlyForADoctor()
        {
            var doctor = Person("INDEPENDENT_DOCTOR");
            var writer = Person("WRITER");
            var d = await Suggest(doctor, true);
            var w = await Suggest(writer, true, "SECTOR_UPDATE");

            Assert.That(_context.HealthWikiTopicRequests.Single(t => t.TopicId == d).RequestWriter, Is.True);
            Assert.That(_context.HealthWikiTopicRequests.Single(t => t.TopicId == w).RequestWriter, Is.False);
        }

        [Test]
        public async Task AcceptingATopicThatAskedForAWriter_NeedsAChoice()
        {
            var topic = await Suggest(Person("INDEPENDENT_DOCTOR"), true);

            var res = await Accept(topic);

            Assert.That(res.StatusCode, Is.EqualTo(400));
            Assert.That(_context.HealthWikiTopicRequests.Single().Status, Is.EqualTo("SUBMITTED"));
        }

        [Test]
        public async Task ContentTeam_WritesIt_AndTheDoctorBecomesTheReviewer_WithoutTheAcceptedMessage()
        {
            var doctor = Person("INDEPENDENT_DOCTOR", name: "Dr. Asha");
            var topic = await Suggest(doctor, true);

            var res = await Accept(topic, contentTeam: true);

            Assert.That(res.Success, Is.True, res.Message);
            var article = _context.HealthArticles.Single();
            Assert.That(article.AuthorContributorId, Is.Null);
            Assert.That(article.ReviewerContributorId, Is.EqualTo(doctor.ContributorId));
            Assert.That(article.Status, Is.EqualTo("DRAFT"));
            Assert.That(_context.HealthWikiTopicRequests.Single().Status, Is.EqualTo("ACCEPTED"));
            // "A draft has been created for you to write" would be untrue for this doctor
            Assert.That(_notifier.Of("N10"), Is.Empty);
        }

        [Test]
        public async Task AChosenDoctorCanWriteIt_AndTheSuggestingDoctorReviews()
        {
            var asks = Person("INDEPENDENT_DOCTOR", name: "Dr. Asha");
            var writerDoctor = Person("INDEPENDENT_DOCTOR", name: "Dr. Ravi");
            var topic = await Suggest(asks, true);

            var res = await Accept(topic, writerDoctor.ContributorId);

            Assert.That(res.Success, Is.True, res.Message);
            var article = _context.HealthArticles.Single();
            Assert.That(article.AuthorContributorId, Is.EqualTo(writerDoctor.ContributorId));
            Assert.That(article.ReviewerContributorId, Is.EqualTo(asks.ContributorId));
        }

        [Test]
        public async Task AWriterCannotBeChosenForAMedicalArticle()
        {
            var topic = await Suggest(Person("INDEPENDENT_DOCTOR"), true);
            var writer = Person("WRITER");

            var res = await Accept(topic, writer.ContributorId);

            Assert.That(res.StatusCode, Is.EqualTo(409));
            Assert.That(_context.HealthArticles.Any(), Is.False);
        }

        [Test]
        public async Task ASectorUpdateWrittenByAWriter_HasNoReviewer()
        {
            var asks = Person("INDEPENDENT_DOCTOR");
            var writer = Person("WRITER");
            var topic = await Suggest(asks, true, "SECTOR_UPDATE");

            var res = await Accept(topic, writer.ContributorId);

            Assert.That(res.Success, Is.True, res.Message);
            var article = _context.HealthArticles.Single();
            Assert.That(article.AuthorContributorId, Is.EqualTo(writer.ContributorId));
            Assert.That(article.ReviewerContributorId, Is.Null);
        }

        [Test]
        public async Task ATopicThatDidNotAskForAWriter_StillGoesToTheSuggester_AndTheyAreTold()
        {
            var doctor = Person("INDEPENDENT_DOCTOR");
            var topic = await Suggest(doctor, false);

            var res = await Accept(topic);

            Assert.That(res.Success, Is.True, res.Message);
            Assert.That(_context.HealthArticles.Single().AuthorContributorId, Is.EqualTo(doctor.ContributorId));
            Assert.That(_notifier.Of("N10").Count(), Is.EqualTo(1));
        }

        // ---- hand-off from EasyHMS

        [Test]
        public async Task AHospitalDoctor_IsPassedOver_ByACodeThatWorksOnce()
        {
            var doctor = Person("HOSPITAL_DOCTOR", "PENDING");
            var svc = new HealthWikiHandoffService(_context);

            var code = await svc.CreateAsync(doctor.ContributorId, Ct);
            var first = await svc.ExchangeAsync(code, Ct);
            var second = await svc.ExchangeAsync(code, Ct);

            Assert.That(code, Is.Not.Null);
            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Null);
            var session = _context.ContributorSessions.Single();
            Assert.That(session.ContributorId, Is.EqualTo(doctor.ContributorId));
            // only a hash of the code and of the token is kept
            Assert.That(_context.ContributorHandoffs.Single().TokenHash, Is.Not.EqualTo(code));
            Assert.That(session.TokenHash, Is.Not.EqualTo(first!.Token));
        }

        [Test]
        public async Task OnlyAHospitalDoctor_CanBePassedOver()
        {
            var svc = new HealthWikiHandoffService(_context);
            Assert.That(await svc.CreateAsync(Person("INDEPENDENT_DOCTOR").ContributorId, Ct), Is.Null);
            Assert.That(await svc.CreateAsync(Person("WRITER").ContributorId, Ct), Is.Null);
            Assert.That(await svc.CreateAsync(Person("HOSPITAL_DOCTOR", "REJECTED").ContributorId, Ct), Is.Null);
            Assert.That(_context.ContributorHandoffs.Any(), Is.False);
        }

        [Test]
        public async Task AnExpiredOrUnknownCode_ReadsTheSame_AndMakesNoSession()
        {
            var doctor = Person("HOSPITAL_DOCTOR", "PENDING");
            var svc = new HealthWikiHandoffService(_context);
            var code = await svc.CreateAsync(doctor.ContributorId, Ct);
            _context.ContributorHandoffs.Single().ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
            await _context.SaveChangesAsync(Ct);

            Assert.That(await svc.ExchangeAsync(code, Ct), Is.Null);
            Assert.That(await svc.ExchangeAsync("not-a-code", Ct), Is.Null);
            Assert.That(await svc.ExchangeAsync(null, Ct), Is.Null);
            Assert.That(_context.ContributorSessions.Any(), Is.False);
        }

        [Test]
        public async Task ADoctorRejectedAfterTheCodeWasMade_CannotUseIt()
        {
            var doctor = Person("HOSPITAL_DOCTOR", "PENDING");
            var svc = new HealthWikiHandoffService(_context);
            var code = await svc.CreateAsync(doctor.ContributorId, Ct);
            doctor.Status = "REJECTED";
            await _context.SaveChangesAsync(Ct);

            Assert.That(await svc.ExchangeAsync(code, Ct), Is.Null);
        }

        // ---- EasyHMS side

        private static IConfiguration Config(string? baseUrl) =>
            new ConfigurationBuilder().AddInMemoryCollection(new System.Collections.Generic.Dictionary<string, string?> { ["HealthWiki:ContributorBaseUrl"] = baseUrl }).Build();

        private Mock<IHealthWikiStaffService> StaffAs(HealthWikiContributor? c)
        {
            var staff = new Mock<IHealthWikiStaffService>();
            staff.Setup(x => x.EnsureContributorAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(c);
            return staff;
        }

        [Test]
        public async Task WriteAnArticle_ReturnsADoctorDekhoAddress_ThatSignsTheDoctorInOnce()
        {
            var doctor = Person("HOSPITAL_DOCTOR", "VERIFIED");
            var handler = new CreateHealthWikiWriteHandoffHandler(StaffAs(doctor).Object, new HealthWikiHandoffService(_context), Config("https://dd.example.com/"));

            var res = await handler.Handle(new CreateHealthWikiWriteHandoffRequestModel { UserId = Guid.NewGuid(), To = "drafts" }, Ct);

            Assert.That(res.Success, Is.True, res.Message);
            Assert.That(res.Url, Does.StartWith("https://dd.example.com/contribute/sso?code="));
            Assert.That(res.Url, Does.EndWith("&to=drafts"));
            // an unknown place to land falls back to the writing page
            var other = await handler.Handle(new CreateHealthWikiWriteHandoffRequestModel { UserId = Guid.NewGuid(), To = "https://evil.example.com" }, Ct);
            Assert.That(other.Url, Does.EndWith("&to=write"));
        }

        [Test]
        public async Task WriteAnArticle_RefusesANonDoctor_ARemovedDoctor_AndAMissingAddress()
        {
            var removed = Person("HOSPITAL_DOCTOR", "REJECTED");
            var doctor = Person("HOSPITAL_DOCTOR", "VERIFIED");

            var notDoctor = await new CreateHealthWikiWriteHandoffHandler(StaffAs(null).Object, new HealthWikiHandoffService(_context), Config("https://dd.example.com"))
                .Handle(new CreateHealthWikiWriteHandoffRequestModel(), Ct);
            var rejected = await new CreateHealthWikiWriteHandoffHandler(StaffAs(removed).Object, new HealthWikiHandoffService(_context), Config("https://dd.example.com"))
                .Handle(new CreateHealthWikiWriteHandoffRequestModel(), Ct);
            var noAddress = await new CreateHealthWikiWriteHandoffHandler(StaffAs(doctor).Object, new HealthWikiHandoffService(_context), Config(null))
                .Handle(new CreateHealthWikiWriteHandoffRequestModel(), Ct);

            Assert.That(notDoctor.StatusCode, Is.EqualTo(403));
            Assert.That(rejected.StatusCode, Is.EqualTo(403));
            Assert.That(noAddress.StatusCode, Is.EqualTo(503));
            Assert.That(_context.ContributorHandoffs.Any(), Is.False);
        }

        [Test]
        public async Task TheSsoExchange_GivesASession_AndAnExpiredCodeA401()
        {
            var doctor = Person("HOSPITAL_DOCTOR", "VERIFIED");
            var svc = new HealthWikiHandoffService(_context);
            var code = await svc.CreateAsync(doctor.ContributorId, Ct);
            var handler = new ContributorSsoExchangeHandler(svc);

            var ok = await handler.Handle(new ContributorSsoExchangeRequestModel { Code = code }, Ct);
            var again = await handler.Handle(new ContributorSsoExchangeRequestModel { Code = code }, Ct);

            Assert.That(ok.Success, Is.True);
            Assert.That(ok.SessionToken, Is.Not.Null.And.Not.Empty);
            Assert.That(ok.NeedsRegistration, Is.False);
            Assert.That(again.Success, Is.False);
            Assert.That(again.StatusCode, Is.EqualTo(401));
        }

        [Test]
        public async Task ADoctorInEasyHms_SuggestsATopic_AsAContributor_AndAsksForAWriter()
        {
            var doctor = Person("HOSPITAL_DOCTOR", "VERIFIED", "Dr. Hospital");
            var staff = StaffAs(doctor).Object;
            var topics = new HealthWikiTopicService(_context);

            var created = await new CreateHealthWikiStaffTopicHandler(staff, topics).Handle(new CreateHealthWikiStaffTopicRequestModel
            { UserId = Guid.NewGuid(), Title = "Sleep and the heart", Type = "MEDICAL", Outline = "How sleep affects BP.", WhyItMatters = "Patients ask.", RequestWriter = true }, Ct);
            var list = await new GetHealthWikiStaffTopicsHandler(staff, topics).Handle(new GetHealthWikiStaffTopicsRequestModel { UserId = Guid.NewGuid() }, Ct);

            Assert.That(created.Success, Is.True, created.Message);
            Assert.That(created.Topic!.RequestWriter, Is.True);
            Assert.That(list.Topics.Select(t => t.Title), Is.EqualTo(new[] { "Sleep and the heart" }));
            Assert.That(_context.HealthWikiTopicRequests.Single().ContributorId, Is.EqualTo(doctor.ContributorId));
        }

        [Test]
        public async Task ANonDoctor_CannotUseTheEasyHmsTopicRoutes()
        {
            var staff = StaffAs(null).Object;
            var topics = new HealthWikiTopicService(_context);

            var created = await new CreateHealthWikiStaffTopicHandler(staff, topics).Handle(new CreateHealthWikiStaffTopicRequestModel { Title = "x", Outline = "y", WhyItMatters = "z" }, Ct);
            var list = await new GetHealthWikiStaffTopicsHandler(staff, topics).Handle(new GetHealthWikiStaffTopicsRequestModel(), Ct);

            Assert.That(created.StatusCode, Is.EqualTo(403));
            Assert.That(list.StatusCode, Is.EqualTo(403));
        }

        // ---- the contributor tag and one-click entry from a patient's profile

        [TestCase("PENDING", true)]
        [TestCase("VERIFIED", true)]
        [TestCase("INVITED", false)]
        [TestCase("REJECTED", false)]
        public async Task OnlyAContributorWhoFinishedJoining_IsFoundByTheirVerifiedMobile(string status, bool found)
        {
            var person = Person("WRITER", status);
            var svc = new HealthWikiHandoffService(_context);

            Assert.That((await svc.FindByVerifiedMobileAsync(person.Mobile, Ct) != null), Is.EqualTo(found));
            Assert.That((await svc.FindByVerifiedMobileAsync("+91 " + person.Mobile, Ct) != null), Is.EqualTo(found), "a number with a country code finds the same person");
        }

        [Test]
        public async Task ANumberThatIsNotAContributor_OrIsMissing_IsNotFound()
        {
            var svc = new HealthWikiHandoffService(_context);
            Assert.That(await svc.FindByVerifiedMobileAsync("9000000000", Ct), Is.Null);
            Assert.That(await svc.FindByVerifiedMobileAsync(null, Ct), Is.Null);
            Assert.That(await svc.FindByVerifiedMobileAsync("abc", Ct), Is.Null);
        }

        [Test]
        public async Task TheProfileTag_SaysWhoTheyAre_WithoutTheirNumber()
        {
            var doctor = Person("INDEPENDENT_DOCTOR", "VERIFIED", "Dr. Asha");
            var handler = new GetContributorProfileTagHandler(new HealthWikiHandoffService(_context));

            var yes = await handler.Handle(new GetContributorProfileTagRequestModel { Mobile = doctor.Mobile }, Ct);
            var no = await handler.Handle(new GetContributorProfileTagRequestModel { Mobile = "9000000001" }, Ct);

            Assert.That(yes.IsContributor, Is.True);
            Assert.That((yes.Role, yes.Status, yes.FullName), Is.EqualTo(("INDEPENDENT_DOCTOR", "VERIFIED", "Dr. Asha")));
            Assert.That(no.IsContributor, Is.False);
            Assert.That(no.Role, Is.Null);
        }

        [Test]
        public async Task TheProfilePage_OpensTheContributorArea_WithoutASecondCode_AndOnlyForAContributor()
        {
            var writer = Person("WRITER", "PENDING");
            var handler = new StartContributorSessionFromPatientHandler(new HealthWikiHandoffService(_context));

            var ok = await handler.Handle(new StartContributorSessionFromPatientRequestModel { Mobile = writer.Mobile }, Ct);
            var none = await handler.Handle(new StartContributorSessionFromPatientRequestModel { Mobile = "9000000002" }, Ct);

            Assert.That(ok.Success, Is.True);
            Assert.That(ok.NeedsRegistration, Is.False);
            var session = _context.ContributorSessions.Single();
            Assert.That(session.ContributorId, Is.EqualTo(writer.ContributorId));
            Assert.That(session.TokenHash, Is.Not.EqualTo(ok.SessionToken), "only a hash is kept");
            Assert.That(none.Success, Is.False);
            Assert.That(none.StatusCode, Is.EqualTo(404));
        }
    }
}
