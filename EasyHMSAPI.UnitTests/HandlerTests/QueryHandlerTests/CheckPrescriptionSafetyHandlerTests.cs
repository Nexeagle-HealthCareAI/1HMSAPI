using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using EasyHMSAPI.UnitTests.TestUtils;
using Microsoft.Extensions.Logging.Abstractions;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class CheckPrescriptionSafetyHandlerTests
    {
        private AppDbContext _context = null!;
        private CheckPrescriptionSafetyHandler _handler = null!;
        private Guid _hospitalId;
        private const string PatientId = "P-0001";

        [SetUp]
        public void SetUp()
        {
            _context = InMemoryDbContextFactory.CreateContext();
            _handler = new CheckPrescriptionSafetyHandler(_context, NullLogger<CheckPrescriptionSafetyHandler>.Instance);
            _hospitalId = Guid.NewGuid();

            _context.DrugInteraction.Add(new DrugInteraction
            {
                DrugInteractionId = Guid.NewGuid(),
                DrugA = "warfarin",
                DrugB = "aspirin",
                Severity = "MAJOR",
                Effect = "Bleeding risk",
                Management = "Avoid",
                IsActive = true,
            });
            _context.SaveChanges();
        }

        [TearDown]
        public void TearDown() => _context.Dispose();

        private static CheckPrescriptionSafetyRequestModel Request(Guid hospitalId, params (string name, string? generic)[] meds) => new()
        {
            HospitalId = hospitalId,
            PatientId = PatientId,
            Medicines = meds.Select(m => new SafetyCheckMedicine { Name = m.name, GenericName = m.generic }).ToList(),
        };

        [Test]
        public async Task Flags_a_known_interaction_between_two_medicines()
        {
            var result = await _handler.Handle(Request(_hospitalId, ("Warfarin 5mg", null), ("Ecosprin", "Aspirin 75")), CancellationToken.None);

            Assert.That(result.Checked, Is.True);
            Assert.That(result.InteractionAlerts, Has.Count.EqualTo(1));
            Assert.That(result.InteractionAlerts[0].Severity, Is.EqualTo("MAJOR"));
        }

        [Test]
        public async Task Interaction_matches_regardless_of_order_and_ignores_inactive_pairs()
        {
            _context.DrugInteraction.Add(new DrugInteraction { DrugInteractionId = Guid.NewGuid(), DrugA = "foo", DrugB = "bar", Severity = "MAJOR", IsActive = false });
            _context.SaveChanges();

            var result = await _handler.Handle(Request(_hospitalId, ("aspirin", null), ("warfarin", null), ("foo", null), ("bar", null)), CancellationToken.None);

            Assert.That(result.InteractionAlerts, Has.Count.EqualTo(1));
        }

        [Test]
        public async Task Flags_structured_drug_allergy_but_not_other_allergy_types_or_other_hospitals()
        {
            _context.PatientAllergy.AddRange(
                new PatientAllergy { PatientAllergyId = Guid.NewGuid(), HospitalId = _hospitalId, PatientId = PatientId, AllergyType = "DRUG", Allergen = "penicillin", Severity = "ANAPHYLAXIS", IsActive = true },
                new PatientAllergy { PatientAllergyId = Guid.NewGuid(), HospitalId = _hospitalId, PatientId = PatientId, AllergyType = "FOOD", Allergen = "amoxicillin", Severity = "MILD", IsActive = true },
                new PatientAllergy { PatientAllergyId = Guid.NewGuid(), HospitalId = Guid.NewGuid(), PatientId = PatientId, AllergyType = "DRUG", Allergen = "ibuprofen", Severity = "SEVERE", IsActive = true });
            _context.SaveChanges();

            var result = await _handler.Handle(Request(_hospitalId, ("Penicillin V", null), ("Amoxicillin", null), ("Ibuprofen", null)), CancellationToken.None);

            Assert.That(result.AllergyAlerts, Has.Count.EqualTo(1));
            Assert.That(result.AllergyAlerts[0].Allergen, Is.EqualTo("penicillin"));
            Assert.That(result.AllergyAlerts[0].Source, Is.EqualTo("PATIENT_ALLERGY"));
        }

        [Test]
        public async Task Flags_free_text_allergy_from_the_patient_profile()
        {
            _context.PatientRegistrations.Add(new PatientRegistration
            {
                RegistrationId = Guid.NewGuid(),
                HospitalId = _hospitalId,
                PatientId = PatientId,
                Allergies = "Sulfa, Penicillin; dust",
            });
            _context.SaveChanges();

            var result = await _handler.Handle(Request(_hospitalId, ("Penicillin G", null)), CancellationToken.None);

            Assert.That(result.AllergyAlerts, Has.Count.EqualTo(1));
            Assert.That(result.AllergyAlerts[0].Source, Is.EqualTo("PROFILE"));
        }

        [Test]
        public async Task Returns_checked_with_no_alerts_for_an_empty_or_clean_list()
        {
            var empty = await _handler.Handle(Request(_hospitalId), CancellationToken.None);
            var clean = await _handler.Handle(Request(_hospitalId, ("Paracetamol 500", null), ("Pantoprazole", null)), CancellationToken.None);

            Assert.That(empty.Checked, Is.True);
            Assert.That(clean.Checked, Is.True);
            Assert.That(clean.AllergyAlerts, Is.Empty);
            Assert.That(clean.InteractionAlerts, Is.Empty);
        }
    }
}
