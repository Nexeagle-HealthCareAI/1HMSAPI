using EasyHMSAPI.Application.Services;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    // Linking a facility to our bridge: the names ABDM's facility registry accepts.
    [TestFixture]
    public class AbdmFacilityRulesTests
    {
        [TestCase("Test Hospitalyyy", "Test Hospitalyy")]
        [TestCase("St. John's Clinic & Lab", "St John s Clini")]
        [TestCase("  Apollo   Care  ", "Apollo Care")]
        [TestCase("", "Hospital")]
        [TestCase(null, "Hospital")]
        public void TheNamePatientsSee_IsLettersDigitsAndSpaces_AtMost15(string? name, string expected)
        {
            var hip = AbdmFacilityRules.HipName(name);
            Assert.That(hip, Is.EqualTo(expected));
            Assert.That(AbdmFacilityRules.IsValidHipName(hip), Is.True);
        }

        [TestCase("St. John's Clinic & Lab", "St. John s Clinic Lab")]
        [TestCase("-Care (East)/2", "Care (East)/2")]
        public void TheFacilityName_KeepsOnlyTheCharactersTheRegistryAccepts(string name, string expected) =>
            Assert.That(AbdmFacilityRules.FacilityName(name), Is.EqualTo(expected));

        [TestCase("Sixteen chars xx", false)]
        [TestCase("Bad#Name", false)]
        [TestCase("Good Name 1", true)]
        public void AHipNameTypedByStaff_IsChecked(string name, bool ok) =>
            Assert.That(AbdmFacilityRules.IsValidHipName(name), Is.EqualTo(ok));
    }
}
