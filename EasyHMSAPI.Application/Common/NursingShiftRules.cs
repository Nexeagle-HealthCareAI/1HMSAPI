using System.Text.RegularExpressions;
using EasyHMSAPI.Data.Constants;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Common
{
    /// <summary>
    /// Shift-code validation for nursing rosters, patient assignments and SBAR handovers. Hospitals define
    /// their own shifts (NursingShift); until a hospital has saved any, the built-in MORNING / EVENING /
    /// NIGHT apply. Codes are always upper-cased and must look like a shift code.
    /// </summary>
    public static class NursingShiftRules
    {
        private static readonly Regex CodePattern = new(@"^[A-Z0-9_\-]{1,30}$", RegexOptions.Compiled);

        /// <summary>
        /// Returns the normalised (trimmed, upper-case) shift code when it is valid for the hospital,
        /// otherwise null. With no configured shifts the three built-ins are accepted; with configured
        /// shifts only an ACTIVE configured code is accepted.
        /// </summary>
        public static async Task<string?> ResolveAsync(AppDbContext context, Guid hospitalId, string? rawCode, CancellationToken cancellationToken)
        {
            var code = rawCode?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(code) || !CodePattern.IsMatch(code)) return null;

            var configured = await context.Set<NursingShift>()
                .AsNoTracking()
                .Where(s => s.HospitalId == hospitalId)
                .Select(s => new { s.ShiftCode, s.IsActive })
                .ToListAsync(cancellationToken);

            if (configured.Count == 0)
                return IpdConstants.ShiftCode.All.Contains(code) ? code : null;

            return configured.Any(s => s.IsActive && s.ShiftCode == code) ? code : null;
        }
    }
}
