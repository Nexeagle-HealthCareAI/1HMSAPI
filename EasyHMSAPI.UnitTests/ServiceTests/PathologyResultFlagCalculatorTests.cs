using EasyHMSAPI.Application.Services;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.ServiceTests
{
    [TestFixture]
    public class PathologyResultFlagCalculatorTests
    {
        // Mirrors the PRD's Hemoglobin row: 13.5-17.5 (M), 12.0-15.5 (F), 11.0-14.5 (Child),
        // critical <6.0 / >20.0.
        private static PathologyParameterRange Hemoglobin() => new(
            Name: "Hemoglobin (Hb)", Unit: "g/dL", DefaultValue: "14.5",
            MaleMin: 13.5m, MaleMax: 17.5m,
            FemaleMin: 12.0m, FemaleMax: 15.5m,
            ChildMin: 11.0m, ChildMax: 14.5m,
            CriticalLow: 6.0m, CriticalHigh: 20.0m,
            SortOrder: 1);

        [Test]
        public void Evaluate_WithinFemaleRange_ReturnsNormal()
        {
            var result = PathologyResultFlagCalculator.Evaluate(Hemoglobin(), "13.0", 30, "Female");
            Assert.That(result, Is.EqualTo(PathologyResultFlag.NORMAL));
        }

        [Test]
        public void Evaluate_WithinMaleRange_ReturnsNormal()
        {
            var result = PathologyResultFlagCalculator.Evaluate(Hemoglobin(), "15.0", 30, "Male");
            Assert.That(result, Is.EqualTo(PathologyResultFlag.NORMAL));
        }

        [Test]
        public void Evaluate_BelowFemaleMin_ReturnsLow()
        {
            var result = PathologyResultFlagCalculator.Evaluate(Hemoglobin(), "10.0", 30, "Female");
            Assert.That(result, Is.EqualTo(PathologyResultFlag.LOW));
        }

        [Test]
        public void Evaluate_AboveMaleMax_ReturnsHigh()
        {
            var result = PathologyResultFlagCalculator.Evaluate(Hemoglobin(), "18.0", 30, "Male");
            Assert.That(result, Is.EqualTo(PathologyResultFlag.HIGH));
        }

        [Test]
        public void Evaluate_BelowCriticalLow_ReturnsCriticalLowNotJustLow()
        {
            // 5.0 is below BOTH the female min (12.0, which alone would say LOW) and the critical
            // threshold (6.0) -- critical must win.
            var result = PathologyResultFlagCalculator.Evaluate(Hemoglobin(), "5.0", 30, "Female");
            Assert.That(result, Is.EqualTo(PathologyResultFlag.CRITICAL_LOW));
        }

        [Test]
        public void Evaluate_AboveCriticalHigh_ReturnsCriticalHigh()
        {
            var result = PathologyResultFlagCalculator.Evaluate(Hemoglobin(), "21.0", 30, "Male");
            Assert.That(result, Is.EqualTo(PathologyResultFlag.CRITICAL_HIGH));
        }

        [Test]
        public void Evaluate_ChildUnderCutoff_UsesChildRange()
        {
            // 15.0 is within the male AND female adult ranges, but ABOVE the child max (14.5) --
            // proves the child band is actually selected, not silently falling through to adult.
            var result = PathologyResultFlagCalculator.Evaluate(Hemoglobin(), "15.0", 8, "Male");
            Assert.That(result, Is.EqualTo(PathologyResultFlag.HIGH));
        }

        [Test]
        public void Evaluate_AdultAtChildCutoffBoundary_UsesAdultRange()
        {
            // Age exactly 12 is the documented adult-side boundary (child = < 12).
            var result = PathologyResultFlagCalculator.Evaluate(Hemoglobin(), "13.5", 12, "Male");
            Assert.That(result, Is.EqualTo(PathologyResultFlag.NORMAL));
        }

        [Test]
        public void Evaluate_NoCriticalThresholdsConfigured_NeverFlagsCritical()
        {
            // Mirrors PRD parameters like MCH/MCHC that have a normal range but no critical bounds
            // at all -- an extreme value must come back HIGH, never CRITICAL_HIGH.
            var mch = new PathologyParameterRange(
                "MCH", "pg", "29.5",
                MaleMin: 27.0m, MaleMax: 32.0m,
                FemaleMin: 27.0m, FemaleMax: 32.0m,
                ChildMin: null, ChildMax: null,
                CriticalLow: null, CriticalHigh: null,
                SortOrder: 12);

            var result = PathologyResultFlagCalculator.Evaluate(mch, "99.0", 30, "Male");
            Assert.That(result, Is.EqualTo(PathologyResultFlag.HIGH));
        }

        [Test]
        public void Evaluate_MissingDemographicSplit_FallsBackToWhicheverBandIsPopulated()
        {
            // Older-schema-shaped row: only the (originally flat) min/max carried into MaleMin/Max,
            // Female/Child left null. A female patient must still get evaluated against it rather
            // than silently skipping range checks.
            var legacyShape = new PathologyParameterRange(
                "Total Cholesterol", "mg/dL", "165",
                MaleMin: 0m, MaleMax: 200m,
                FemaleMin: null, FemaleMax: null,
                ChildMin: null, ChildMax: null,
                CriticalLow: null, CriticalHigh: null,
                SortOrder: 1);

            var result = PathologyResultFlagCalculator.Evaluate(legacyShape, "250", 40, "Female");
            Assert.That(result, Is.EqualTo(PathologyResultFlag.HIGH));
        }

        [Test]
        public void Evaluate_NonNumericValue_ReturnsNormal()
        {
            var widal = new PathologyParameterRange(
                "S. Typhi O", "titre", "Negative (< 1:20)",
                MaleMin: null, MaleMax: null, FemaleMin: null, FemaleMax: null,
                ChildMin: null, ChildMax: null, CriticalLow: null, CriticalHigh: null,
                SortOrder: 1);

            var result = PathologyResultFlagCalculator.Evaluate(widal, "Reactive 1:160", 40, "Male");
            Assert.That(result, Is.EqualTo(PathologyResultFlag.NORMAL));
        }

        [Test]
        public void Evaluate_NoRangeConfiguredAtAll_ReturnsNormalRegardlessOfValue()
        {
            var noRange = new PathologyParameterRange(
                "Color", "", null,
                MaleMin: null, MaleMax: null, FemaleMin: null, FemaleMax: null,
                ChildMin: null, ChildMax: null, CriticalLow: null, CriticalHigh: null,
                SortOrder: 1);

            var result = PathologyResultFlagCalculator.Evaluate(noRange, "999999", 40, "Male");
            Assert.That(result, Is.EqualTo(PathologyResultFlag.NORMAL));
        }

        [Test]
        public void Evaluate_UnknownGenderAndAge_FallsBackToMaleRangeFirst()
        {
            var result = PathologyResultFlagCalculator.Evaluate(Hemoglobin(), "16.5", null, null);
            // 16.5 is within the male range (13.5-17.5) but above the female range (12.0-15.5) --
            // confirms the male-range fallback, not an exception or a false LOW/HIGH.
            Assert.That(result, Is.EqualTo(PathologyResultFlag.NORMAL));
        }
    }
}
