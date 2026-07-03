using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Data.Constants;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class SofaScoreCalculatorTests
    {
        [Test]
        public void ComputeRespiratoryScore_NormalRatio_ReturnsZero()
        {
            Assert.That(SofaScoreCalculator.ComputeRespiratoryScore(450m, onRespiratorySupport: false), Is.EqualTo(0));
        }

        [Test]
        public void ComputeRespiratoryScore_SevereWithoutSupport_CapsAtTwo()
        {
            // Scores 3-4 require respiratory support per the standard SOFA definition.
            Assert.That(SofaScoreCalculator.ComputeRespiratoryScore(80m, onRespiratorySupport: false), Is.EqualTo(2));
        }

        [Test]
        public void ComputeRespiratoryScore_SevereWithSupport_ReturnsFour()
        {
            Assert.That(SofaScoreCalculator.ComputeRespiratoryScore(80m, onRespiratorySupport: true), Is.EqualTo(4));
        }

        [Test]
        public void ComputeCoagulationScore_NormalPlatelets_ReturnsZero()
        {
            Assert.That(SofaScoreCalculator.ComputeCoagulationScore(200m), Is.EqualTo(0));
        }

        [Test]
        public void ComputeCoagulationScore_SeverelyLow_ReturnsFour()
        {
            Assert.That(SofaScoreCalculator.ComputeCoagulationScore(15m), Is.EqualTo(4));
        }

        [Test]
        public void ComputeLiverScore_NormalBilirubin_ReturnsZero()
        {
            Assert.That(SofaScoreCalculator.ComputeLiverScore(0.8m), Is.EqualTo(0));
        }

        [Test]
        public void ComputeLiverScore_SeverelyHigh_ReturnsFour()
        {
            Assert.That(SofaScoreCalculator.ComputeLiverScore(15m), Is.EqualTo(4));
        }

        [Test]
        public void ComputeCardiovascularScore_NoVasopressorsNormalMap_ReturnsZero()
        {
            Assert.That(SofaScoreCalculator.ComputeCardiovascularScore(80, IpdConstants.SofaVasopressorTier.None), Is.EqualTo(0));
        }

        [Test]
        public void ComputeCardiovascularScore_NoVasopressorsLowMap_ReturnsOne()
        {
            Assert.That(SofaScoreCalculator.ComputeCardiovascularScore(65, IpdConstants.SofaVasopressorTier.None), Is.EqualTo(1));
        }

        [Test]
        public void ComputeCardiovascularScore_HighDoseVasopressors_ReturnsFour()
        {
            Assert.That(SofaScoreCalculator.ComputeCardiovascularScore(60, IpdConstants.SofaVasopressorTier.DopamineHighOrEpiHighOrNorepiHigh), Is.EqualTo(4));
        }

        [Test]
        public void ComputeCnsScore_FullyAlert_ReturnsZero()
        {
            Assert.That(SofaScoreCalculator.ComputeCnsScore(15), Is.EqualTo(0));
        }

        [Test]
        public void ComputeCnsScore_DeeplyUnresponsive_ReturnsFour()
        {
            Assert.That(SofaScoreCalculator.ComputeCnsScore(4), Is.EqualTo(4));
        }

        [Test]
        public void ComputeRenalScore_TakesWorseOfCreatinineAndUrineOutput()
        {
            // Creatinine says mild (1), urine output says severe (4) — renal should reflect the worse one.
            var score = SofaScoreCalculator.ComputeRenalScore(creatinineMgDl: 1.3m, urineOutputMlPerDay: 150m);
            Assert.That(score, Is.EqualTo(4));
        }

        [Test]
        public void ComputeRenalScore_BothNormal_ReturnsZero()
        {
            var score = SofaScoreCalculator.ComputeRenalScore(creatinineMgDl: 0.9m, urineOutputMlPerDay: 1500m);
            Assert.That(score, Is.EqualTo(0));
        }

        [Test]
        public void ComputeTotal_AllNormal_ReturnsZero()
        {
            var total = SofaScoreCalculator.ComputeTotal(
                paO2FiO2Ratio: 450m, onRespiratorySupport: false, plateletsCount: 250m, bilirubinMgDl: 0.7m,
                mapValue: 85, vasopressorTier: IpdConstants.SofaVasopressorTier.None, gcsTotal: 15,
                creatinineMgDl: 0.9m, urineOutputMlPerDay: 1800m);

            Assert.That(total, Is.EqualTo(0));
        }

        [Test]
        public void ComputeTotal_SevereMultiOrganDysfunction_SumsAcrossComponents()
        {
            var total = SofaScoreCalculator.ComputeTotal(
                paO2FiO2Ratio: 90m, onRespiratorySupport: true, plateletsCount: 18m, bilirubinMgDl: 13m,
                mapValue: 55, vasopressorTier: IpdConstants.SofaVasopressorTier.DopamineHighOrEpiHighOrNorepiHigh, gcsTotal: 5,
                creatinineMgDl: 5.5m, urineOutputMlPerDay: 150m);

            // resp4 + coag4 + liver4 + cv4 + cns4 + renal4 (max of creat4/urine4) = 24 (max possible)
            Assert.That(total, Is.EqualTo(24));
        }

        [Test]
        public void ComputeTotal_MissingInputs_ContributeZeroNotBlocked()
        {
            var total = SofaScoreCalculator.ComputeTotal(
                paO2FiO2Ratio: null, onRespiratorySupport: false, plateletsCount: null, bilirubinMgDl: null,
                mapValue: null, vasopressorTier: IpdConstants.SofaVasopressorTier.None, gcsTotal: null,
                creatinineMgDl: null, urineOutputMlPerDay: null);

            Assert.That(total, Is.EqualTo(0));
        }
    }
}
