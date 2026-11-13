using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.EntityFrameworkCore;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    // The WHO checklist used to be a rubber stamp: any posted dictionary (even empty) marked a phase complete, a completed
    // phase could be silently rewritten, and phases could be recorded on a cancelled case or at the wrong stage.
    [TestFixture]
    public class SurgicalSafetyChecklistTests
    {
        private AppDbContext _context = null!;
        private SurgicalSafetyChecklistCommandHandlers _handler = null!;
        private Guid _hospitalId;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new SurgicalSafetyChecklistCommandHandlers(_context);
            _hospitalId = Guid.NewGuid();
        }

        [TearDown]
        public void TearDown()
        {
            InMemoryDbContextFactory.Destroy(_context);
            _context.Dispose();
        }

        private SurgeryCase SeedCase(string status)
        {
            var c = new SurgeryCase
            {
                SurgeryCaseId = Guid.NewGuid(), HospitalId = _hospitalId, AdmissionId = Guid.NewGuid(), ProcedureName = "Appendicectomy",
                SurgeryType = "ELECTIVE", Urgency = "ROUTINE", StatusCode = status, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            };
            _context.SurgeryCase.Add(c);
            _context.SaveChanges();
            return c;
        }

        private void MoveTo(SurgeryCase c, string status) { c.StatusCode = status; _context.SaveChanges(); }

        private static Dictionary<string, bool> All(IReadOnlyList<(string Key, string Label)> items, bool value = true) => items.ToDictionary(i => i.Key, _ => value);

        private Task<EasyHMSAPI.Application.ResponseModels.CommandResponseModels.RecordSignInResponseModel> SignIn(SurgeryCase c, Dictionary<string, bool>? items = null, string by = "Nurse A") =>
            _handler.Handle(new RecordSignInRequestModel { HospitalId = _hospitalId, SurgeryCaseId = c.SurgeryCaseId, Items = items ?? All(IpdConstants.WhoChecklistItems.SignIn), LoggedInUserName = by }, CancellationToken.None);

        private Task<EasyHMSAPI.Application.ResponseModels.CommandResponseModels.RecordTimeOutResponseModel> TimeOut(SurgeryCase c, Dictionary<string, bool>? items = null) =>
            _handler.Handle(new RecordTimeOutRequestModel { HospitalId = _hospitalId, SurgeryCaseId = c.SurgeryCaseId, Items = items ?? All(IpdConstants.WhoChecklistItems.TimeOut), LoggedInUserName = "Surgeon" }, CancellationToken.None);

        private Task<EasyHMSAPI.Application.ResponseModels.CommandResponseModels.RecordSignOutResponseModel> SignOut(SurgeryCase c, Dictionary<string, bool>? items = null) =>
            _handler.Handle(new RecordSignOutRequestModel { HospitalId = _hospitalId, SurgeryCaseId = c.SurgeryCaseId, Items = items ?? All(IpdConstants.WhoChecklistItems.SignOut), LoggedInUserName = "Nurse B" }, CancellationToken.None);

        [Test]
        public async Task FullSequence_WithEveryItemConfirmed_Succeeds()
        {
            var c = SeedCase(IpdConstants.SurgeryStatus.PreOp);
            Assert.That((await SignIn(c)).Success, Is.True);

            MoveTo(c, IpdConstants.SurgeryStatus.InTheatre);
            Assert.That((await TimeOut(c)).Success, Is.True);
            Assert.That((await SignOut(c)).Success, Is.True);

            var saved = _context.SurgicalSafetyChecklist.Single();
            Assert.That(saved.SignInCompletedAt, Is.Not.Null);
            Assert.That(saved.TimeOutCompletedAt, Is.Not.Null);
            Assert.That(saved.SignOutCompletedAt, Is.Not.Null);
            Assert.That(saved.SignOutCompletedBy, Is.EqualTo("Nurse B"));
        }

        [Test]
        public async Task EmptyItems_AreRefused_AndNothingIsStamped()
        {
            var c = SeedCase(IpdConstants.SurgeryStatus.PreOp);
            var response = await SignIn(c, new Dictionary<string, bool>());

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("every item"));
            Assert.That(_context.SurgicalSafetyChecklist.Any(x => x.SignInCompletedAt != null), Is.False);
        }

        [Test]
        public async Task OneUntickedItem_IsRefused_AndNamesIt()
        {
            var c = SeedCase(IpdConstants.SurgeryStatus.PreOp);
            var items = All(IpdConstants.WhoChecklistItems.SignIn);
            items["pulse_oximeter"] = false;

            var response = await SignIn(c, items);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("Pulse oximeter"));
        }

        [Test]
        public async Task MissingItem_IsRefused()
        {
            var c = SeedCase(IpdConstants.SurgeryStatus.PreOp);
            var items = All(IpdConstants.WhoChecklistItems.SignIn);
            items.Remove("site_marked");
            Assert.That((await SignIn(c, items)).Success, Is.False);
        }

        [Test]
        public async Task UnknownItemKey_IsRefused()
        {
            var c = SeedCase(IpdConstants.SurgeryStatus.PreOp);
            var items = All(IpdConstants.WhoChecklistItems.SignIn);
            items["made_up_item"] = true;

            var response = await SignIn(c, items);

            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("made_up_item"));
        }

        [Test]
        public async Task ItemsFromTheWrongPhase_AreRefused()
        {
            var c = SeedCase(IpdConstants.SurgeryStatus.PreOp);
            Assert.That((await SignIn(c, All(IpdConstants.WhoChecklistItems.TimeOut))).Success, Is.False);
        }

        [Test]
        public async Task CompletedPhase_CannotBeOverwritten()
        {
            var c = SeedCase(IpdConstants.SurgeryStatus.PreOp);
            await SignIn(c, by: "Nurse A");
            var before = _context.SurgicalSafetyChecklist.Single().SignInCompletedAt;

            var again = await SignIn(c, by: "Nurse Z");

            Assert.That(again.Success, Is.False);
            Assert.That(again.Message, Does.Contain("already completed by Nurse A"));
            var saved = _context.SurgicalSafetyChecklist.Single();
            Assert.That(saved.SignInCompletedBy, Is.EqualTo("Nurse A"));
            Assert.That(saved.SignInCompletedAt, Is.EqualTo(before));
        }

        [Test]
        public async Task TimeOut_NeedsSignIn_AndSignOut_NeedsTimeOut()
        {
            var c = SeedCase(IpdConstants.SurgeryStatus.InTheatre);
            Assert.That((await TimeOut(c)).Message, Does.Contain("Sign-In must be completed"));

            MoveTo(c, IpdConstants.SurgeryStatus.PreOp);
            await SignIn(c);
            MoveTo(c, IpdConstants.SurgeryStatus.InTheatre);
            Assert.That((await SignOut(c)).Message, Does.Contain("Time-Out must be completed"));
        }

        [TestCase(IpdConstants.SurgeryStatus.Requested)]
        [TestCase(IpdConstants.SurgeryStatus.InTheatre)]
        [TestCase(IpdConstants.SurgeryStatus.PostOp)]
        [TestCase(IpdConstants.SurgeryStatus.Completed)]
        [TestCase(IpdConstants.SurgeryStatus.Cancelled)]
        public async Task SignIn_OutsideScheduledOrPreOp_IsRefused(string status)
        {
            var c = SeedCase(status);
            var response = await SignIn(c);
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("scheduled or in pre-op"));
        }

        [TestCase(IpdConstants.SurgeryStatus.PreOp)]
        [TestCase(IpdConstants.SurgeryStatus.PostOp)]
        [TestCase(IpdConstants.SurgeryStatus.Cancelled)]
        public async Task TimeOutAndSignOut_OutsideTheatre_AreRefused(string status)
        {
            var c = SeedCase(IpdConstants.SurgeryStatus.PreOp);
            await SignIn(c);
            MoveTo(c, status);

            var t = await TimeOut(c);
            Assert.That(t.Success, Is.False);
            Assert.That(t.Message, Does.Contain("in theatre"));
            Assert.That((await SignOut(c)).Success, Is.False);
        }

        [Test]
        public async Task SurgeryCaseFromAnotherHospital_IsNotFound()
        {
            var c = SeedCase(IpdConstants.SurgeryStatus.PreOp);
            var response = await _handler.Handle(new RecordSignInRequestModel
            {
                HospitalId = Guid.NewGuid(), SurgeryCaseId = c.SurgeryCaseId, Items = All(IpdConstants.WhoChecklistItems.SignIn),
            }, CancellationToken.None);
            Assert.That(response.Success, Is.False);
            Assert.That(response.Message, Does.Contain("not found"));
        }

        [Test]
        public async Task Addendum_IsAppended_AndLeavesItemsAndSignOffUntouched()
        {
            var c = SeedCase(IpdConstants.SurgeryStatus.PreOp);
            await _handler.Handle(new RecordSignInRequestModel { HospitalId = _hospitalId, SurgeryCaseId = c.SurgeryCaseId, Items = All(IpdConstants.WhoChecklistItems.SignIn), Notes = "original note", LoggedInUserName = "Nurse A" }, CancellationToken.None);
            var before = _context.SurgicalSafetyChecklist.AsNoTracking().Single();

            var response = await _handler.Handle(new AddChecklistAddendumRequestModel
            {
                HospitalId = _hospitalId, SurgeryCaseId = c.SurgeryCaseId, Phase = "sign-in", Text = "Allergy to latex was found later", LoggedInUserName = "Dr S",
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True, response.Message);
            var after = _context.SurgicalSafetyChecklist.Single();
            Assert.That(after.SignInNotes, Does.StartWith("original note"));
            Assert.That(after.SignInNotes, Does.Contain("Dr S").And.Contain("latex"));
            Assert.That(after.SignInItemsJson, Is.EqualTo(before.SignInItemsJson));
            Assert.That(after.SignInCompletedBy, Is.EqualTo("Nurse A"));
            Assert.That(after.SignInCompletedAt, Is.EqualTo(before.SignInCompletedAt));
        }

        [Test]
        public async Task Addendum_RequiresACompletedPhase_AndRealText()
        {
            var c = SeedCase(IpdConstants.SurgeryStatus.PreOp);
            var notDone = await _handler.Handle(new AddChecklistAddendumRequestModel { HospitalId = _hospitalId, SurgeryCaseId = c.SurgeryCaseId, Phase = "TimeOut", Text = "something long enough" }, CancellationToken.None);
            Assert.That(notDone.Success, Is.False);

            await SignIn(c);
            var tooShort = await _handler.Handle(new AddChecklistAddendumRequestModel { HospitalId = _hospitalId, SurgeryCaseId = c.SurgeryCaseId, Phase = "SignIn", Text = "no" }, CancellationToken.None);
            Assert.That(tooShort.Success, Is.False);

            var badPhase = await _handler.Handle(new AddChecklistAddendumRequestModel { HospitalId = _hospitalId, SurgeryCaseId = c.SurgeryCaseId, Phase = "Whatever", Text = "something long enough" }, CancellationToken.None);
            Assert.That(badPhase.Success, Is.False);
        }
    }
}
