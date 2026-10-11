using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services.Implementations;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    /// <summary>Who is told what, and when: each real change must tell the right people, and a change that did not happen must tell nobody.</summary>
    [TestFixture]
    public class HealthWikiNotificationHookTests
    {
        private AppDbContext _context = null!;
        private RecordingNotifier _n = null!;
        private HealthArticleService _articles = null!;
        private HealthArticleReviewService _reviews = null!;
        private HealthWikiStaffService _staff = null!;
        private CancellationToken Ct => CancellationToken.None;
        private long _mobile = 9200000000;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _n = new RecordingNotifier();
            _articles = new HealthArticleService(_context, _n);
            _reviews = new HealthArticleReviewService(_context, _n);
            _staff = new HealthWikiStaffService(_context, _n);
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private HealthWikiContributor Person(string name, string type = "INDEPENDENT_DOCTOR", string status = "VERIFIED")
        {
            var c = new HealthWikiContributor
            {
                ContributorId = Guid.NewGuid(), Type = type, FullName = name, Mobile = (_mobile++).ToString(), Status = status,
                RegistrationNumber = "R" + _mobile, RegistrationCouncil = "MCI", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            _context.HealthWikiContributors.Add(c);
            _context.SaveChanges();
            return c;
        }

        private async Task<string> Draft(HealthWikiContributor author, string type = "MEDICAL", string title = "High blood pressure")
        {
            var res = await new SaveContributorArticleHandler(_context, _articles).Handle(new SaveContributorArticleRequestModel
            { ContributorId = author.ContributorId, Type = type, Title = title, Content = "## Body" }, Ct);
            Assert.That(res.Success, Is.True, res.Message);
            return res.Article!.Slug;
        }

        private Task<EasyHMSAPI.Application.ResponseModels.CommandResponseModels.SaveHealthArticleResponseModel> Cms(SaveHealthArticleRequestModel r) => _articles.SaveAsync(r, Ct);

        private async Task Submit(HealthWikiContributor author, string slug) =>
            Assert.That((await new SubmitContributorArticleHandler(_context, _n).Handle(new SubmitContributorArticleRequestModel { ContributorId = author.ContributorId, Slug = slug }, Ct)).Success, Is.True);

        // ------------------------------------------------------------------------------------------ people

        [Test]
        public async Task Approving_AProfile_TellsThePerson_N3()
        {
            var c = Person("Dr. Asha", status: "PENDING");
            var res = await new VerifyContributorHandler(_context, _staff, _n).Handle(new VerifyContributorRequestModel { ContributorId = c.ContributorId }, Ct);

            Assert.That(res.Success, Is.True, res.Message);
            Assert.That(_n.Of("N3").Single().Person, Is.EqualTo(c.ContributorId));
        }

        [Test]
        public async Task ARefusedApproval_TellsNobody()
        {
            var invited = Person("Not registered yet", status: "INVITED");
            var res = await new VerifyContributorHandler(_context, _staff, _n).Handle(new VerifyContributorRequestModel { ContributorId = invited.ContributorId }, Ct);

            Assert.That(res.StatusCode, Is.EqualTo(409));
            Assert.That(_n.Calls, Is.Empty);
        }

        [Test]
        public async Task Verifying_ADoctorWhoHadApprovedAnArticleEarly_AlsoTellsTheAuthorAndReviewer_N8()
        {
            var author = Person("Priya"); var reviewer = Person("Dr. New", status: "PENDING");
            var slug = await Draft(author);
            await Cms(new SaveHealthArticleRequestModel { Slug = slug, ReviewerContributorId = reviewer.ContributorId, Status = "IN_REVIEW" });
            await _reviews.ReviewAsync(reviewer.ContributorId, slug, "APPROVE", null, true, Ct); // approved, but not verified yet, so not live
            _n.Calls.Clear();

            await new VerifyContributorHandler(_context, _staff, _n).Handle(new VerifyContributorRequestModel { ContributorId = reviewer.ContributorId }, Ct);

            Assert.That(_n.Of("N8").Select(t => t.Person), Is.EquivalentTo(new Guid?[] { author.ContributorId, reviewer.ContributorId }));
            Assert.That(_n.Of("N3"), Has.Exactly(1).Items);
        }

        [Test]
        public async Task Rejecting_AProfile_TellsThePersonWhy_N4_ButNeedsAReasonFirst()
        {
            var c = Person("Dr. Asha", status: "PENDING");
            var h = new RejectContributorHandler(_context, _n);

            await h.Handle(new RejectContributorRequestModel { ContributorId = c.ContributorId }, Ct);
            Assert.That(_n.Calls, Is.Empty, "no reason, no change, no message");

            await h.Handle(new RejectContributorRequestModel { ContributorId = c.ContributorId, Reason = "Registration not found." }, Ct);
            var told = _n.Of("N4").Single();
            Assert.That(told.Person, Is.EqualTo(c.ContributorId));
            Assert.That(told.Extra, Is.EqualTo("Registration not found."));
        }

        [Test]
        public async Task TheFirstRegistration_SaysThankYou_N15_ButChangingTheBioLaterDoesNot()
        {
            var c = Person("Dr. Asha", status: "INVITED");
            var h = new RegisterContributorHandler(_context, _n);
            var input = new RegisterContributorRequestModel
            {
                ContributorId = c.ContributorId, FullName = "Dr. Asha Rao", Speciality = "GP", Qualification = "MBBS", RegistrationNumber = "R1",
                RegistrationCouncil = "MCI", RegistrationYear = "2012", Consent = true,
            };

            await h.Handle(input, Ct);
            Assert.That(_n.Of("N15"), Has.Exactly(1).Items);

            await h.Handle(input, Ct);
            Assert.That(_n.Of("N15"), Has.Exactly(1).Items, "still one: it is not the first registration any more");
        }

        [Test]
        public async Task ABadRegistration_SaysNothing()
        {
            var c = Person("Dr. Asha", status: "INVITED");
            await new RegisterContributorHandler(_context, _n).Handle(new RegisterContributorRequestModel { ContributorId = c.ContributorId, FullName = "" }, Ct);
            Assert.That(_n.Calls, Is.Empty);
        }

        // ------------------------------------------------------------------------------------------ the doctor is told an article is waiting (N5)

        [Test]
        public async Task SubmittingAMedicalArticleThatHasADoctor_TellsTheDoctor_N5()
        {
            var author = Person("Priya"); var doctor = Person("Dr. Meera");
            var slug = await Draft(author);
            _context.HealthArticles.Single().ReviewerContributorId = doctor.ContributorId;
            await _context.SaveChangesAsync();

            await Submit(author, slug);

            var told = _n.Of("N5").Single();
            Assert.That(told.Person, Is.EqualTo(doctor.ContributorId));
            Assert.That(told.Slug, Is.EqualTo(slug));
            Assert.That(told.Extra, Is.EqualTo("High blood pressure"));
        }

        [Test]
        public async Task WithoutADoctorYet_NobodyIsTold_UntilTheTeamAssignsOne()
        {
            var author = Person("Priya"); var doctor = Person("Dr. Meera");
            var slug = await Draft(author);

            await Submit(author, slug);
            Assert.That(_n.Calls, Is.Empty, "waiting for the team to choose a doctor");

            await Cms(new SaveHealthArticleRequestModel { Slug = slug, ReviewerContributorId = doctor.ContributorId });
            Assert.That(_n.Of("N5").Single().Person, Is.EqualTo(doctor.ContributorId), "told the moment the doctor is assigned");
        }

        [Test]
        public async Task ChangingTheDoctorWhileItWaits_TellsTheNewOne_AndASectorUpdateTellsNoDoctor()
        {
            var author = Person("Priya"); var first = Person("Dr. One"); var second = Person("Dr. Two");
            var slug = await Draft(author);
            await Cms(new SaveHealthArticleRequestModel { Slug = slug, ReviewerContributorId = first.ContributorId, Status = "IN_REVIEW" });
            Assert.That(_n.Of("N5").Single().Person, Is.EqualTo(first.ContributorId));

            await Cms(new SaveHealthArticleRequestModel { Slug = slug, ReviewerContributorId = second.ContributorId });
            Assert.That(_n.Of("N5").Select(t => t.Person), Is.EqualTo(new Guid?[] { first.ContributorId, second.ContributorId }));

            _n.Calls.Clear();
            var sector = await Draft(author, "SECTOR_UPDATE", "Telemedicine rules");
            await Submit(author, sector);
            Assert.That(_n.Calls, Is.Empty);
        }

        [Test]
        public async Task SavingWithoutAnyChange_OrAFailedSave_TellsNobody()
        {
            var author = Person("Priya"); var doctor = Person("Dr. Meera");
            var slug = await Draft(author);
            await Cms(new SaveHealthArticleRequestModel { Slug = slug, ReviewerContributorId = doctor.ContributorId, Status = "IN_REVIEW" });
            _n.Calls.Clear();

            await Cms(new SaveHealthArticleRequestModel { Slug = slug, Description = "Only a description changed" });
            var bad = await Cms(new SaveHealthArticleRequestModel { Slug = slug, Status = "PUBLISHED" }); // a medical article cannot be published like this

            Assert.That(bad.StatusCode, Is.EqualTo(400));
            Assert.That(_n.Calls, Is.Empty);
        }

        [Test]
        public async Task AnEditToALiveArticleSentForReview_TellsTheDoctor_WithTheNewTitle()
        {
            var author = Person("Priya"); var doctor = Person("Dr. Meera");
            var slug = await Draft(author, title: "Old title");
            await Cms(new SaveHealthArticleRequestModel { Slug = slug, ReviewerContributorId = doctor.ContributorId, Status = "IN_REVIEW" });
            await _reviews.ReviewAsync(doctor.ContributorId, slug, "APPROVE", null, true, Ct); // live
            _n.Calls.Clear();

            await Cms(new SaveHealthArticleRequestModel { Slug = slug, Title = "New title" });
            Assert.That(_n.Calls, Is.Empty, "a draft edit is not sent yet");
            await Cms(new SaveHealthArticleRequestModel { Slug = slug, Status = "IN_REVIEW" });

            Assert.That(_n.Of("N5").Single().Extra, Is.EqualTo("New title"));
        }

        // ------------------------------------------------------------------------------------------ the decision

        [Test]
        public async Task ADoctorAskingForChanges_TellsTheAuthor_N7_WithTheirOwnWords()
        {
            var author = Person("Priya"); var doctor = Person("Dr. Meera Nair");
            var slug = await Draft(author);
            await Cms(new SaveHealthArticleRequestModel { Slug = slug, ReviewerContributorId = doctor.ContributorId, Status = "IN_REVIEW" });
            _n.Calls.Clear();

            await _reviews.ReviewAsync(doctor.ContributorId, slug, "REQUEST_CHANGES", "Add when to see a doctor.", false, Ct);

            var told = _n.Of("N7").Single();
            Assert.That(told.Person, Is.EqualTo(author.ContributorId));
            Assert.That(told.Extra, Is.EqualTo("Dr. Meera Nair|Add when to see a doctor."));
            Assert.That(told.ByEditor, Is.False);
        }

        [Test]
        public async Task ApprovalByAVerifiedDoctor_TellsTheAuthorAndTheDoctor_N8()
        {
            var author = Person("Priya"); var doctor = Person("Dr. Meera Nair");
            var slug = await Draft(author);
            await Cms(new SaveHealthArticleRequestModel { Slug = slug, ReviewerContributorId = doctor.ContributorId, Status = "IN_REVIEW" });
            _n.Calls.Clear();

            var res = await _reviews.ReviewAsync(doctor.ContributorId, slug, "APPROVE", null, true, Ct);

            Assert.That(res.Outcome, Is.EqualTo("PUBLISHED"));
            Assert.That(_n.Of("N8").Select(t => t.Person), Is.EquivalentTo(new Guid?[] { author.ContributorId, doctor.ContributorId }));
        }

        [Test]
        public async Task ApprovalByADoctorStillBeingVerified_TellsNobodyYet_BecauseItIsNotLive()
        {
            var author = Person("Priya"); var doctor = Person("Dr. New", status: "PENDING");
            var slug = await Draft(author);
            await Cms(new SaveHealthArticleRequestModel { Slug = slug, ReviewerContributorId = doctor.ContributorId, Status = "IN_REVIEW" });
            _n.Calls.Clear();

            var res = await _reviews.ReviewAsync(doctor.ContributorId, slug, "APPROVE", null, true, Ct);

            Assert.That(res.Outcome, Is.EqualTo("APPROVED_AWAITING_VERIFICATION"));
            Assert.That(_n.Calls, Is.Empty);
        }

        [Test]
        public async Task ARefusedDecision_TellsNobody()
        {
            var author = Person("Priya"); var doctor = Person("Dr. Meera");
            var slug = await Draft(author);
            await Cms(new SaveHealthArticleRequestModel { Slug = slug, ReviewerContributorId = doctor.ContributorId, Status = "IN_REVIEW" });
            _n.Calls.Clear();

            await _reviews.ReviewAsync(doctor.ContributorId, slug, "APPROVE", null, false, Ct);          // no accuracy confirmation
            await _reviews.ReviewAsync(doctor.ContributorId, slug, "REQUEST_CHANGES", "short", false, Ct); // comment too short
            await _reviews.ReviewAsync(author.ContributorId, slug, "APPROVE", null, true, Ct);            // not the reviewer

            Assert.That(_n.Calls, Is.Empty);
        }

        [Test]
        public async Task WhenAHospitalDoctorGetsVerifiedInTheCms_TheirWaitingArticleGoesLive_AndTheyAreTold()
        {
            var userId = Guid.NewGuid(); var doctorId = Guid.NewGuid();
            _context.Doctors.Add(new Doctor { DoctorID = doctorId, UserID = userId, LicenseNumber = "L-48213", MedicalCouncil = "Delhi Medical Council", RegistrationYear = 2010 });
            _context.UserProfiles.Add(new UserProfile { UserProfileID = Guid.NewGuid(), UserID = userId, FullName = "Dr. Hospital" });
            await _context.SaveChangesAsync();
            var hospitalDoctor = (await _staff.EnsureContributorAsync(userId, Ct))!;
            var author = Person("Priya");
            var slug = await Draft(author);
            await Cms(new SaveHealthArticleRequestModel { Slug = slug, ReviewerContributorId = hospitalDoctor.ContributorId, Status = "IN_REVIEW" });
            await _reviews.ReviewAsync(hospitalDoctor.ContributorId, slug, "APPROVE", null, true, Ct); // approved, registration not yet marked by the CMS
            _n.Calls.Clear();

            _context.Doctors.Single().IsRegistrationVerified = true;   // what a CMS admin does after the NMC register check
            await _context.SaveChangesAsync();
            await _staff.SyncHospitalDoctorsAsync(null, Ct);

            Assert.That(_n.Of("N8").Select(t => t.Person), Is.EquivalentTo(new Guid?[] { author.ContributorId, hospitalDoctor.ContributorId }));
        }

        // ------------------------------------------------------------------------------------------ the editorial team

        [Test]
        public async Task ApprovingASectorUpdate_TellsTheAuthor_N8_AndSendingItBack_IsN9()
        {
            var author = Person("Priya", "WRITER");
            var slug = await Draft(author, "SECTOR_UPDATE", "Telemedicine rules");
            await Cms(new SaveHealthArticleRequestModel { Slug = slug, Status = "IN_REVIEW" });
            var decide = new DecideHealthArticleHandler(_context, _n);

            await decide.Handle(new DecideHealthArticleRequestModel { Slug = slug, Action = "WITHDRAW", Reason = "More sources, please." }, Ct);
            var back = _n.Of("N9").Single();
            Assert.That(back.Person, Is.EqualTo(author.ContributorId));
            Assert.That(back.Extra, Is.EqualTo("the NexEagle editorial team|More sources, please."));
            Assert.That(back.ByEditor, Is.True);

            await Cms(new SaveHealthArticleRequestModel { Slug = slug, Status = "IN_REVIEW" });
            _n.Calls.Clear();
            await decide.Handle(new DecideHealthArticleRequestModel { Slug = slug, Action = "APPROVE" }, Ct);
            Assert.That(_n.Of("N8").Single().Person, Is.EqualTo(author.ContributorId));
        }

        [Test]
        public async Task TakingAnArticleDown_TellsTheAuthorAndTheDoctor_N14_WithTheReason_ButNeedsAReason()
        {
            var author = Person("Priya"); var doctor = Person("Dr. Meera");
            var slug = await Draft(author);
            await Cms(new SaveHealthArticleRequestModel { Slug = slug, ReviewerContributorId = doctor.ContributorId, Status = "IN_REVIEW" });
            await _reviews.ReviewAsync(doctor.ContributorId, slug, "APPROVE", null, true, Ct);
            _n.Calls.Clear();
            var decide = new DecideHealthArticleHandler(_context, _n);

            await decide.Handle(new DecideHealthArticleRequestModel { Slug = slug, Action = "UNPUBLISH" }, Ct);
            Assert.That(_n.Calls, Is.Empty, "no reason, so nothing happened and nobody is told");

            await decide.Handle(new DecideHealthArticleRequestModel { Slug = slug, Action = "UNPUBLISH", Reason = "Wrong dosage." }, Ct);
            Assert.That(_n.Of("N14").Select(t => t.Person), Is.EquivalentTo(new Guid?[] { author.ContributorId, doctor.ContributorId }));
            Assert.That(_n.Of("N14").Select(t => t.Extra).Distinct(), Is.EqualTo(new[] { "Wrong dosage." }));
        }

        [Test]
        public async Task PublishingASectorUpdateDirectly_TellsTheAuthor_N8()
        {
            var author = Person("Priya", "WRITER");
            var slug = await Draft(author, "SECTOR_UPDATE", "Telemedicine rules");

            await Cms(new SaveHealthArticleRequestModel { Slug = slug, Status = "PUBLISHED" });

            Assert.That(_n.Of("N8").Single().Person, Is.EqualTo(author.ContributorId));
        }

        // ------------------------------------------------------------------------------------------ topics

        [Test]
        public async Task EachTopicDecision_TellsTheContributor_N10_N11_N12()
        {
            var c = Person("Dr. Asha");
            var create = new CreateContributorTopicHandler(_context);
            var accept = (await create.Handle(new CreateContributorTopicRequestModel { ContributorId = c.ContributorId, Title = "Silent heart attack", Outline = "o", WhyItMatters = "w" }, Ct)).Topic!;
            var decline = (await create.Handle(new CreateContributorTopicRequestModel { ContributorId = c.ContributorId, Title = "Off topic", Outline = "o", WhyItMatters = "w" }, Ct)).Topic!;
            var ask = (await create.Handle(new CreateContributorTopicRequestModel { ContributorId = c.ContributorId, Title = "Vague", Outline = "o", WhyItMatters = "w" }, Ct)).Topic!;
            var decide = new DecideTopicHandler(_context, _n);

            await decide.Handle(new DecideTopicRequestModel { TopicId = accept.Id, Action = "ACCEPT" }, Ct);
            await decide.Handle(new DecideTopicRequestModel { TopicId = decline.Id, Action = "DECLINE", Note = "Out of scope." }, Ct);
            await decide.Handle(new DecideTopicRequestModel { TopicId = ask.Id, Action = "ASK_DETAIL", Note = "Which age group?" }, Ct);

            Assert.That(_n.Of("N10").Single().Slug, Is.EqualTo("silent-heart-attack"));
            Assert.That(_n.Of("N11").Single().Extra, Is.EqualTo("Out of scope."));
            Assert.That(_n.Of("N12").Single().Extra, Is.EqualTo("Which age group?"));
            Assert.That(_n.Calls.Select(t => t.Person).Distinct(), Is.EqualTo(new Guid?[] { c.ContributorId }));
        }

        [Test]
        public async Task ARefusedTopicDecision_TellsNobody()
        {
            var c = Person("Dr. Asha");
            var topic = (await new CreateContributorTopicHandler(_context).Handle(new CreateContributorTopicRequestModel { ContributorId = c.ContributorId, Title = "T", Outline = "o", WhyItMatters = "w" }, Ct)).Topic!;

            await new DecideTopicHandler(_context, _n).Handle(new DecideTopicRequestModel { TopicId = topic.Id, Action = "DECLINE" }, Ct); // no reason

            Assert.That(_n.Calls, Is.Empty);
        }
    }
}
