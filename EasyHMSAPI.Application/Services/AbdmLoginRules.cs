using System.Text.Json;
using System.Text.RegularExpressions;
using EasyHMSAPI.Application.Services.Models;

namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// The rules of verifying an existing ABHA (ABDM M1 cases VRFY_ABHA_101 to 405), free of any
    /// network: which scope a login uses, what a typed Aadhaar or ABHA address must look like before
    /// anything is sent, and how the ABHA address login's answers are read.
    /// </summary>
    public static class AbdmLoginRules
    {
        public const string HintMobile = "mobile";
        public const string HintAadhaar = "aadhaar";
        public const string HintAbhaNumber = "abha-number";
        public const string HintAbhaAddress = "abha-address";

        public const string OtpAadhaar = "aadhaar";
        public const string OtpAbdm = "abdm";

        /// <summary>Verifying the mobile OTP after an ABHA is created (ABDM's "After ABHA creation -
        /// verify mobile OTP"). The path has no "/verify" at the end and the scope is required.</summary>
        public const string EnrolMobileVerifyPath = "/enrollment/auth/byAbdm";
        public static readonly string[] EnrolMobileVerifyScope = { "abha-enrol", "mobile-verify" };

        /// <summary>The digits of an ABHA number, so "91-1234-5678-9012" and "91123456789012" are one key.</summary>
        public static string AbhaDigits(string? abhaNumber) => new((abhaNumber ?? string.Empty).Where(char.IsDigit).ToArray());

        /// <summary>ABDM sends the person's name, sex, birth date and mobile once, with the Aadhaar
        /// step. The later answers (mobile verified, address created) carry only the ABHA number, so
        /// what they leave empty is filled from what the Aadhaar step gave. A mobile verified in its
        /// own step wins over an empty one.</summary>
        public static AbdmEnrollResult FillFromEnrolment(AbdmEnrollResult fresh, AbdmEnrollResult? remembered, string? verifiedMobile = null)
        {
            static string? Keep(string? now, string? before) => string.IsNullOrWhiteSpace(now) ? before : now;
            if (remembered != null)
            {
                fresh.AbhaNumber = Keep(fresh.AbhaNumber, remembered.AbhaNumber) ?? string.Empty;
                fresh.AbhaAddress = Keep(fresh.AbhaAddress, remembered.AbhaAddress);
                fresh.FullName = Keep(fresh.FullName, remembered.FullName) ?? string.Empty;
                fresh.Gender = Keep(fresh.Gender, remembered.Gender);
                fresh.DateOfBirth = Keep(fresh.DateOfBirth, remembered.DateOfBirth);
                fresh.Mobile = Keep(fresh.Mobile, remembered.Mobile);
                fresh.IsNew = fresh.IsNew || remembered.IsNew;
            }
            if (!string.IsNullOrWhiteSpace(verifiedMobile))
                fresh.Mobile = verifiedMobile;
            fresh.MobileVerified = fresh.MobileVerified || !string.IsNullOrWhiteSpace(fresh.Mobile);
            return fresh;
        }

        /// <summary>ABDM refusing the transaction a resend was sent under ("Invalid Transaction Id").
        /// Seen on the sandbox for an Aadhaar OTP resend that followed ABDM's own page, so the
        /// resend then goes out as a new request.</summary>
        public static bool IsInvalidTransaction(AbdmException refusal) =>
            refusal.StatusCode == 400
            && refusal.Message.Replace(" ", string.Empty).Contains("InvalidTransaction", StringComparison.OrdinalIgnoreCase);

        public const string AddressSearchPath = "/phr/web/login/abha/search";
        public const string AddressRequestOtpPath = "/phr/web/login/abha/request/otp";
        public const string AddressVerifyPath = "/phr/web/login/abha/verify";
        public const string AddressProfilePath = "/phr/web/login/profile/abha-profile";

        // Verhoeff: the checksum UIDAI puts in the last digit of every Aadhaar number.
        private static readonly int[,] Multiply =
        {
            { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, { 1, 2, 3, 4, 0, 6, 7, 8, 9, 5 }, { 2, 3, 4, 0, 1, 7, 8, 9, 5, 6 },
            { 3, 4, 0, 1, 2, 8, 9, 5, 6, 7 }, { 4, 0, 1, 2, 3, 9, 5, 6, 7, 8 }, { 5, 9, 8, 7, 6, 0, 4, 3, 2, 1 },
            { 6, 5, 9, 8, 7, 1, 0, 4, 3, 2 }, { 7, 6, 5, 9, 8, 2, 1, 0, 4, 3 }, { 8, 7, 6, 5, 9, 3, 2, 1, 0, 4 },
            { 9, 8, 7, 6, 5, 4, 3, 2, 1, 0 }
        };
        private static readonly int[,] Permute =
        {
            { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }, { 1, 5, 7, 6, 2, 8, 3, 0, 9, 4 }, { 5, 8, 0, 3, 7, 9, 6, 1, 4, 2 },
            { 8, 9, 1, 6, 0, 4, 3, 5, 2, 7 }, { 9, 4, 5, 3, 1, 2, 6, 8, 7, 0 }, { 4, 2, 8, 6, 5, 7, 3, 9, 0, 1 },
            { 2, 7, 9, 3, 8, 0, 6, 4, 1, 5 }, { 7, 0, 4, 6, 9, 1, 3, 2, 5, 8 }
        };

        /// <summary>Twelve digits, not starting with 0 or 1, with a correct check digit. A number
        /// that fails is never sent to ABDM (cases CRT_ABHA_104 and VRFY_ABHA_401).</summary>
        public static bool IsValidAadhaar(string? value)
        {
            var digits = (value ?? string.Empty).Replace(" ", string.Empty).Replace("-", string.Empty);
            if (digits.Length != 12 || !digits.All(char.IsAsciiDigit) || digits[0] is '0' or '1')
                return false;
            var check = 0;
            for (var i = 0; i < digits.Length; i++)
                check = Multiply[check, Permute[i % 8, digits[digits.Length - 1 - i] - '0']];
            return check == 0;
        }

        /// <summary>An ABHA number as ABDM expects it before encryption: 14 digits with their
        /// dashes, XX-XXXX-XXXX-XXXX. The bare digits are refused by ABDM. Null when it is not 14 digits.</summary>
        public static string? DashedAbhaNumber(string? value)
        {
            var digits = new string((value ?? string.Empty).Where(char.IsAsciiDigit).ToArray());
            return digits.Length == 14 ? $"{digits[..2]}-{digits[2..6]}-{digits[6..10]}-{digits[10..]}" : null;
        }

        private static readonly Regex AddressPattern = new(@"^[a-z0-9][a-z0-9_.]+[a-z0-9]@(abdm|sbx)$", RegexOptions.Compiled);

        /// <summary>name@abdm (or name@sbx in the sandbox), lower-cased and trimmed; null when it is not one.</summary>
        public static string? NormaliseAbhaAddress(string? value)
        {
            var address = (value ?? string.Empty).Trim().ToLowerInvariant();
            return AddressPattern.IsMatch(address) ? address : null;
        }

        public static string NormaliseOtpSystem(string? otpSystem, string? loginHint) =>
            string.Equals(otpSystem, OtpAadhaar, StringComparison.OrdinalIgnoreCase) || string.Equals(loginHint, HintAadhaar, StringComparison.OrdinalIgnoreCase)
                ? OtpAadhaar
                : OtpAbdm;

        /// <summary>The scope of a login's OTP request, which the verify must repeat exactly (ABDM
        /// answers "Invalid Scope" otherwise). The second element follows where the OTP goes: the
        /// Aadhaar mobile or the ABHA mobile, whichever way the account was named.</summary>
        public static string[] Scope(string? loginHint, string? otpSystem)
        {
            var verify = NormaliseOtpSystem(otpSystem, loginHint) == OtpAadhaar ? "aadhaar-verify" : "mobile-verify";
            var login = string.Equals(loginHint, HintAbhaAddress, StringComparison.OrdinalIgnoreCase) ? "abha-address-login" : "abha-login";
            return new[] { login, verify };
        }

        /// <summary>The user token of an ABHA address login: {"tokens":{"token":...}} or {"token":...}.</summary>
        public static string? ReadToken(JsonElement root)
        {
            if (root.ValueKind != JsonValueKind.Object)
                return null;
            if (root.TryGetProperty("tokens", out var tokens) && tokens.ValueKind == JsonValueKind.Object)
            {
                var nested = Text(tokens, "token");
                if (nested != null)
                    return nested;
            }
            return Text(root, "token");
        }

        /// <summary>Whether the verify answer says the OTP was wrong, and ABDM's own words for it.</summary>
        public static string? Failure(JsonElement root)
        {
            var result = Text(root, "authResult");
            if (result != null && !string.Equals(result, "success", StringComparison.OrdinalIgnoreCase))
                return Text(root, "message") ?? "The OTP was not accepted.";
            return null;
        }

        /// <summary>The account behind an ABHA address, from the profile call (or, failing that, the
        /// account listed in the verify answer). The ABHA number is empty when the address has none linked.</summary>
        public static AbdmProfileResult ReadAddressProfile(JsonElement root, string txnId, string typedAddress)
        {
            var account = root;
            foreach (var list in new[] { "users", "accounts" })
            {
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty(list, out var array) && array.ValueKind == JsonValueKind.Array && array.GetArrayLength() > 0)
                {
                    account = array.EnumerateArray().FirstOrDefault(a => string.Equals(Text(a, "abhaAddress"), typedAddress, StringComparison.OrdinalIgnoreCase));
                    if (account.ValueKind != JsonValueKind.Object)
                        account = array[0];
                    break;
                }
            }

            var name = Text(account, "fullName", "name")
                ?? string.Join(' ', new[] { Text(account, "firstName"), Text(account, "middleName"), Text(account, "lastName") }.Where(s => s != null));
            return new AbdmProfileResult
            {
                TxnId = txnId,
                AbhaNumber = Text(account, "abhaNumber", "ABHANumber", "healthIdNumber") ?? string.Empty,
                AbhaAddress = Text(account, "abhaAddress", "preferredAbhaAddress") ?? typedAddress,
                FullName = name,
                Gender = Text(account, "gender"),
                DateOfBirth = Text(account, "dateOfBirth", "dob") ?? BirthDate(account),
                Mobile = Text(account, "mobile"),
                Email = Text(account, "email")
            };
        }

        private static string? BirthDate(JsonElement account)
        {
            var year = Text(account, "yearOfBirth");
            if (year == null)
                return null;
            var month = Text(account, "monthOfBirth");
            var day = Text(account, "dayOfBirth");
            return month != null && day != null ? $"{day.PadLeft(2, '0')}-{month.PadLeft(2, '0')}-{year}" : year;
        }

        private static string? Text(JsonElement element, params string[] names)
        {
            if (element.ValueKind != JsonValueKind.Object)
                return null;
            foreach (var name in names)
            {
                if (!element.TryGetProperty(name, out var value))
                    continue;
                var text = value.ValueKind switch
                {
                    JsonValueKind.String => value.GetString(),
                    JsonValueKind.Number => value.GetRawText(),
                    _ => null
                };
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }
            return null;
        }
    }
}
