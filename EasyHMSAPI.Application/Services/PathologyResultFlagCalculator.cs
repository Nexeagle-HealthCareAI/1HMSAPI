using System;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>Shared by GetPathologyOrderByIdHandler (drives the frontend's demographic default)
    /// and EnterPathologyResultHandler (drives the actual flag computation) so both agree on the
    /// same patient age at the moment of use.</summary>
    public static class PathologyAgeCalculator
    {
        public static int? CalculateAgeYears(DateTime? dateOfBirth)
        {
            if (!dateOfBirth.HasValue) return null;
            var today = DateTime.UtcNow.Date;
            var dob = dateOfBirth.Value.Date;
            var age = today.Year - dob.Year;
            if (dob > today.AddYears(-age)) age--;
            return age;
        }
    }

    /// <summary>One parameter's reference-range schema, as parsed from
    /// PathologyTestMaster.ParameterSchemaJson.</summary>
    public record PathologyParameterRange(
        string Name,
        string? Unit,
        string? DefaultValue,
        decimal? MaleMin,
        decimal? MaleMax,
        decimal? FemaleMin,
        decimal? FemaleMax,
        decimal? ChildMin,
        decimal? ChildMax,
        decimal? CriticalLow,
        decimal? CriticalHigh,
        int SortOrder
    );

    public enum PathologyResultFlag
    {
        NORMAL,
        HIGH,
        LOW,
        CRITICAL_HIGH,
        CRITICAL_LOW
    }

    /// <summary>
    /// Pure, deterministic reference-range evaluation for a single pathology parameter result --
    /// no DB access, mirrors AppointmentTypeResolver/PatientVolumeTrendCalculator's static-pure
    /// convention. Non-numeric results (e.g. "Positive"/"Negative"/free text, common across the
    /// serology and urine/stool panels) always come back NORMAL -- flagging is a numeric-range
    /// concept only here; text results are reviewed by the technician/pathologist directly, not
    /// auto-flagged.
    /// </summary>
    public static class PathologyResultFlagCalculator
    {
        // The PRD's demographic ranges split "Pediatric/Infant" vs "Adult"; this schema models a
        // single child band (no separate infant/adolescent tier), so 12 is the common Indian
        // clinical-lab convention for where that one child band ends.
        private const int ChildAgeCutoffYears = 12;

        public static PathologyResultFlag Evaluate(
            PathologyParameterRange range,
            string enteredValue,
            int? patientAgeYears,
            string? patientGender)
        {
            if (!decimal.TryParse(enteredValue, out var value))
                return PathologyResultFlag.NORMAL;

            // Critical thresholds are checked first and independently of the demographic normal
            // range: the PRD's critical bounds are hospital-wide safety thresholds (not
            // demographic-specific) and always sit outside whichever normal range applies, so a
            // value beyond them is critical regardless of which demographic band it also falls
            // outside of.
            if (range.CriticalLow.HasValue && value < range.CriticalLow.Value)
                return PathologyResultFlag.CRITICAL_LOW;
            if (range.CriticalHigh.HasValue && value > range.CriticalHigh.Value)
                return PathologyResultFlag.CRITICAL_HIGH;

            var (min, max) = ResolveRange(range, patientAgeYears, patientGender);
            if (min.HasValue && value < min.Value) return PathologyResultFlag.LOW;
            if (max.HasValue && value > max.Value) return PathologyResultFlag.HIGH;

            return PathologyResultFlag.NORMAL;
        }

        private static (decimal? Min, decimal? Max) ResolveRange(
            PathologyParameterRange range, int? age, string? gender)
        {
            var isChild = age.HasValue && age.Value < ChildAgeCutoffYears;
            if (isChild && (range.ChildMin.HasValue || range.ChildMax.HasValue))
                return (range.ChildMin, range.ChildMax);

            var g = gender?.Trim().ToUpperInvariant();
            if (g is "F" or "FEMALE" && (range.FemaleMin.HasValue || range.FemaleMax.HasValue))
                return (range.FemaleMin, range.FemaleMax);
            if (g is "M" or "MALE" && (range.MaleMin.HasValue || range.MaleMax.HasValue))
                return (range.MaleMin, range.MaleMax);

            // No matching demographic split available (older schema rows that only ever populate
            // one band, or gender/age unknown) -- fall back to whichever band IS populated,
            // preferring male/female over child since most parameters share one adult range across
            // sexes and child is the narrower, more specific case.
            if (range.MaleMin.HasValue || range.MaleMax.HasValue) return (range.MaleMin, range.MaleMax);
            if (range.FemaleMin.HasValue || range.FemaleMax.HasValue) return (range.FemaleMin, range.FemaleMax);
            return (range.ChildMin, range.ChildMax);
        }
    }
}
