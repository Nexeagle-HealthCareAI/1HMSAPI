namespace EasyHMSAPI.Application.Services
{
    /// <summary>
    /// Spelling-tolerant hospital matching for "Sadar Hospital Purnia", "amri salt lake", "apolo".
    /// Generic words (hospital, clinic, nursing home ...) are ignored; every other query word is
    /// scored against its best match among the hospital's name words AND its city words, so a
    /// patient can say the city in the same breath. Same word similarity as DoctorNameMatcher.
    /// </summary>
    public static class HospitalNameMatcher
    {
        public const double MatchThreshold = 0.72;

        private static readonly HashSet<string> Generic = new(StringComparer.Ordinal)
        {
            "hospital", "hospitals", "aspatal", "haspatal", "clinic", "clinics", "nursing", "home", "care", "centre",
            "center", "medical", "medicare", "health", "healthcare", "multispeciality", "multispecialty",
            "superspeciality", "superspecialty", "speciality", "specialty", "the", "and", "of", "pvt", "ltd",
            "private", "limited", "wala", "wali", "ka", "ki", "ke", "me", "mein", "se", "near", "in",
        };

        public static double Score(string? query, string? name, string? city)
        {
            var q = DoctorNameMatcher.Tokens(query).Where(t => !Generic.Contains(t)).ToList();
            var targets = DoctorNameMatcher.Tokens(name).Where(t => !Generic.Contains(t))
                .Concat(DoctorNameMatcher.Tokens(city)).Distinct().ToList();
            if (q.Count == 0 || targets.Count == 0) return 0;
            double total = 0;
            foreach (var qt in q)
                total += targets.Max(t => DoctorNameMatcher.TokenSimilarity(qt, t));
            return Math.Round(total / q.Count, 3);
        }
    }
}
