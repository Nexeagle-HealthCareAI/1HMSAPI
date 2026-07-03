using EasyHMSAPI.Data.Constants;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Computes the SOFA (Sequential Organ Failure Assessment) score (Vincent et al., Intensive
    /// Care Med 1996) from raw component inputs. Pure/stateless, mirrors ApacheIIScoreCalculator.
    /// Missing (null) inputs contribute 0 to that component rather than blocking the score.
    /// </summary>
    public static class SofaScoreCalculator
    {
        public static int ComputeRespiratoryScore(decimal? paO2FiO2Ratio, bool onRespiratorySupport)
        {
            if (paO2FiO2Ratio == null) return 0;
            var r = paO2FiO2Ratio.Value;
            if (r >= 400) return 0;
            if (r >= 300) return 1;
            if (r >= 200) return 2;
            // Scores 3-4 require respiratory support per the standard SOFA definition.
            if (!onRespiratorySupport) return 2;
            if (r >= 100) return 3;
            return 4;
        }

        public static int ComputeCoagulationScore(decimal? plateletsThousandsPerUl)
        {
            if (plateletsThousandsPerUl == null) return 0;
            var p = plateletsThousandsPerUl.Value;
            if (p >= 150) return 0;
            if (p >= 100) return 1;
            if (p >= 50) return 2;
            if (p >= 20) return 3;
            return 4;
        }

        public static int ComputeLiverScore(decimal? bilirubinMgDl)
        {
            if (bilirubinMgDl == null) return 0;
            var b = bilirubinMgDl.Value;
            if (b < 1.2m) return 0;
            if (b < 2.0m) return 1;
            if (b < 6.0m) return 2;
            if (b < 12.0m) return 3;
            return 4;
        }

        public static int ComputeCardiovascularScore(int? mapValue, string vasopressorTier) => vasopressorTier switch
        {
            IpdConstants.SofaVasopressorTier.DopamineHighOrEpiHighOrNorepiHigh => 4,
            IpdConstants.SofaVasopressorTier.DopamineMedOrEpiLowOrNorepiLow => 3,
            IpdConstants.SofaVasopressorTier.DopamineLowOrDobutamine => 2,
            IpdConstants.SofaVasopressorTier.MapLow => 1,
            _ => mapValue.HasValue && mapValue.Value < 70 ? 1 : 0,
        };

        public static int ComputeCnsScore(int? gcsTotal)
        {
            if (gcsTotal == null) return 0;
            var g = gcsTotal.Value;
            if (g == 15) return 0;
            if (g >= 13) return 1;
            if (g >= 10) return 2;
            if (g >= 6) return 3;
            return 4;
        }

        public static int ComputeRenalScore(decimal? creatinineMgDl, decimal? urineOutputMlPerDay)
        {
            var fromCreatinine = 0;
            if (creatinineMgDl.HasValue)
            {
                var c = creatinineMgDl.Value;
                fromCreatinine = c >= 5.0m ? 4 : c >= 3.5m ? 3 : c >= 2.0m ? 2 : c >= 1.2m ? 1 : 0;
            }

            var fromUrine = 0;
            if (urineOutputMlPerDay.HasValue)
            {
                var u = urineOutputMlPerDay.Value;
                fromUrine = u < 200 ? 4 : u < 500 ? 3 : 0;
            }

            // Renal is scored from whichever input indicates worse function.
            return Math.Max(fromCreatinine, fromUrine);
        }

        public static int ComputeTotal(
            decimal? paO2FiO2Ratio, bool onRespiratorySupport, decimal? plateletsCount, decimal? bilirubinMgDl,
            int? mapValue, string vasopressorTier, int? gcsTotal, decimal? creatinineMgDl, decimal? urineOutputMlPerDay)
        {
            return ComputeRespiratoryScore(paO2FiO2Ratio, onRespiratorySupport)
                + ComputeCoagulationScore(plateletsCount)
                + ComputeLiverScore(bilirubinMgDl)
                + ComputeCardiovascularScore(mapValue, vasopressorTier)
                + ComputeCnsScore(gcsTotal)
                + ComputeRenalScore(creatinineMgDl, urineOutputMlPerDay);
        }
    }
}
