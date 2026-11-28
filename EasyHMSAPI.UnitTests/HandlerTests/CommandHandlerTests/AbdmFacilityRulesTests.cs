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

        [Test]
        public void LinkedTypes_ReadsTheRolesAbdmLinked_EvenBesideARefusal()
        {
            // ABDM's real sandbox answer, 28 Nov 2026: the HIP entry linked, the HIU entry refused.
            var answer = "[{\"servicesLinked\":{\"id\":\"IN1010001608\",\"name\":\"GLISAN Hospital\",\"types\":[\"HIP\"],\"active\":true}},"
                + "{\"error\":{\"code\":\"2500\",\"message\":\"Bridge-Id=SBXID_058814 is already associated with Hfr-Id=IN1010001608\"}}]";
            Assert.That(AbdmFacilityRules.LinkedTypes(answer), Is.EqualTo(new[] { "HIP" }));
            Assert.That(AbdmFacilityRules.LinkError(answer)!.Value.Code, Is.EqualTo("2500"));

            Assert.That(AbdmFacilityRules.LinkedTypes("[{\"servicesLinked\":{\"types\":[\"HIP\",\"hiu\"],\"active\":true}}]"), Is.EqualTo(new[] { "HIP", "HIU" }));
            Assert.That(AbdmFacilityRules.LinkedTypes("[{\"servicesLinked\":{\"types\":[\"HIP\"],\"active\":false}}]"), Is.Empty);
            Assert.That(AbdmFacilityRules.LinkedTypes("[{\"error\":{\"code\":\"2500\"}}]"), Is.Empty);
            Assert.That(AbdmFacilityRules.LinkedTypes(null), Is.Empty);
        }

        [Test]
        public void LinkError_FindsTheRefusalInsideAn200Answer()
        {
            var refusal = AbdmFacilityRules.LinkError("[{\"error\":{\"code\":\"2500\",\"message\":\"facility id is not registered in HFR or invalid\"}}]");
            Assert.That(refusal, Is.Not.Null);
            Assert.That(refusal!.Value.Code, Is.EqualTo("2500"));
            Assert.That(refusal.Value.Message, Is.EqualTo("facility id is not registered in HFR or invalid"));

            Assert.That(AbdmFacilityRules.LinkError("{\"error\":{\"code\":1035}}")!.Value.Code, Is.EqualTo("1035"));
            Assert.That(AbdmFacilityRules.LinkError("[{\"servicesLinked\":true,\"error\":null}]"), Is.Null);
            Assert.That(AbdmFacilityRules.LinkError(""), Is.Null);
            Assert.That(AbdmFacilityRules.LinkError("not json"), Is.Null);
        }
    }
}
