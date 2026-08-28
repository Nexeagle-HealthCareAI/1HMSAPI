using System.Text.RegularExpressions;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Mirrors the CK_PrescriptionSettings_TextColour_Hex / CK_DischargeSettings_TextColour_Hex
    /// database CHECK constraints (#RRGGBB or #RRGGBBAA) -- validated here too so a malformed
    /// value gets a clear, specific error instead of the database being the only thing that
    /// catches it (surfacing as a generic, unhelpful save failure).
    /// </summary>
    public static class TextColourValidator
    {
        private static readonly Regex HexColourPattern = new(@"^#[0-9A-Fa-f]{6}([0-9A-Fa-f]{2})?$", RegexOptions.Compiled);

        public static bool IsValid(string textColour) => HexColourPattern.IsMatch(textColour);
    }
}
