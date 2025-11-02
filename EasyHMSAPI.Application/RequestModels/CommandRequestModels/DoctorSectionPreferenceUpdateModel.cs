using System;
using System.Diagnostics.CodeAnalysis;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    [ExcludeFromCodeCoverage]
    public class DoctorSectionPreferenceUpdateModel
    {
        public bool? Vitals { get; set; }
        public bool? ChiefComplaint { get; set; }
        public bool? History { get; set; }
        public bool? Comorbidity { get; set; }
        public bool? Examination { get; set; }
        public bool? Diagnosis { get; set; }
        public bool? Investigations { get; set; }
        public bool? Procedures { get; set; }
        public bool? Medications { get; set; }
        public bool? PrivateNotes { get; set; }
        public bool? CertificatesAndNotes { get; set; }
        public bool? Immunizations { get; set; }
        public bool? FollowUpAndReferral { get; set; }
        public bool? NonPharmacologicalAdvice { get; set; }
        public bool? Attachments { get; set; }
    }
}