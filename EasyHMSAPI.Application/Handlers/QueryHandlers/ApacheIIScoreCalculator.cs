namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Computes the APACHE II score (Knaus et al., Crit Care Med 1985) from raw physiological
    /// inputs. Pure/stateless — no DB access — mirrors MarScheduleCalculator's shape so it's
    /// independently unit-testable.
    ///
    /// Simplification, documented deliberately: the oxygenation component is scored from PaO2
    /// alone at every FiO2 level. The original scale uses A-aDO2 when FiO2 &gt;= 0.5, which needs
    /// PaCO2 and barometric pressure — neither is captured this phase (that would need a fuller
    /// ABG calculator, out of scope per the ICU phase plan). Using PaO2 thresholds throughout is a
    /// known, commonly used bedside approximation when A-aDO2 isn't available.
    ///
    /// A missing (null) input contributes 0 points rather than blocking the score — TotalScore is
    /// always the sum of whatever was actually supplied, not a strict "all 12 required" gate.
    /// </summary>
    public static class ApacheIIScoreCalculator
    {
        public static int ComputeTemperaturePoints(decimal? celsius)
        {
            if (celsius == null) return 0;
            var t = celsius.Value;
            if (t >= 41) return 4;
            if (t >= 39) return 3;
            if (t >= 38.5m) return 1;
            if (t >= 36) return 0;
            if (t >= 34) return 1;
            if (t >= 32) return 2;
            if (t >= 30) return 3;
            return 4;
        }

        public static int ComputeMapPoints(int? mmHg)
        {
            if (mmHg == null) return 0;
            var m = mmHg.Value;
            if (m >= 160) return 4;
            if (m >= 130) return 3;
            if (m >= 110) return 2;
            if (m >= 70) return 0;
            if (m >= 50) return 2;
            return 4;
        }

        public static int ComputeHeartRatePoints(int? bpm)
        {
            if (bpm == null) return 0;
            var h = bpm.Value;
            if (h >= 180) return 4;
            if (h >= 140) return 3;
            if (h >= 110) return 2;
            if (h >= 70) return 0;
            if (h >= 55) return 2;
            if (h >= 40) return 3;
            return 4;
        }

        public static int ComputeRespiratoryRatePoints(int? perMin)
        {
            if (perMin == null) return 0;
            var r = perMin.Value;
            if (r >= 50) return 4;
            if (r >= 35) return 3;
            if (r >= 25) return 1;
            if (r >= 12) return 0;
            if (r >= 10) return 1;
            if (r >= 6) return 2;
            return 4;
        }

        // See class remarks — PaO2-based approximation used at every FiO2 level.
        public static int ComputeOxygenationPoints(decimal? paO2)
        {
            if (paO2 == null) return 0;
            var p = paO2.Value;
            if (p > 70) return 0;
            if (p >= 61) return 1;
            if (p >= 55) return 3;
            return 4;
        }

        public static int ComputeArterialPhPoints(decimal? ph)
        {
            if (ph == null) return 0;
            var v = ph.Value;
            if (v >= 7.7m) return 4;
            if (v >= 7.6m) return 3;
            if (v >= 7.5m) return 1;
            if (v >= 7.33m) return 0;
            if (v >= 7.25m) return 2;
            if (v >= 7.15m) return 3;
            return 4;
        }

        public static int ComputeSodiumPoints(int? mmolL)
        {
            if (mmolL == null) return 0;
            var s = mmolL.Value;
            if (s >= 180) return 4;
            if (s >= 160) return 3;
            if (s >= 155) return 2;
            if (s >= 150) return 1;
            if (s >= 130) return 0;
            if (s >= 120) return 2;
            if (s >= 111) return 3;
            return 4;
        }

        public static int ComputePotassiumPoints(decimal? mmolL)
        {
            if (mmolL == null) return 0;
            var k = mmolL.Value;
            if (k >= 7) return 4;
            if (k >= 6) return 3;
            if (k >= 5.5m) return 1;
            if (k >= 3.5m) return 0;
            if (k >= 3) return 1;
            if (k >= 2.5m) return 2;
            return 4;
        }

        public static int ComputeCreatininePoints(decimal? mgDl, bool isAcuteRenalFailure)
        {
            if (mgDl == null) return 0;
            var c = mgDl.Value;
            int points;
            if (c >= 3.5m) points = 4;
            else if (c >= 2) points = 3;
            else if (c >= 1.5m) points = 2;
            else if (c >= 0.6m) points = 0;
            else points = 2;
            return isAcuteRenalFailure ? points * 2 : points;
        }

        public static int ComputeHematocritPoints(decimal? percent)
        {
            if (percent == null) return 0;
            var h = percent.Value;
            if (h >= 60) return 4;
            if (h >= 50) return 2;
            if (h >= 46) return 1;
            if (h >= 30) return 0;
            if (h >= 20) return 2;
            return 4;
        }

        public static int ComputeWbcPoints(decimal? thousandsPerUl)
        {
            if (thousandsPerUl == null) return 0;
            var w = thousandsPerUl.Value;
            if (w >= 40) return 4;
            if (w >= 20) return 2;
            if (w >= 15) return 1;
            if (w >= 3) return 0;
            if (w >= 1) return 2;
            return 4;
        }

        public static int ComputeGcsPoints(int? gcsTotal) => gcsTotal == null ? 0 : 15 - gcsTotal.Value;

        public static int ComputeAgePoints(int? ageYears)
        {
            if (ageYears == null) return 0;
            var a = ageYears.Value;
            if (a >= 75) return 6;
            if (a >= 65) return 5;
            if (a >= 55) return 3;
            if (a >= 45) return 2;
            return 0;
        }

        public static int ComputeChronicHealthPoints(string chronicHealthCategory) => chronicHealthCategory switch
        {
            Data.Constants.IpdConstants.ApacheChronicHealthCategory.ElectivePostOp => 2,
            Data.Constants.IpdConstants.ApacheChronicHealthCategory.NonoperativeOrEmergencyPostOp => 5,
            _ => 0,
        };

        public static int ComputeAcutePhysiologyScore(
            decimal? temperature, int? mapValue, int? heartRate, int? respiratoryRate, decimal? paO2,
            decimal? arterialPh, int? serumSodium, decimal? serumPotassium, decimal? serumCreatinine,
            bool isAcuteRenalFailure, decimal? hematocrit, decimal? wbc, int? gcsTotal)
        {
            return ComputeTemperaturePoints(temperature)
                + ComputeMapPoints(mapValue)
                + ComputeHeartRatePoints(heartRate)
                + ComputeRespiratoryRatePoints(respiratoryRate)
                + ComputeOxygenationPoints(paO2)
                + ComputeArterialPhPoints(arterialPh)
                + ComputeSodiumPoints(serumSodium)
                + ComputePotassiumPoints(serumPotassium)
                + ComputeCreatininePoints(serumCreatinine, isAcuteRenalFailure)
                + ComputeHematocritPoints(hematocrit)
                + ComputeWbcPoints(wbc)
                + ComputeGcsPoints(gcsTotal);
        }

        public static int ComputeTotal(
            decimal? temperature, int? mapValue, int? heartRate, int? respiratoryRate, decimal? paO2,
            decimal? arterialPh, int? serumSodium, decimal? serumPotassium, decimal? serumCreatinine,
            bool isAcuteRenalFailure, decimal? hematocrit, decimal? wbc, int? gcsTotal,
            int? ageYears, string chronicHealthCategory)
        {
            var aps = ComputeAcutePhysiologyScore(temperature, mapValue, heartRate, respiratoryRate, paO2,
                arterialPh, serumSodium, serumPotassium, serumCreatinine, isAcuteRenalFailure, hematocrit, wbc, gcsTotal);
            return aps + ComputeAgePoints(ageYears) + ComputeChronicHealthPoints(chronicHealthCategory);
        }
    }
}
