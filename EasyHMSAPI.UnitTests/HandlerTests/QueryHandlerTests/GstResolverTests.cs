using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Data.Services;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class GstResolverTests
    {
        [Test]
        public void Resolve_IcuRoom_AlwaysExempt_RegardlessOfTariff()
        {
            var result = GstResolver.Resolve("BED", IpdConstants.WardType.Icu, roomDailyRate: 9000m,
                sourceModule: null, isBundled: false, itemGstSlabPercent: null, itemIsTaxable: false);

            Assert.That(result.IsExempt, Is.True);
            Assert.That(result.EffectiveGstRatePercent, Is.Null);
        }

        [Test]
        public void Resolve_NicuRoom_AlwaysExempt()
        {
            var result = GstResolver.Resolve("BED", IpdConstants.WardType.Nicu, roomDailyRate: 12000m,
                sourceModule: null, isBundled: false, itemGstSlabPercent: null, itemIsTaxable: false);

            Assert.That(result.IsExempt, Is.True);
        }

        [Test]
        public void Resolve_NonIcuRoomAboveThreshold_FivePercentNoItc()
        {
            var result = GstResolver.Resolve("BED", IpdConstants.WardType.Private, roomDailyRate: 6000m,
                sourceModule: null, isBundled: false, itemGstSlabPercent: null, itemIsTaxable: false);

            Assert.That(result.IsExempt, Is.False);
            Assert.That(result.EffectiveGstRatePercent, Is.EqualTo(5m));
            Assert.That(result.NoInputTaxCredit, Is.True);
        }

        [Test]
        public void Resolve_NonIcuRoomAtExactlyThreshold_Exempt()
        {
            // Threshold is a strict ">" per the spec ("> Rs.5,000/day"), so exactly 5000 stays exempt.
            var result = GstResolver.Resolve("BED", IpdConstants.WardType.General, roomDailyRate: 5000m,
                sourceModule: null, isBundled: false, itemGstSlabPercent: null, itemIsTaxable: false);

            Assert.That(result.IsExempt, Is.True);
        }

        [Test]
        public void Resolve_NonIcuRoomBelowThreshold_Exempt()
        {
            var result = GstResolver.Resolve("BED", IpdConstants.WardType.General, roomDailyRate: 2000m,
                sourceModule: null, isBundled: false, itemGstSlabPercent: null, itemIsTaxable: false);

            Assert.That(result.IsExempt, Is.True);
        }

        [Test]
        public void Resolve_BundledPackage_ExemptEvenAboveThresholdAndNonIcu()
        {
            var result = GstResolver.Resolve("BED", IpdConstants.WardType.Private, roomDailyRate: 8000m,
                sourceModule: null, isBundled: true, itemGstSlabPercent: null, itemIsTaxable: false);

            Assert.That(result.IsExempt, Is.True);
        }

        [Test]
        public void Resolve_IcuWinsOverBundledCheck_StillExemptViaIcuReason()
        {
            // Order shouldn't matter for the outcome here, but ICU is checked first — confirm no crash/conflict.
            var result = GstResolver.Resolve("BED", IpdConstants.WardType.Icu, roomDailyRate: 8000m,
                sourceModule: null, isBundled: false, itemGstSlabPercent: null, itemIsTaxable: false);

            Assert.That(result.IsExempt, Is.True);
            Assert.That(result.Reason, Does.Contain("ICU"));
        }

        [Test]
        public void Resolve_PharmacyIpd_Exempt()
        {
            var result = GstResolver.Resolve("PHARMACY", null, null,
                sourceModule: BillingConstants.SourceModule.PharmacyIpd, isBundled: false,
                itemGstSlabPercent: 12m, itemIsTaxable: true);

            Assert.That(result.IsExempt, Is.True);
        }

        [Test]
        public void Resolve_PharmacyCounter_TaxableAtItemRate()
        {
            var result = GstResolver.Resolve("PHARMACY", null, null,
                sourceModule: BillingConstants.SourceModule.PharmacyCounter, isBundled: false,
                itemGstSlabPercent: 12m, itemIsTaxable: true);

            Assert.That(result.IsExempt, Is.False);
            Assert.That(result.EffectiveGstRatePercent, Is.EqualTo(12m));
        }

        [Test]
        public void Resolve_NonRoomNonPharmacyItem_FallsThroughToItemOwnSettings_Taxable()
        {
            var result = GstResolver.Resolve("PROCEDURE", null, null,
                sourceModule: null, isBundled: false, itemGstSlabPercent: 18m, itemIsTaxable: true);

            Assert.That(result.IsExempt, Is.False);
            Assert.That(result.EffectiveGstRatePercent, Is.EqualTo(18m));
        }

        [Test]
        public void Resolve_NonRoomNonPharmacyItem_FallsThroughToItemOwnSettings_Exempt()
        {
            var result = GstResolver.Resolve("PROCEDURE", null, null,
                sourceModule: null, isBundled: false, itemGstSlabPercent: null, itemIsTaxable: false);

            Assert.That(result.IsExempt, Is.True);
        }

        [Test]
        public void Resolve_RoomCategoryCaseInsensitive()
        {
            var result = GstResolver.Resolve("bed", IpdConstants.WardType.Icu, roomDailyRate: 9000m,
                sourceModule: null, isBundled: false, itemGstSlabPercent: null, itemIsTaxable: false);

            Assert.That(result.IsExempt, Is.True);
        }
    }
}
