using System;
using System.Collections.Generic;
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
    public class SaltCompositionCommandHandlersTests
    {
        private AppDbContext _context = null!;
        private SaltCompositionCommandHandlers _handler = null!;

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new SaltCompositionCommandHandlers(_context);
        }

        [TearDown]
        public void TearDown()
        {
            _context.Database.EnsureDeleted();
            _context.Dispose();
        }

        [Test]
        public async Task Handle_CreateMolecule_CreatesNewRow()
        {
            var response = await _handler.Handle(new CreateMoleculeRequestModel { Name = "Amoxicillin" }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.MoleculeId, Is.Not.Null);
            Assert.That(_context.Molecule.Count(), Is.EqualTo(1));
        }

        [Test]
        public async Task Handle_CreateMolecule_DuplicateName_ReturnsExistingId()
        {
            var first = await _handler.Handle(new CreateMoleculeRequestModel { Name = "Amoxicillin" }, CancellationToken.None);
            var second = await _handler.Handle(new CreateMoleculeRequestModel { Name = "amoxicillin" }, CancellationToken.None);

            Assert.That(second.Success, Is.True);
            Assert.That(second.MoleculeId, Is.EqualTo(first.MoleculeId));
            Assert.That(_context.Molecule.Count(), Is.EqualTo(1));
        }

        [Test]
        public async Task Handle_CreateMolecule_BlankName_ReturnsError()
        {
            var response = await _handler.Handle(new CreateMoleculeRequestModel { Name = "  " }, CancellationToken.None);
            Assert.That(response.Success, Is.False);
        }

        [Test]
        public async Task Handle_CreateSaltComposition_WithValidMolecules_CreatesCompositionAndComponents()
        {
            var molecule = new Molecule { MoleculeId = Guid.NewGuid(), Name = "Amoxicillin", CreatedAt = DateTime.UtcNow };
            _context.Molecule.Add(molecule);
            await _context.SaveChangesAsync();

            var response = await _handler.Handle(new CreateSaltCompositionRequestModel
            {
                DisplayName = "Amoxicillin 500mg",
                DosageForm = "tablet",
                Components = new List<SaltCompositionComponentInput>
                {
                    new() { MoleculeId = molecule.MoleculeId, StrengthValue = 500, StrengthUnit = "mg" }
                }
            }, CancellationToken.None);

            Assert.That(response.Success, Is.True);
            Assert.That(response.SaltCompositionId, Is.Not.Null);
            Assert.That(_context.SaltCompositionComponent.Count(), Is.EqualTo(1));
            var saved = _context.SaltComposition.Single();
            Assert.That(saved.DosageForm, Is.EqualTo("TABLET"));
        }

        [Test]
        public async Task Handle_CreateSaltComposition_UnknownMolecule_ReturnsError()
        {
            var response = await _handler.Handle(new CreateSaltCompositionRequestModel
            {
                DisplayName = "Ghost Drug",
                Components = new List<SaltCompositionComponentInput> { new() { MoleculeId = Guid.NewGuid(), StrengthValue = 1, StrengthUnit = "MG" } }
            }, CancellationToken.None);

            Assert.That(response.Success, Is.False);
            Assert.That(_context.SaltComposition.Count(), Is.EqualTo(0));
        }

        [Test]
        public async Task Handle_CreateSaltComposition_NoComponents_ReturnsError()
        {
            var response = await _handler.Handle(new CreateSaltCompositionRequestModel { DisplayName = "Empty" }, CancellationToken.None);
            Assert.That(response.Success, Is.False);
        }
    }
}
