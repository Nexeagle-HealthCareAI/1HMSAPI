using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Fuzzy duplicate detection. Narrows candidates in SQL (same hospital, not merged, sharing a
    /// strong signal), then scores names with Jaro-Winkler and classifies per the agreed rules:
    ///   NEAR_CERTAIN — Aadhaar last-4 match + name ≥ 0.80
    ///   PROBABLE     — mobile match + name ≥ 0.85
    ///   POSSIBLE     — DOB match + name ≥ 0.85
    /// </summary>
    public class CheckPatientDuplicatesHandler : IRequestHandler<CheckPatientDuplicatesRequestModel, CheckPatientDuplicatesResponseModel>
    {
        private const double NearCertainNameThreshold = 0.80;
        private const double FuzzyNameThreshold = 0.85;
        private readonly AppDbContext _context;

        public CheckPatientDuplicatesHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<CheckPatientDuplicatesResponseModel> Handle(CheckPatientDuplicatesRequestModel request, CancellationToken cancellationToken)
        {
            try
            {
                if (request.HospitalId == Guid.Empty || string.IsNullOrWhiteSpace(request.FullName))
                    return new CheckPatientDuplicatesResponseModel { Success = true };

                var mobile = string.IsNullOrWhiteSpace(request.Mobile) ? null : request.Mobile.Trim();
                var dob = request.DateOfBirth?.Date;
                var aadhaar4 = FuzzyMatch.Last4Digits(request.AadhaarNumber);
                var namePrefix = FuzzyMatch.Normalize(request.FullName);
                namePrefix = namePrefix.Length >= 3 ? namePrefix[..3] : namePrefix;

                // Narrow the candidate set in SQL on the strong signals + a coarse name prefix.
                IQueryable<PatientRegistration> q = _context.PatientRegistrations
                    .Where(p => p.HospitalId == request.HospitalId
                                && p.MergedIntoPatientId == null
                                && p.PatientId != null
                                && (request.ExcludePatientId == null || p.PatientId != request.ExcludePatientId));

                q = q.Where(p =>
                    (mobile != null && p.Mobile == mobile) ||
                    (dob != null && p.DateOfBirth != null && p.DateOfBirth.Value.Date == dob) ||
                    (aadhaar4 != null && p.AadhaarNumber != null && p.AadhaarNumber.EndsWith(aadhaar4)) ||
                    (namePrefix.Length > 0 && p.FullName != null && p.FullName.ToLower().StartsWith(namePrefix)));

                var candidates = await q.Take(200).ToListAsync(cancellationToken);

                var matches = new List<DuplicateMatch>();
                foreach (var c in candidates)
                {
                    var sim = FuzzyMatch.JaroWinkler(request.FullName, c.FullName);
                    var matchedOn = new List<string>();

                    bool mobileMatch = mobile != null && c.Mobile == mobile;
                    bool dobMatch = dob != null && c.DateOfBirth?.Date == dob;
                    bool aadhaarMatch = aadhaar4 != null && FuzzyMatch.Last4Digits(c.AadhaarNumber) == aadhaar4;

                    string? confidence = null;
                    if (aadhaarMatch && sim >= NearCertainNameThreshold) confidence = "NEAR_CERTAIN";
                    else if (mobileMatch && sim >= FuzzyNameThreshold) confidence = "PROBABLE";
                    else if (dobMatch && sim >= FuzzyNameThreshold) confidence = "POSSIBLE";

                    if (confidence == null) continue;

                    if (sim >= FuzzyNameThreshold || aadhaarMatch) matchedOn.Add("NAME");
                    if (mobileMatch) matchedOn.Add("MOBILE");
                    if (dobMatch) matchedOn.Add("DOB");
                    if (aadhaarMatch) matchedOn.Add("AADHAAR4");

                    matches.Add(new DuplicateMatch
                    {
                        PatientId = c.PatientId!,
                        FullName = c.FullName,
                        Mobile = c.Mobile,
                        Age = c.Age,
                        AgeUnit = c.AgeUnit,
                        Sex = c.Sex,
                        City = c.City,
                        Similarity = Math.Round(sim, 3),
                        Confidence = confidence,
                        MatchedOn = matchedOn,
                    });
                }

                int Rank(string c) => c switch { "NEAR_CERTAIN" => 0, "PROBABLE" => 1, _ => 2 };
                matches = matches
                    .OrderBy(m => Rank(m.Confidence))
                    .ThenByDescending(m => m.Similarity)
                    .Take(10)
                    .ToList();

                return new CheckPatientDuplicatesResponseModel { Success = true, Matches = matches };
            }
            catch (Exception)
            {
                // Advisory feature — never block the caller on failure.
                return new CheckPatientDuplicatesResponseModel { Success = false, Message = "Error checking duplicates." };
            }
        }
    }
}
