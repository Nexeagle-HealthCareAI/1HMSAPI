#nullable enable
using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EasyHMSAPI.Application.Handlers.CommandHandlers;
using EasyHMSAPI.Application.RequestModels.CommandRequestModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Interfaces;
using EasyHMSAPI.Application.Services.Models;
using Moq;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.HandlerTests.CommandHandlerTests
{
    // ABDM M1, verifying an existing ABHA (cases VRFY_ABHA_101 to 405): what is refused before any
    // call, which scope each way of logging in uses, and the ABHA address login.
    [TestFixture]
    public class AbdmLoginRulesTests
    {
        private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement;

        // ---------------------------------------------------------------- Aadhaar check digit

        [TestCase("123412341234", false, Description = "starts with 1")]
        [TestCase("023456789012", false, Description = "starts with 0")]
        [TestCase("23456789012", false, Description = "eleven digits")]
        [TestCase("2345678901245", false)]
        [TestCase("23456789012a", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void AnAadhaarNumber_MustCarryACorrectCheckDigit(string? value, bool valid) =>
            Assert.That(AbdmLoginRules.IsValidAadhaar(value), Is.EqualTo(valid));

        // No number that passes the check is written here: an Aadhaar-looking number in a repository
        // may be somebody's. The one valid ending of each stem is worked out by the test.
        [TestCase("23456789012")]
        [TestCase("99999999001")]
        [TestCase("50000000000")]
        public void ExactlyOneLastDigit_MakesAnAadhaarNumberValid_AndAMistypeIsCaught(string firstEleven)
        {
            var valid = Enumerable.Range(0, 10).Select(d => firstEleven + d).Where(AbdmLoginRules.IsValidAadhaar).ToList();
            Assert.That(valid, Has.Count.EqualTo(1));
            var number = valid[0];

            Assert.That(AbdmLoginRules.IsValidAadhaar($"{number[..4]} {number[4..8]} {number[8..]}"), Is.True, "spaces are ignored");
            // Any single wrong digit, and any two neighbours swapped, is caught.
            for (var i = 1; i < 12; i++)
            {
                var wrong = number.ToCharArray();
                wrong[i] = wrong[i] == '9' ? '0' : (char)(wrong[i] + 1);
                Assert.That(AbdmLoginRules.IsValidAadhaar(new string(wrong)), Is.False, $"digit {i} wrong");
            }
            for (var i = 1; i < 11; i++)
            {
                if (number[i] == number[i + 1]) continue;
                var swapped = number.ToCharArray();
                (swapped[i], swapped[i + 1]) = (swapped[i + 1], swapped[i]);
                Assert.That(AbdmLoginRules.IsValidAadhaar(new string(swapped)), Is.False, $"digits {i} and {i + 1} swapped");
            }
        }

        [TestCase("91123456789012", "91-1234-5678-9012")]
        [TestCase("91-1234-5678-9012", "91-1234-5678-9012")]
        [TestCase(" 91 1234 5678 9012 ", "91-1234-5678-9012")]
        [TestCase("9112345678901", null)]
        [TestCase("asha@abdm", null)]
        public void AnAbhaNumber_IsSentWithItsDashes(string typed, string? expected) =>
            Assert.That(AbdmLoginRules.DashedAbhaNumber(typed), Is.EqualTo(expected));

        // ---------------------------------------------------------------- scope

        [TestCase("mobile", "abdm", "abha-login", "mobile-verify")]
        [TestCase("aadhaar", "aadhaar", "abha-login", "aadhaar-verify")]
        [TestCase("aadhaar", "abdm", "abha-login", "aadhaar-verify", Description = "an Aadhaar number can only be verified by the Aadhaar OTP")]
        [TestCase("abha-number", "abdm", "abha-login", "mobile-verify", Description = "VRFY_ABHA_201")]
        [TestCase("abha-number", "aadhaar", "abha-login", "aadhaar-verify", Description = "VRFY_ABHA_101")]
        [TestCase("abha-address", "abdm", "abha-address-login", "mobile-verify", Description = "VRFY_ABHA_202")]
        [TestCase("abha-address", "aadhaar", "abha-address-login", "aadhaar-verify", Description = "VRFY_ABHA_102")]
        public void TheScope_FollowsHowTheAccountIsNamed_AndWhereTheOtpGoes(string hint, string otpSystem, string login, string verify) =>
            Assert.That(AbdmLoginRules.Scope(hint, otpSystem), Is.EqualTo(new[] { login, verify }));

        // ---------------------------------------------------------------- ABHA address

        [TestCase("asha.rao@abdm", "asha.rao@abdm")]
        [TestCase("  Asha_Rao1@SBX ", "asha_rao1@sbx")]
        [TestCase("asha", null)]
        [TestCase("asha@", null)]
        [TestCase("@abdm", null)]
        [TestCase("as@abdm", null, Description = "too short")]
        [TestCase("asha rao@abdm", null)]
        [TestCase("asha@gmail", null, Description = "only @abdm and @sbx are ABHA addresses")]
        [TestCase("asha.@abdm", null, Description = "must end with a letter or digit")]
        [TestCase("91-1234-5678-9012", null)]
        [TestCase(null, null)]
        public void AnAbhaAddress_IsCleanedOrRefused(string? typed, string? expected) =>
            Assert.That(AbdmLoginRules.NormaliseAbhaAddress(typed), Is.EqualTo(expected));

        [Test]
        public void TheUserToken_IsReadFromEitherShape_AndAWrongOtpIsNamed()
        {
            Assert.That(AbdmLoginRules.ReadToken(Json("{\"tokens\":{\"token\":\"t-1\",\"expiresIn\":1800}}")), Is.EqualTo("t-1"));
            Assert.That(AbdmLoginRules.ReadToken(Json("{\"token\":\"t-2\"}")), Is.EqualTo("t-2"));
            Assert.That(AbdmLoginRules.ReadToken(Json("{\"message\":\"x\"}")), Is.Null);

            Assert.That(AbdmLoginRules.Failure(Json("{\"authResult\":\"success\",\"message\":\"OTP verified successfully\"}")), Is.Null);
            Assert.That(AbdmLoginRules.Failure(Json("{\"authResult\":\"failed\",\"message\":\"Please enter a valid OTP.\"}")), Is.EqualTo("Please enter a valid OTP."));
            Assert.That(AbdmLoginRules.Failure(Json("{\"tokens\":{\"token\":\"t\"}}")), Is.Null);
        }

        [Test]
        public void TheMobileOtpAfterCreation_IsVerifiedOnAbdmsPath_WithItsScope()
        {
            // Found on the sandbox on 2026-11-18: "/enrollment/auth/byAbdm/verify" with no scope was refused with 400.
            Assert.That(AbdmLoginRules.EnrolMobileVerifyPath, Is.EqualTo("/enrollment/auth/byAbdm"));
            Assert.That(AbdmLoginRules.EnrolMobileVerifyScope, Is.EqualTo(new[] { "abha-enrol", "mobile-verify" }));
            Assert.That(AbdmLoginRules.Failure(Json("{\"txnId\":\"t\",\"authResult\":\"success\",\"message\":\"Mobile number is now successfully linked to your Account\"}")), Is.Null);
            Assert.That(AbdmLoginRules.Failure(Json("{\"txnId\":\"t\",\"authResult\":\"failed\",\"message\":\"OTP expired\"}")), Is.EqualTo("OTP expired"));
        }

        [Test]
        public void WhatTheAadhaarStepToldUs_FillsTheLaterAnswers_ThatCarryOnlyTheNumber()
        {
            // Found on the sandbox on 2026-11-18: a created ABHA was saved as "Unknown" with no mobile.
            var remembered = new AbdmEnrollResult { AbhaNumber = "91-1234-5678-9012", FullName = "Asha Rao", Gender = "F", DateOfBirth = "05-08-1990", IsNew = true };

            var afterMobile = AbdmLoginRules.FillFromEnrolment(new AbdmEnrollResult { AbhaNumber = "91-1234-5678-9012" }, remembered, "9876543210");
            Assert.That(afterMobile.FullName, Is.EqualTo("Asha Rao"));
            Assert.That(afterMobile.Mobile, Is.EqualTo("9876543210"));
            Assert.That(afterMobile.MobileVerified, Is.True);

            var afterAddress = AbdmLoginRules.FillFromEnrolment(new AbdmEnrollResult { AbhaNumber = "91123456789012", AbhaAddress = "asha@sbx" }, afterMobile);
            Assert.That(afterAddress.FullName, Is.EqualTo("Asha Rao"));
            Assert.That(afterAddress.Gender, Is.EqualTo("F"));
            Assert.That(afterAddress.DateOfBirth, Is.EqualTo("05-08-1990"));
            Assert.That(afterAddress.Mobile, Is.EqualTo("9876543210"));
            Assert.That(afterAddress.AbhaAddress, Is.EqualTo("asha@sbx"));
            Assert.That(afterAddress.IsNew, Is.True);

            Assert.That(AbdmLoginRules.AbhaDigits("91-1234-5678-9012"), Is.EqualTo(AbdmLoginRules.AbhaDigits("91123456789012")));
            Assert.That(AbdmLoginRules.FillFromEnrolment(new AbdmEnrollResult { AbhaNumber = "1" }, null).FullName, Is.Empty);
        }

        [Test]
        public void ARefusedResendTransaction_IsRecognised_SoTheResendCanGoOutAsANewRequest()
        {
            // The sandbox's words on 2026-11-18, as our own message carries them.
            Assert.That(AbdmLoginRules.IsInvalidTransaction(new AbdmException("txnId: Invalid Transaction Id Reference: d98bbf56", null, 400, "r")), Is.True);
            Assert.That(AbdmLoginRules.IsInvalidTransaction(new AbdmException("Invalid TransactionId (ABDM-1017). Reference: r", "ABDM-1017", 400, "r")), Is.True);
            Assert.That(AbdmLoginRules.IsInvalidTransaction(new AbdmException("Invalid LoginId Reference: r", null, 400, "r")), Is.False);
            Assert.That(AbdmLoginRules.IsInvalidTransaction(new AbdmException("txnId: Invalid Transaction Id", null, 500, "r")), Is.False);
        }

        [Test]
        public void TheAccountBehindAnAddress_IsReadFromTheProfile_OrFromTheAccountsListed()
        {
            var profile = AbdmLoginRules.ReadAddressProfile(Json(
                "{\"abhaAddress\":\"asha@sbx\",\"fullName\":\"Asha Rao\",\"abhaNumber\":\"91-1234-5678-9012\",\"gender\":\"F\",\"dayOfBirth\":\"5\",\"monthOfBirth\":\"8\",\"yearOfBirth\":\"1990\",\"mobile\":\"9876543210\"}"),
                "txn-1", "asha@sbx");
            Assert.That(profile.AbhaNumber, Is.EqualTo("91-1234-5678-9012"));
            Assert.That(profile.AbhaAddress, Is.EqualTo("asha@sbx"));
            Assert.That(profile.FullName, Is.EqualTo("Asha Rao"));
            Assert.That(profile.DateOfBirth, Is.EqualTo("05-08-1990"));
            Assert.That(profile.TxnId, Is.EqualTo("txn-1"));

            // Two addresses on one login: the one that was typed is the one read.
            var listed = AbdmLoginRules.ReadAddressProfile(Json(
                "{\"users\":[{\"abhaAddress\":\"other@sbx\",\"fullName\":\"Other\",\"abhaNumber\":\"91-0000-0000-0000\"},{\"abhaAddress\":\"asha@sbx\",\"fullName\":\"Asha Rao\",\"abhaNumber\":\"91-1234-5678-9012\"}]}"),
                "txn-1", "asha@sbx");
            Assert.That(listed.AbhaNumber, Is.EqualTo("91-1234-5678-9012"));

            // An address with no ABHA number behind it: the number is empty, never invented.
            var bare = AbdmLoginRules.ReadAddressProfile(Json("{\"abhaAddress\":\"asha@sbx\",\"firstName\":\"Asha\",\"lastName\":\"Rao\",\"yearOfBirth\":1990}"), "txn-1", "asha@sbx");
            Assert.That(bare.AbhaNumber, Is.Empty);
            Assert.That(bare.FullName, Is.EqualTo("Asha Rao"));
            Assert.That(bare.DateOfBirth, Is.EqualTo("1990"));
        }

        // ---------------------------------------------------------------- the handlers

        [Test]
        public async Task AnInvalidAadhaarOrAddress_IsRefusedBeforeAbdmIsCalled()
        {
            var abha = new Mock<IAbdmAbhaService>(MockBehavior.Strict);
            var handler = new RequestAbdmLoginOtpHandler(abha.Object);

            var aadhaar = await handler.Handle(new RequestAbdmLoginOtpRequestModel { LoginId = "234567890120", LoginHint = "aadhaar", OtpSystem = "aadhaar" }, CancellationToken.None);
            var number = await handler.Handle(new RequestAbdmLoginOtpRequestModel { LoginId = "9112345678", LoginHint = "abha-number", OtpSystem = "abdm" }, CancellationToken.None);
            Assert.That(number.Success, Is.False);
            var address = await handler.Handle(new RequestAbdmLoginOtpRequestModel { LoginId = "asha", LoginHint = "abha-address", OtpSystem = "abdm" }, CancellationToken.None);

            Assert.That(aadhaar.Success, Is.False);
            Assert.That(aadhaar.Message, Does.Contain("not a valid Aadhaar"));
            Assert.That(address.Success, Is.False);
            Assert.That(address.Message, Does.Contain("name@abdm"));
            abha.VerifyNoOtherCalls();
        }

        [Test]
        public async Task AnAbhaNumberTypedWithoutDashes_IsSentWithThem()
        {
            var abha = new Mock<IAbdmAbhaService>(MockBehavior.Strict);
            abha.Setup(a => a.RequestLoginOtpAsync("91-1234-5678-9012", "abha-number", "aadhaar", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AbdmOtpTxnResult { TxnId = "txn-3" });

            var sent = await new RequestAbdmLoginOtpHandler(abha.Object).Handle(
                new RequestAbdmLoginOtpRequestModel { LoginId = "91123456789012", LoginHint = "abha-number", OtpSystem = "aadhaar" }, CancellationToken.None);

            Assert.That(sent.Success, Is.True);
        }

        [Test]
        public async Task AnAbhaAddress_GoesToItsOwnLogin_WithTheOtpWhereTheDeskChose()
        {
            var abha = new Mock<IAbdmAbhaService>(MockBehavior.Strict);
            abha.Setup(a => a.RequestAbhaAddressLoginOtpAsync("asha@sbx", "aadhaar", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AbdmOtpTxnResult { TxnId = "txn-7", Message = "OTP sent" });

            var sent = await new RequestAbdmLoginOtpHandler(abha.Object).Handle(
                new RequestAbdmLoginOtpRequestModel { LoginId = " Asha@SBX ", LoginHint = "abha-address", OtpSystem = "aadhaar" }, CancellationToken.None);

            Assert.That(sent.Success, Is.True);
            Assert.That(sent.TxnId, Is.EqualTo("txn-7"));
        }

        [Test]
        public async Task VerifyingAnAddress_GivesALinkProofOnlyWhenAnAbhaNumberStandsBehindIt()
        {
            var abha = new Mock<IAbdmAbhaService>(MockBehavior.Strict);
            var proofs = new Mock<IAbhaLinkProofStore>();
            proofs.Setup(p => p.Issue(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<VerifiedAbhaProfile>())).Returns("proof-1");
            var handler = new VerifyAbdmLoginOtpHandler(abha.Object, proofs.Object);
            VerifyAbdmLoginOtpRequestModel Request() => new()
            {
                HospitalId = Guid.NewGuid(), CallerUserId = Guid.NewGuid(), TxnId = "txn-7", Otp = "123456",
                LoginHint = "abha-address", OtpSystem = "abdm", LoginId = "asha@sbx"
            };

            abha.Setup(a => a.VerifyAbhaAddressLoginOtpAsync("txn-7", "123456", "abdm", "asha@sbx", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AbdmProfileResult { TxnId = "txn-7", AbhaNumber = "91-1234-5678-9012", AbhaAddress = "asha@sbx", FullName = "Asha Rao" });
            var withNumber = await handler.Handle(Request(), CancellationToken.None);
            Assert.That(withNumber.Success, Is.True);
            Assert.That(withNumber.LinkToken, Is.EqualTo("proof-1"));
            Assert.That(withNumber.Message, Is.Null);

            abha.Setup(a => a.VerifyAbhaAddressLoginOtpAsync("txn-7", "123456", "abdm", "asha@sbx", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AbdmProfileResult { TxnId = "txn-7", AbhaNumber = "", AbhaAddress = "asha@sbx", FullName = "Asha Rao" });
            var bare = await handler.Handle(Request(), CancellationToken.None);
            Assert.That(bare.Success, Is.True);
            Assert.That(bare.LinkToken, Is.Null);
            Assert.That(bare.Message, Does.Contain("no ABHA number"));

            // The address the OTP was sent for must come back with the OTP.
            var missing = Request();
            missing.LoginId = null;
            Assert.That((await handler.Handle(missing, CancellationToken.None)).Success, Is.False);
        }
    }
}
