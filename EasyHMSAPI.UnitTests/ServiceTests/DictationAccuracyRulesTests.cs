using System.Linq;
using EasyHMSAPI.Application.Services;
using EasyHMSAPI.Application.Services.Implementations;
using NUnit.Framework;

namespace EasyHMSAPI.UnitTests.ServiceTests
{
    [TestFixture]
    public class DictationAccuracyRulesTests
    {
        private static readonly string[] Known = { "Tab. Ticagrelor 90 mg", "Inj. Ceftriaxone 1 g", "Tab. Pantoprazole 40 mg", "Tab. Aspirin 75 mg" };

        [Test]
        public void Hint_names_the_patients_medicines_and_common_terms()
        {
            var hint = DictationAccuracyRules.BuildVocabularyHint(Known.Concat(new[] { "tab. ticagrelor 90 mg", "  ", null }));
            Assert.That(hint, Does.Contain("Tab. Ticagrelor 90 mg"));
            Assert.That(hint, Does.Contain("Inj. Ceftriaxone 1 g"));
            Assert.That(hint, Does.Contain("TDS"));
            // The same medicine in different case is listed once.
            Assert.That(hint.Split("icagrelor").Length - 1, Is.EqualTo(1));
        }

        [Test]
        public void Hint_stays_short_enough_for_the_transcriber_however_many_medicines_there_are()
        {
            var many = Enumerable.Range(1, 200).Select(i => $"Tab. Medicinename{i} 500 mg");
            var hint = DictationAccuracyRules.BuildVocabularyHint(many);
            Assert.That(hint.Length, Is.LessThanOrEqualTo(DictationAccuracyRules.MaxHintLength));
            Assert.That(hint, Does.EndWith("after food."));
        }

        [Test]
        public void Hint_with_no_medicines_still_reads_properly()
        {
            Assert.That(DictationAccuracyRules.BuildVocabularyHint(Enumerable.Empty<string?>()), Does.StartWith("Hospital discharge dictation by a doctor. Terms:"));
        }

        [Test]
        public void A_medicine_on_record_comes_back_in_the_recorded_spelling()
        {
            var check = DictationAccuracyRules.CheckMedicineName("pantoprazole", Known);
            Assert.That(check.Status, Is.EqualTo(DictationAccuracyRules.NameKnown));
            Assert.That(check.Name, Is.EqualTo("Tab. Pantoprazole 40 mg"));
            Assert.That(check.HeardAs, Is.Null);
        }

        [Test]
        public void A_near_miss_is_corrected_and_says_what_was_heard()
        {
            var check = DictationAccuracyRules.CheckMedicineName("Ticagrelol", Known);
            Assert.That(check.Status, Is.EqualTo(DictationAccuracyRules.NameCorrected));
            Assert.That(check.Name, Is.EqualTo("Tab. Ticagrelor 90 mg"));
            Assert.That(check.HeardAs, Is.EqualTo("Ticagrelol"));
        }

        [Test]
        public void A_medicine_not_on_record_is_kept_as_heard_and_marked_unverified()
        {
            var check = DictationAccuracyRules.CheckMedicineName("Metformin 500", Known);
            Assert.That(check.Status, Is.EqualTo(DictationAccuracyRules.NameUnverified));
            Assert.That(check.Name, Is.EqualTo("Metformin 500"));
        }

        [Test]
        public void A_different_drug_with_a_similar_name_is_never_swapped_in()
        {
            // Ceftriaxone vs cefotaxime, and a short name, must not be "corrected" into something else.
            Assert.That(DictationAccuracyRules.CheckMedicineName("Cefotaxime", Known).Status, Is.EqualTo(DictationAccuracyRules.NameUnverified));
            Assert.That(DictationAccuracyRules.CheckMedicineName("Asp", Known).Status, Is.EqualTo(DictationAccuracyRules.NameUnverified));
        }

        [Test]
        public void Transcript_medicine_names_heard_slightly_wrong_are_fixed_and_reported()
        {
            var result = DictationAccuracyRules.CorrectTranscript("Continue Ticagrelol twice daily and stop ceftriaxon. Patient is comfortable and afebrile.", Known);
            Assert.That(result.Text, Is.EqualTo("Continue Ticagrelor twice daily and stop ceftriaxone. Patient is comfortable and afebrile."));
            Assert.That(result.Corrections.Select(c => $"{c.Heard}>{c.Corrected}"), Is.EqualTo(new[] { "Ticagrelol>Ticagrelor", "ceftriaxon>ceftriaxone" }));
        }

        [Test]
        public void Ordinary_words_and_correct_names_are_left_alone()
        {
            const string text = "Patient tolerated aspirin and pantoprazole. Continue as planned; particular care with fluids.";
            var result = DictationAccuracyRules.CorrectTranscript(text, Known);
            Assert.That(result.Text, Is.EqualTo(text));
            Assert.That(result.Corrections, Is.Empty);
            Assert.That(DictationAccuracyRules.CorrectTranscript(text, new string[0]).Text, Is.EqualTo(text));
        }

        [Test]
        public void Picks_out_every_dose_once()
        {
            var doses = DictationAccuracyRules.DosesToCheck("Pantoprazole 40 mg once daily, insulin 10 units, syrup 5ml, again 40 MG, platelets 1.1 lakh, review in 7 days, 0.5 mcg");
            Assert.That(doses, Is.EqualTo(new[] { "40 mg", "10 units", "5ml", "0.5 mcg" }));
            Assert.That(DictationAccuracyRules.DosesToCheck(null), Is.Empty);
        }

        [Test]
        public void Reads_the_text_and_the_unclear_segments_from_the_transcriber()
        {
            const string body = "{\"text\":\" Stop ceftriaxone. Add pantoprazole. \",\"segments\":[" +
                                "{\"text\":\" Stop ceftriaxone.\",\"avg_logprob\":-0.2,\"no_speech_prob\":0.01,\"compression_ratio\":1.2}," +
                                "{\"text\":\" Add pantoprazole.\",\"avg_logprob\":-1.4,\"no_speech_prob\":0.02,\"compression_ratio\":1.1}," +
                                "{\"text\":\" (noise)\",\"avg_logprob\":-0.3,\"no_speech_prob\":0.9,\"compression_ratio\":1.0}]}";
            var result = VoiceRxService.ParseDictationTranscript(body);
            Assert.That(result.Text, Does.Contain("Stop ceftriaxone"));
            Assert.That(result.UnclearPhrases, Is.EqualTo(new[] { "Add pantoprazole.", "(noise)" }));
            // An answer without segments (plain json) still gives the text.
            Assert.That(VoiceRxService.ParseDictationTranscript("{\"text\":\"hello\"}").UnclearPhrases, Is.Empty);
        }
    }
}
