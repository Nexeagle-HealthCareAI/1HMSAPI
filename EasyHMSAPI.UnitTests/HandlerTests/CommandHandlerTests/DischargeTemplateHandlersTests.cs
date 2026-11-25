using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.UnitTests.TestUtils;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    [TestFixture]
    public class DischargeTemplateHandlersTests
    {
        private AppDbContext _context = null!;
        private DischargeTemplateHandlers _handler = null!;
        private readonly Guid _hospitalId = Guid.NewGuid();
        private readonly Guid _userId = Guid.NewGuid();
        private const string Content = "{\"sections\":{\"dietInstructions\":\"Soft diet\"},\"medications\":[]}";

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new DischargeTemplateHandlers(_context);
        }

        [TearDown]
        public void TearDown() => _context.Dispose();

        private Task<Application.ResponseModels.CommandResponseModels.SaveDischargeTemplateResponseModel> Save(string? name, string? content = Content, Guid? id = null, bool replace = false, Guid? hospitalId = null) =>
            _handler.Handle(new SaveDischargeTemplateRequestModel { HospitalId = hospitalId ?? _hospitalId, Name = name, ContentJson = content, DischargeTemplateId = id, ReplaceExisting = replace, CallerUserId = _userId }, CancellationToken.None);

        private async Task<System.Collections.Generic.List<Application.ResponseModels.CommandResponseModels.DischargeTemplateItem>> List(Guid? hospitalId = null) =>
            (await _handler.Handle(new GetDischargeTemplatesRequestModel { HospitalId = hospitalId ?? _hospitalId }, CancellationToken.None)).Templates;

        [Test]
        public async Task Saves_a_template_and_lists_it()
        {
            var saved = await Save("  Normal   delivery ");
            Assert.That(saved.Success, Is.True);
            Assert.That(saved.Template!.Name, Is.EqualTo("Normal delivery"));

            var list = await List();
            Assert.That(list.Select(t => t.Name), Is.EqualTo(new[] { "Normal delivery" }));
            Assert.That(list[0].ContentJson, Is.EqualTo(Content));
            Assert.That(_context.DischargeTemplate.Single().CreatedByUserId, Is.EqualTo(_userId));
        }

        [Test]
        public async Task Refuses_a_missing_name_or_empty_content()
        {
            Assert.That((await Save("   ")).Success, Is.False);
            Assert.That((await Save("Dengue", "")).Success, Is.False);
            Assert.That((await Save("Dengue", "{}")).Success, Is.False);
            Assert.That((await Save("Dengue", "not json")).Success, Is.False);
            Assert.That((await Save("Dengue", "[1,2]")).Success, Is.False);
            Assert.That((await Save(new string('x', 201))).Success, Is.False);
            Assert.That(_context.DischargeTemplate.Count(), Is.EqualTo(0));
        }

        [Test]
        public async Task A_name_already_in_use_is_refused_until_the_user_chooses_to_replace()
        {
            var first = await Save("Dengue");
            var second = await Save("dengue", "{\"sections\":{\"dietInstructions\":\"Plenty of fluids\"}}");
            Assert.That(second.Success, Is.False);
            Assert.That(second.NameTaken, Is.True);

            var replaced = await Save("dengue", "{\"sections\":{\"dietInstructions\":\"Plenty of fluids\"}}", replace: true);
            Assert.That(replaced.Success, Is.True);
            Assert.That(replaced.Template!.DischargeTemplateId, Is.EqualTo(first.Template!.DischargeTemplateId));
            Assert.That((await List()).Single().ContentJson, Does.Contain("Plenty of fluids"));
        }

        [Test]
        public async Task Renaming_onto_another_templates_name_is_refused()
        {
            await Save("Dengue");
            var other = await Save("Malaria");
            var renamed = await Save("Dengue", id: other.Template!.DischargeTemplateId);
            Assert.That(renamed.Success, Is.False);
            Assert.That(renamed.NameTaken, Is.True);
            Assert.That((await Save("Malaria (adult)", id: other.Template.DischargeTemplateId)).Success, Is.True);
        }

        [Test]
        public async Task One_hospital_never_sees_or_changes_anothers_templates()
        {
            var mine = await Save("Dengue");
            var otherHospital = Guid.NewGuid();

            Assert.That(await List(otherHospital), Is.Empty);
            // The same name is free in another hospital.
            Assert.That((await Save("Dengue", hospitalId: otherHospital)).Success, Is.True);
            // Another hospital cannot edit, delete or count a use against this one.
            Assert.That((await Save("Hijacked", id: mine.Template!.DischargeTemplateId, hospitalId: otherHospital)).Success, Is.False);
            Assert.That((await _handler.Handle(new DeleteDischargeTemplateRequestModel { HospitalId = otherHospital, DischargeTemplateId = mine.Template.DischargeTemplateId }, CancellationToken.None)).Success, Is.False);
            Assert.That((await _handler.Handle(new MarkDischargeTemplateUsedRequestModel { HospitalId = otherHospital, DischargeTemplateId = mine.Template.DischargeTemplateId }, CancellationToken.None)).Success, Is.False);
            Assert.That((await List()).Single().Name, Is.EqualTo("Dengue"));
        }

        [Test]
        public async Task Deleting_hides_the_template_and_frees_its_name()
        {
            var saved = await Save("Dengue");
            var deleted = await _handler.Handle(new DeleteDischargeTemplateRequestModel { HospitalId = _hospitalId, DischargeTemplateId = saved.Template!.DischargeTemplateId, CallerUserId = _userId }, CancellationToken.None);
            Assert.That(deleted.Success, Is.True);
            Assert.That(await List(), Is.Empty);
            // Kept in the table, not removed.
            Assert.That(_context.DischargeTemplate.Single().IsDeleted, Is.True);
            Assert.That((await Save("Dengue")).Success, Is.True);
        }

        [Test]
        public async Task The_most_used_template_is_listed_first()
        {
            await Save("Appendix");
            var dengue = await Save("Dengue");
            for (var i = 0; i < 2; i++)
                await _handler.Handle(new MarkDischargeTemplateUsedRequestModel { HospitalId = _hospitalId, DischargeTemplateId = dengue.Template!.DischargeTemplateId }, CancellationToken.None);

            var list = await List();
            Assert.That(list[0].Name, Is.EqualTo("Dengue"));
            Assert.That(list[0].UseCount, Is.EqualTo(2));
            Assert.That(list[0].LastUsedAt, Is.Not.Null);
        }
    }
}
