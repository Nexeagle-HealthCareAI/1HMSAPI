using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using MediatR;
using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    [ExcludeFromCodeCoverage]
    public class GetPathologyOrdersQuery : IRequest<List<PathologyOrderDto>>
    {
        public Guid HospitalId { get; set; }
        public string? Status { get; set; }
    }

    [ExcludeFromCodeCoverage]
    public class GetPathologyOrderByIdQuery : IRequest<PathologyOrderDto>
    {
        public Guid HospitalId { get; set; }
        public Guid OrderId { get; set; }
    }

    // Powers DocBoard's "Lab Report Ready" badge -- fetched once per hospital (same shape as
    // AdmissionReferralItem's fetch-once-and-index-by-patientId pattern in DocBoard.tsx), not
    // per appointment row, to avoid an N+1 request per visible patient.
    [ExcludeFromCodeCoverage]
    public class GetRecentlyApprovedPathologyReportsQuery : IRequest<List<PathologyReportReadyDto>>
    {
        public Guid HospitalId { get; set; }
    }
}
