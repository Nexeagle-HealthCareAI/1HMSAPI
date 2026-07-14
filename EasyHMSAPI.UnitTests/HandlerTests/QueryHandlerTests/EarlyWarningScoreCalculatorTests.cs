using EasyHMSAPI.Application.Handlers.QueryHandlers;
using EasyHMSAPI.Data.Constants;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.QueryHandlerTests
{
    [TestFixture]
    public class EarlyWarningScoreCalculatorTests
    {
        [Test]
        public void ComputeRespiratoryScore_Normal_ReturnsZero()
        {
            Assert.That(EarlyWarningScoreCalculator.ComputeRespiratoryScore(16), Is.EqualTo(0));
        }

        [Test]
        public void ComputeRespiratoryScore_VeryLow_ReturnsThree()
        {
            Assert.That(EarlyWarningScoreCalculator.ComputeRespiratoryScore(7), Is.EqualTo(3));
        }

        [Test]
        public void ComputeRespiratoryScore_VeryHigh_ReturnsThree()
        {
            Assert.That(EarlyWarningScoreCalculator.ComputeRespiratoryScore(26), Is.EqualTo(3));
        }

        [Test]
        public void ComputeRespiratoryScore_MildlyHigh_ReturnsTwo()
        {
            Assert.That(EarlyWarningScoreCalculator.ComputeRespiratoryScore(22), Is.EqualTo(2));
        }

        [Test]
        public void ComputeSpo2Score_Normal_ReturnsZero()
        {
            Assert.That(EarlyWarningScoreCalculator.ComputeSpo2Score(98m), Is.EqualTo(0));
        }

        [Test]
        public void ComputeSpo2Score_SeverelyLow_ReturnsThree()
        {
            Assert.That(EarlyWarningScoreCalculator.ComputeSpo2Score(88m), Is.EqualTo(3));
        }

        [Test]
        public void ComputeOxygenScore_OnSupplementalOxygen_ReturnsTwo()
        {
            Assert.That(EarlyWarningScoreCalculator.ComputeOxygenScore(true), Is.EqualTo(2));
        }

        [Test]
        public void ComputeOxygenScore_OnRoomAir_ReturnsZero()
        {
            Assert.That(EarlyWarningScoreCalculator.ComputeOxygenScore(false), Is.EqualTo(0));
        }

        [Test]
        public void ComputeBloodPressureScore_Normal_ReturnsZero()
        {
            Assert.That(EarlyWarningScoreCalculator.ComputeBloodPressureScore(120), Is.EqualTo(0));
        }

        [Test]
        public void ComputeBloodPressureScore_VeryLow_ReturnsThree()
        {
            Assert.That(EarlyWarningScoreCalculator.ComputeBloodPressureScore(85), Is.EqualTo(3));
        }

        [Test]
        public void ComputeBloodPressureScore_VeryHigh_ReturnsThree()
        {
            Assert.That(EarlyWarningScoreCalculator.ComputeBloodPressureScore(225), Is.EqualTo(3));
        }

        [Test]
        public void ComputePulseScore_Normal_ReturnsZero()
        {
            Assert.That(EarlyWarningScoreCalculator.ComputePulseScore(75), Is.EqualTo(0));
        }

        [Test]
        public void ComputePulseScore_VeryLow_ReturnsThree()
        {
            Assert.That(EarlyWarningScoreCalculator.ComputePulseScore(35), Is.EqualTo(3));
        }

        [Test]
        public void ComputePulseScore_VeryHigh_ReturnsThree()
        {
            Assert.That(EarlyWarningScoreCalculator.ComputePulseScore(135), Is.EqualTo(3));
        }

        [Test]
        public void ComputeConsciousnessScore_Alert_ReturnsZero()
        {
            Assert.That(EarlyWarningScoreCalculator.ComputeConsciousnessScore(IpdConstants.EwsConsciousnessLevel.Alert), Is.EqualTo(0));
        }

        [Test]
        public void ComputeConsciousnessScore_NotAlert_ReturnsThree()
        {
            Assert.That(EarlyWarningScoreCalculator.ComputeConsciousnessScore(IpdConstants.EwsConsciousnessLevel.ConfusionNew), Is.EqualTo(3));
        }

        [Test]
        public void ComputeTemperatureScore_Normal_ReturnsZero()
        {
            Assert.That(EarlyWarningScoreCalculator.ComputeTemperatureScore(37.0m), Is.EqualTo(0));
        }

        [Test]
        public void ComputeTemperatureScore_VeryLow_ReturnsThree()
        {
            Assert.That(EarlyWarningScoreCalculator.ComputeTemperatureScore(34.5m), Is.EqualTo(3));
        }

        [Test]
        public void ComputeTemperatureScore_VeryHigh_ReturnsTwo()
        {
            Assert.That(EarlyWarningScoreCalculator.ComputeTemperatureScore(39.5m), Is.EqualTo(2));
        }

        [Test]
        public void ComputeTotal_AllNormal_ReturnsZero()
        {
            var total = EarlyWarningScoreCalculator.ComputeTotal(
                respiratoryRate: 16, spo2: 98m, supplementalOxygen: false, systolicBp: 120,
                pulse: 75, consciousnessLevel: IpdConstants.EwsConsciousnessLevel.Alert, temperatureC: 37.0m);
            Assert.That(total, Is.EqualTo(0));
        }

        [Test]
        public void ComputeTotal_MissingInputs_TreatedAsZeroNotBlocking()
        {
            var total = EarlyWarningScoreCalculator.ComputeTotal(
                respiratoryRate: null, spo2: null, supplementalOxygen: false, systolicBp: null,
                pulse: null, consciousnessLevel: null, temperatureC: null);
            Assert.That(total, Is.EqualTo(0));
        }

        [Test]
        public void ComputeRiskBand_TotalSevenOrMore_ReturnsHigh()
        {
            Assert.That(EarlyWarningScoreCalculator.ComputeRiskBand(7, anyComponentIsThree: false), Is.EqualTo(IpdConstants.EwsRiskBand.High));
        }

        [Test]
        public void ComputeRiskBand_TotalFiveOrSix_ReturnsMedium()
        {
            Assert.That(EarlyWarningScoreCalculator.ComputeRiskBand(5, anyComponentIsThree: false), Is.EqualTo(IpdConstants.EwsRiskBand.Medium));
        }

        [Test]
        public void ComputeRiskBand_LowTotalButRedFlag_ReturnsLowMedium()
        {
            // A single "red" (=3) component is an urgent-review trigger even if the total is low.
            Assert.That(EarlyWarningScoreCalculator.ComputeRiskBand(3, anyComponentIsThree: true), Is.EqualTo(IpdConstants.EwsRiskBand.LowMedium));
        }

        [Test]
        public void ComputeRiskBand_LowTotalNoRedFlag_ReturnsLow()
        {
            Assert.That(EarlyWarningScoreCalculator.ComputeRiskBand(2, anyComponentIsThree: false), Is.EqualTo(IpdConstants.EwsRiskBand.Low));
        }
    }
}
