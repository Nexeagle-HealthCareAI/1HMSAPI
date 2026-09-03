using EasyHMSAPI.Application.Services;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.ServiceTests
{
    [TestFixture]
    public class BillingDepartmentClassifierTests
    {
        [TestCase("LAB_PATH")]
        [TestCase("LAB PATH")]
        [TestCase("LAB")]
        [TestCase("PATHOLOGY")]
        [TestCase("RAD")]
        [TestCase("RADIOLOGY")]
        public void Classify_LabFamilyCategoryCode_IsLab(string categoryCode)
        {
            Assert.That(BillingDepartmentClassifier.Classify(categoryCode, null, "OPD"), Is.EqualTo(BillingDepartmentClassifier.Lab));
        }

        [TestCase("PHARMACY")]
        [TestCase("DRUG")]
        [TestCase("PHARM_RETAIL")]
        public void Classify_PharmacyFamilyCategoryCode_IsPharmacy(string categoryCode)
        {
            Assert.That(BillingDepartmentClassifier.Classify(categoryCode, null, "IPD"), Is.EqualTo(BillingDepartmentClassifier.Pharmacy));
        }

        [Test]
        public void Classify_LabSourceModule_IsLabEvenWithGenericCategoryCode()
        {
            // e.g. SourceModule = "LAB_PATH" but a hospital-configured CategoryCode that doesn't
            // itself mention lab -- SourceModule should still catch it.
            Assert.That(BillingDepartmentClassifier.Classify("DIAGNOSTIC_FEE", "LAB_PATH", "OPD"), Is.EqualTo(BillingDepartmentClassifier.Lab));
        }

        [Test]
        public void Classify_PharmacyCounterSourceModule_IsPharmacy()
        {
            Assert.That(BillingDepartmentClassifier.Classify("RETAIL_ITEM", "PHARMACY_COUNTER", null), Is.EqualTo(BillingDepartmentClassifier.Pharmacy));
        }

        [Test]
        public void Classify_NonLabNonPharmacy_FallsBackToEncounterType_Opd()
        {
            Assert.That(BillingDepartmentClassifier.Classify("CONSULT", null, "OPD"), Is.EqualTo(BillingDepartmentClassifier.Opd));
        }

        [Test]
        public void Classify_NonLabNonPharmacy_FallsBackToEncounterType_Ipd()
        {
            Assert.That(BillingDepartmentClassifier.Classify("BED", null, "IPD"), Is.EqualTo(BillingDepartmentClassifier.Ipd));
        }

        [Test]
        public void Classify_PharmacyRetailCounterEncounterType_IsPharmacy()
        {
            // PharmacyRetailCheckoutCommandHandler tags the encounter itself as EncounterTypeCode "PHARMACY"
            // for a walk-in retail sale with no linked OPD/IPD visit.
            Assert.That(BillingDepartmentClassifier.Classify("RETAIL_ITEM", null, "PHARMACY"), Is.EqualTo(BillingDepartmentClassifier.Pharmacy));
        }

        [Test]
        public void Classify_UnresolvableEncounterType_IsOther()
        {
            Assert.That(BillingDepartmentClassifier.Classify("PROCEDURE", null, "ER"), Is.EqualTo(BillingDepartmentClassifier.Other));
        }

        [Test]
        public void Classify_NoCategoryNoEncounter_IsOther()
        {
            Assert.That(BillingDepartmentClassifier.Classify(null, null, null), Is.EqualTo(BillingDepartmentClassifier.Other));
        }

        [Test]
        public void Classify_LabCheckTakesPriorityOverEncounterType()
        {
            // A lab test billed against an IPD encounter must still land in LAB, not IPD.
            Assert.That(BillingDepartmentClassifier.Classify("LAB_PATH", null, "IPD"), Is.EqualTo(BillingDepartmentClassifier.Lab));
        }
    }
}
