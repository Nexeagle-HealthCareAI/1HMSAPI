using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Data.Constants;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class ApacheIIScoreCalculatorTests
    {
        [Test]
        public void ComputeTemperaturePoints_NormalRange_ReturnsZero()
        {
            Assert.That(ApacheIIScoreCalculator.ComputeTemperaturePoints(37m), Is.EqualTo(0));
        }

        [Test]
        public void ComputeTemperaturePoints_VeryHigh_ReturnsFour()
        {
            Assert.That(ApacheIIScoreCalculator.ComputeTemperaturePoints(41.5m), Is.EqualTo(4));
        }

        [Test]
        public void ComputeTemperaturePoints_VeryLow_ReturnsFour()
        {
            Assert.That(ApacheIIScoreCalculator.ComputeTemperaturePoints(29m), Is.EqualTo(4));
        }

        [Test]
        public void ComputeTemperaturePoints_Null_ReturnsZero()
        {
            Assert.That(ApacheIIScoreCalculator.ComputeTemperaturePoints(null), Is.EqualTo(0));
        }

        [Test]
        public void ComputeMapPoints_Normal_ReturnsZero()
        {
            Assert.That(ApacheIIScoreCalculator.ComputeMapPoints(90), Is.EqualTo(0));
        }

        [Test]
        public void ComputeMapPoints_SeverelyLow_ReturnsFour()
        {
            Assert.That(ApacheIIScoreCalculator.ComputeMapPoints(45), Is.EqualTo(4));
        }

        [Test]
        public void ComputeGcsPoints_FullyAlert_ReturnsZero()
        {
            Assert.That(ApacheIIScoreCalculator.ComputeGcsPoints(15), Is.EqualTo(0));
        }

        [Test]
        public void ComputeGcsPoints_DeeplyUnresponsive_ReturnsTwelve()
        {
            Assert.That(ApacheIIScoreCalculator.ComputeGcsPoints(3), Is.EqualTo(12));
        }

        [Test]
        public void ComputeCreatininePoints_AcuteRenalFailure_DoublesPoints()
        {
            var withoutArf = ApacheIIScoreCalculator.ComputeCreatininePoints(4m, isAcuteRenalFailure: false);
            var withArf = ApacheIIScoreCalculator.ComputeCreatininePoints(4m, isAcuteRenalFailure: true);
            Assert.That(withoutArf, Is.EqualTo(4));
            Assert.That(withArf, Is.EqualTo(8));
        }

        [Test]
        public void ComputeAgePoints_UnderForty_ReturnsZero()
        {
            Assert.That(ApacheIIScoreCalculator.ComputeAgePoints(35), Is.EqualTo(0));
        }

        [Test]
        public void ComputeAgePoints_SeventyFiveOrOlder_ReturnsSix()
        {
            Assert.That(ApacheIIScoreCalculator.ComputeAgePoints(80), Is.EqualTo(6));
        }

        [Test]
        public void ComputeChronicHealthPoints_None_ReturnsZero()
        {
            Assert.That(ApacheIIScoreCalculator.ComputeChronicHealthPoints(IpdConstants.ApacheChronicHealthCategory.None), Is.EqualTo(0));
        }

        [Test]
        public void ComputeChronicHealthPoints_ElectivePostOp_ReturnsTwo()
        {
            Assert.That(ApacheIIScoreCalculator.ComputeChronicHealthPoints(IpdConstants.ApacheChronicHealthCategory.ElectivePostOp), Is.EqualTo(2));
        }

        [Test]
        public void ComputeChronicHealthPoints_NonoperativeOrEmergencyPostOp_ReturnsFive()
        {
            Assert.That(ApacheIIScoreCalculator.ComputeChronicHealthPoints(IpdConstants.ApacheChronicHealthCategory.NonoperativeOrEmergencyPostOp), Is.EqualTo(5));
        }

        [Test]
        public void ComputeTotal_AllNormalValues_YoungHealthyPatient_ReturnsZero()
        {
            var total = ApacheIIScoreCalculator.ComputeTotal(
                temperature: 37m, mapValue: 90, heartRate: 80, respiratoryRate: 16, paO2: 90m,
                arterialPh: 7.4m, serumSodium: 140, serumPotassium: 4m, serumCreatinine: 1.0m,
                isAcuteRenalFailure: false, hematocrit: 40m, wbc: 8m, gcsTotal: 15,
                ageYears: 30, chronicHealthCategory: IpdConstants.ApacheChronicHealthCategory.None);

            Assert.That(total, Is.EqualTo(0));
        }

        [Test]
        public void ComputeTotal_MissingInputs_ContributeZeroNotBlocked()
        {
            var total = ApacheIIScoreCalculator.ComputeTotal(
                temperature: null, mapValue: null, heartRate: null, respiratoryRate: null, paO2: null,
                arterialPh: null, serumSodium: null, serumPotassium: null, serumCreatinine: null,
                isAcuteRenalFailure: false, hematocrit: null, wbc: null, gcsTotal: 15,
                ageYears: 30, chronicHealthCategory: IpdConstants.ApacheChronicHealthCategory.None);

            Assert.That(total, Is.EqualTo(0));
        }

        [Test]
        public void ComputeTotal_CriticallyIllElderlyPatient_SumsAcrossComponents()
        {
            var total = ApacheIIScoreCalculator.ComputeTotal(
                temperature: 39.5m, mapValue: 45, heartRate: 145, respiratoryRate: 38, paO2: 50m,
                arterialPh: 7.2m, serumSodium: 158, serumPotassium: 6.2m, serumCreatinine: 4m,
                isAcuteRenalFailure: true, hematocrit: 22m, wbc: 25m, gcsTotal: 8,
                ageYears: 78, chronicHealthCategory: IpdConstants.ApacheChronicHealthCategory.NonoperativeOrEmergencyPostOp);

            // temp3 + map4 + hr3 + rr3 + o2(4) + ph3 + na2 + k3 + creat(4*2=8) + hct2 + wbc2 + gcs7 = 44 APS
            // + age6 + chronic5 = 55
            Assert.That(total, Is.EqualTo(55));
        }
    }
}
