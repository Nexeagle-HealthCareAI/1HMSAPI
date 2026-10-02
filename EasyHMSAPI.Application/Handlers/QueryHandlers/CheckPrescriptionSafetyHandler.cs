using EasyHMSAPI.Application.RequestModels.QueryRequestModels;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using EasyHMSAPI.Domain.Context;
using EasyHMSAPI.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace EasyHMSAPI.Application.Handlers.QueryHandlers
{
    /// <summary>
    /// Drug-allergy and drug-drug interaction screen for a medication list. Matching is a
    /// case-insensitive substring match of the stored (lowercase) drug/allergen term against each
    /// medicine's name + generic name -- adequate for the seeded starter set and deliberately
    /// conservative: it can miss a brand-only name when no generic is supplied, and it never claims a
    /// combination is safe (callers say "no known interactions in the starter database").
    /// </summary>
    public class CheckPrescriptionSafetyHandler : IRequestHandler<CheckPrescriptionSafetyRequestModel, CheckPrescriptionSafetyResponseModel>
    {
        private static readonly Regex Splitter = new(@"[,;/\n\r]+", RegexOptions.Compiled);
        private static readonly string[] SeverityOrder = { "CONTRAINDICATED", "ANAPHYLAXIS", "SEVERE", "MAJOR", "MODERATE", "MILD", "MINOR" };

        private readonly AppDbContext _context;
        private readonly ILogger<CheckPrescriptionSafetyHandler> _logger;

        public CheckPrescriptionSafetyHandler(AppDbContext context, ILogger<CheckPrescriptionSafetyHandler> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<CheckPrescriptionSafetyResponseModel> Handle(CheckPrescriptionSafetyRequestModel request, CancellationToken cancellationToken)
        {
            var response = new CheckPrescriptionSafetyResponseModel();
            var meds = (request.Medicines ?? new List<SafetyCheckMedicine>())
                .Select(m => new { m.Name, Haystack = $"{m.Name} {m.GenericName}".Trim().ToLowerInvariant() })
                .Where(m => m.Haystack.Length >= 3)
                .ToList();
            if (meds.Count == 0 || string.IsNullOrWhiteSpace(request.PatientId))
            {
                response.Checked = true;
                return response;
            }

            try
            {
                // ---- Allergies: structured rows + free-text on the patient profile ----
                var allergies = await _context.PatientAllergy.AsNoTracking()
                    .Where(a => a.HospitalId == request.HospitalId && a.PatientId == request.PatientId && a.IsActive && a.AllergyType == "DRUG")
                    .ToListAsync(cancellationToken);

                var profileText = await _context.PatientRegistrations.AsNoTracking()
                    .Where(p => p.HospitalId == request.HospitalId && p.PatientId == request.PatientId)
                    .Select(p => p.Allergies)
                    .FirstOrDefaultAsync(cancellationToken);

                var allergenRows = allergies
                    .Select(a => (Allergen: a.Allergen, Severity: a.Severity, Reaction: a.Reaction, Source: "PATIENT_ALLERGY"))
                    .ToList();
                if (!string.IsNullOrWhiteSpace(profileText))
                {
                    foreach (var token in Splitter.Split(profileText).Select(t => t.Trim()).Where(t => t.Length >= 3))
                        allergenRows.Add((token, "SEVERE", null, "PROFILE"));
                }

                foreach (var med in meds)
                {
                    foreach (var row in allergenRows)
                    {
                        var allergen = row.Allergen.Trim().ToLowerInvariant();
                        if (allergen.Length < 3 || !med.Haystack.Contains(allergen)) continue;
                        if (response.AllergyAlerts.Any(x => x.Medicine == (med.Name ?? med.Haystack) && x.Allergen == row.Allergen)) continue;
                        response.AllergyAlerts.Add(new AllergyAlertDataModel
                        {
                            Medicine = med.Name ?? med.Haystack,
                            Allergen = row.Allergen,
                            Severity = row.Severity,
                            Reaction = row.Reaction,
                            Source = row.Source,
                        });
                    }
                }

                // ---- Interactions: every pair of medicines vs the (small) global pair table ----
                if (meds.Count >= 2)
                {
                    var pairs = await _context.DrugInteraction.AsNoTracking()
                        .Where(i => i.IsActive).ToListAsync(cancellationToken);

                    for (var i = 0; i < meds.Count; i++)
                    {
                        for (var j = i + 1; j < meds.Count; j++)
                        {
                            foreach (var p in pairs)
                            {
                                var ab = meds[i].Haystack.Contains(p.DrugA) && meds[j].Haystack.Contains(p.DrugB);
                                var ba = meds[i].Haystack.Contains(p.DrugB) && meds[j].Haystack.Contains(p.DrugA);
                                if (!ab && !ba) continue;
                                response.InteractionAlerts.Add(new InteractionAlertDataModel
                                {
                                    MedicineA = meds[i].Name ?? meds[i].Haystack,
                                    MedicineB = meds[j].Name ?? meds[j].Haystack,
                                    Severity = p.Severity,
                                    Effect = p.Effect,
                                    Management = p.Management,
                                });
                            }
                        }
                    }
                }

                response.AllergyAlerts = response.AllergyAlerts.OrderBy(a => Rank(a.Severity)).ToList();
                response.InteractionAlerts = response.InteractionAlerts.OrderBy(a => Rank(a.Severity)).ToList();
                response.Checked = true;
            }
            catch (Exception ex)
            {
                // Tables may not be deployed yet (separate DB pipeline) -- fail SAFE: report "not checked".
                _logger.LogWarning(ex, "Prescription safety check could not run for patient {PatientId}", request.PatientId);
                response.Checked = false;
                response.Message = "Allergy/interaction check is currently unavailable.";
            }

            return response;
        }

        private static int Rank(string severity)
        {
            var idx = Array.IndexOf(SeverityOrder, (severity ?? string.Empty).ToUpperInvariant());
            return idx < 0 ? int.MaxValue : idx;
        }
    }
}
