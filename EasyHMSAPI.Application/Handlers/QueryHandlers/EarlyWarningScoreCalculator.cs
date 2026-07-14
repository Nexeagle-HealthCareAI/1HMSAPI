using EasyHMSAPI.Data.Constants;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Computes a NEWS2-style Early Warning Score (Royal College of Physicians, 2017) from routine
    /// vitals. Pure/stateless, mirrors SofaScoreCalculator. Applies to any IPD admission (ward or
    /// ICU) -- not ICU-specific -- so a deteriorating ward patient is flagged before a crisis, not
    /// just tracked once they reach ICU. Missing (null) inputs contribute 0 to that component
    /// rather than blocking the score.
    /// </summary>
    public static class EarlyWarningScoreCalculator
    {
        public static int ComputeRespiratoryScore(int? respiratoryRate)
        {
            if (respiratoryRate == null) return 0;
            var r = respiratoryRate.Value;
            if (r <= 8) return 3;
            if (r <= 11) return 1;
            if (r <= 20) return 0;
            if (r <= 24) return 2;
            return 3;
        }

        public static int ComputeSpo2Score(decimal? spo2)
        {
            if (spo2 == null) return 0;
            var s = spo2.Value;
            if (s <= 91) return 3;
            if (s <= 93) return 2;
            if (s <= 95) return 1;
            return 0;
        }

        public static int ComputeOxygenScore(bool supplementalOxygen) => supplementalOxygen ? 2 : 0;

        public static int ComputeBloodPressureScore(int? systolicBp)
        {
            if (systolicBp == null) return 0;
            var s = systolicBp.Value;
            if (s <= 90) return 3;
            if (s <= 100) return 2;
            if (s <= 110) return 1;
            if (s <= 219) return 0;
            return 3;
        }

        public static int ComputePulseScore(int? pulse)
        {
            if (pulse == null) return 0;
            var p = pulse.Value;
            if (p <= 40) return 3;
            if (p <= 50) return 1;
            if (p <= 90) return 0;
            if (p <= 110) return 1;
            if (p <= 130) return 2;
            return 3;
        }

        public static int ComputeConsciousnessScore(string? consciousnessLevel) =>
            string.IsNullOrWhiteSpace(consciousnessLevel) || consciousnessLevel == IpdConstants.EwsConsciousnessLevel.Alert
                ? 0
                : 3;

        public static int ComputeTemperatureScore(decimal? temperatureC)
        {
            if (temperatureC == null) return 0;
            var t = temperatureC.Value;
            if (t <= 35.0m) return 3;
            if (t <= 36.0m) return 1;
            if (t <= 38.0m) return 0;
            if (t <= 39.0m) return 1;
            return 2;
        }

        public static int ComputeTotal(
            int? respiratoryRate, decimal? spo2, bool supplementalOxygen, int? systolicBp,
            int? pulse, string? consciousnessLevel, decimal? temperatureC)
        {
            return ComputeRespiratoryScore(respiratoryRate)
                + ComputeSpo2Score(spo2)
                + ComputeOxygenScore(supplementalOxygen)
                + ComputeBloodPressureScore(systolicBp)
                + ComputePulseScore(pulse)
                + ComputeConsciousnessScore(consciousnessLevel)
                + ComputeTemperatureScore(temperatureC);
        }

        // Standard NEWS2 clinical response bands: any single "red" (=3) component triggers at
        // least urgent (Low-Medium) review even if the total is otherwise low.
        public static string ComputeRiskBand(int totalScore, bool anyComponentIsThree)
        {
            if (totalScore >= 7) return IpdConstants.EwsRiskBand.High;
            if (totalScore >= 5) return IpdConstants.EwsRiskBand.Medium;
            if (anyComponentIsThree) return IpdConstants.EwsRiskBand.LowMedium;
            return IpdConstants.EwsRiskBand.Low;
        }
    }
}
