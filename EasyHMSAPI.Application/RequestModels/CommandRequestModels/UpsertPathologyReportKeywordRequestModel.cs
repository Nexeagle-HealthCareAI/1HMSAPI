using System;
using System.Diagnostics.CodeAnalysis;
using EasyHMSAPI.Application.ResponseModels.CommandResponseModels;
using MediatR;
using System.Text.Json.Serialization;

namespace EasyHMSAPI.Application.RequestModels.CommandRequestModels
{
    // Upsert: KeywordId present => update that keyword in place; absent => create a new one.
    [ExcludeFromCodeCoverage]
    public class UpsertPathologyReportKeywordRequestModel : IRequest<UpsertPathologyReportKeywordResponseModel>
    {
        public Guid HospitalId { get; set; }
        [JsonIgnore]
        public string? LoggedInUserName { get; set; }

        public Guid? KeywordId { get; set; }
        public Guid? TestId { get; set; }
        public string Keyword { get; set; } = null!;
        public string ContentJson { get; set; } = null!;
        public bool IsActive { get; set; } = true;
    }
}
