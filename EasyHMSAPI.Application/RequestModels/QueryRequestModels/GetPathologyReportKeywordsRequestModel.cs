using System;
using System.Diagnostics.CodeAnalysis;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    // TestId omitted/null -> every keyword in the hospital, any scope (the Keywords management
    // tab's "list everything" view). A real TestId -> only that test's own keywords PLUS every
    // global (TestId IS NULL) keyword -- both are usable while reporting on that specific test,
    // which is what OrderResultEntry.tsx's lookup actually needs.
    [ExcludeFromCodeCoverage]
    public class GetPathologyReportKeywordsRequestModel : IRequest<GetPathologyReportKeywordsResponseModel>
    {
        public Guid HospitalId { get; set; }
        public Guid? TestId { get; set; }
        public bool IncludeInactive { get; set; }
    }
}
