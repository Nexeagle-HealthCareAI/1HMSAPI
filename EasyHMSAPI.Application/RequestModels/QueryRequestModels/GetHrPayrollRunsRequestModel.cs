using EasyHMSAPI.Application.ResponseModels.QueryResponseModels;
using MediatR;
using System;

namespace EasyHMSAPI.Application.RequestModels.QueryRequestModels
{
    public class GetHrPayrollRunsRequestModel : IRequest<GetHrPayrollRunsResponseModel>
    {
        public Guid HospitalId { get; set; }
        public int? Month { get; set; }
        public int? Year { get; set; }
        public string? Status { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }
}
